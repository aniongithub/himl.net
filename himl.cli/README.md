# himl.cli

Command-line interface for himl.net that provides exact parity with the original Adobe HIML `himl-config-merger` tool. This package is published as a .NET tool.

## Installation

Install the CLI tool as a .NET global tool:

```bash
dotnet tool install -g himl.cli
```

Update to the latest version:

```bash
dotnet tool update -g himl.cli
```

For development/testing with local packages:

```bash
dotnet tool install --global --add-source ./nupkg himl.cli --version 1.0.0
```

## Usage

The CLI generates configuration files from hierarchical YAML, exactly matching the behavior of `himl-config-merger`.

### Hierarchical Configuration

The tool reads YAML files from a directory hierarchy, starting from the root (default values) down to the leaves (most specific values). Values in deeper directories override values from parent directories.

**Example hierarchy:**
```
config/
├── default.yaml              # Base configuration
└── env=prod/
    ├── env.yaml              # Production environment overrides
    └── region=us-east-1/
        ├── region.yaml       # Region-specific overrides
        └── cluster=web/
            └── cluster.yaml  # Cluster-specific overrides (leaf)
```

The tool processes this hierarchy by:
1. Loading `default.yaml` (base values)
2. Merging with `env=prod/env.yaml` (environment overrides)
3. Merging with `env=prod/region=us-east-1/region.yaml` (region overrides)
4. Merging with `env=prod/region=us-east-1/cluster=web/cluster.yaml` (final overrides)
5. Extracting key-value pairs from directory names (`env=prod`, `region=us-east-1`, `cluster=web`)
6. Producing final configuration for the leaf directory

### Basic Usage

```bash
himl.cli <path> --output-dir <output-dir> --levels <levels...> --leaf-directories <leaf-directories...>
```

### Examples

Process a complex hierarchy with environment, region, and cluster levels:

```bash
himl.cli examples/complex --output-dir /tmp/output --levels env region cluster --leaf-directories cluster
```

Process a simple environment-based hierarchy:

```bash
himl.cli test-config --output-dir /tmp/output --levels env --leaf-directories env
```

#### Merge Modes

**All Files Mode (default)**: Merges all YAML files in each directory into a single output file per leaf.

```bash
himl.cli examples/complex --output-dir /tmp/output --levels env region cluster --leaf-directories cluster --merge-mode all-files
```

This generates: `cluster1.yaml`, `cluster2.yaml`, etc., with all configuration merged into each file.

**Same-Named Files Mode**: Merges only same-named YAML files across the hierarchy, producing multiple output files per leaf.

```bash
himl.cli examples/multi-file --output-dir /tmp/output --levels env region --leaf-directories region --merge-mode same-named-files
```

With a structure like:
```
examples/multi-file/
├── app1.yaml          # Base app1 configuration
├── app2.yaml          # Base app2 configuration
└── env=dev/
    ├── app1.yaml      # Dev overrides for app1
    ├── app2.yaml      # Dev overrides for app2
    └── region=us-east-1/
        ├── app1.yaml  # Region-specific app1 config
        └── app2.yaml  # Region-specific app2 config
```

This generates: `us-east-1-app1.yaml`, `us-east-1-app2.yaml`, where each file contains only the merged configuration from files with the same name across the hierarchy.

### Required Arguments

- `path` - The configs directory to process
- `--output-dir` - Output directory where generated configs will be saved
- `--levels` - Hierarchy levels (e.g., env, region, cluster)
- `--leaf-directories` - Leaf directories that define output files (e.g., cluster)

### Optional Arguments

- `--enable-parallel` - Process config using multiprocessing
- `--filter-rules-key` - Keep only these keys from the generated data, based on the configured filter key
- `--merge-mode` - File merging mode: `all-files` (default) or `same-named-files`

## Secret Resolution

The CLI automatically resolves secrets from various cloud providers during configuration processing. Your YAML files can reference secrets using interpolation syntax:

```yaml
# Example configuration with secrets
database:
  host: "prod-db.example.com"
  password: "{{gcp-sm://my-project/db-password/latest}}"
  
api:
  key: "{{ssm:/app/api-key}}"
  
ssl:
  certificate: "{{vault:/secret/data/ssl:cert}}"
  
backup:
  credentials: "{{s3:backup-bucket:service-account.json}}"

# Environment variables with defaults
environment:
  log_level: "{{env(LOG_LEVEL):INFO}}"
  database_url: "{{env(DATABASE_URL):localhost:5432}}"
```

### Supported Secret Managers

- **Google Secret Manager**: `{{gcp-sm://project-id/secret-name/version}}`
  - Format: `gcp-sm://PROJECT_ID/SECRET_NAME/VERSION`
  - Version can be "latest" or a specific version number
  - Requires Google Cloud authentication (service account or user credentials)

- **AWS Systems Manager Parameter Store**: `{{ssm:/parameter/path}}`
  - Supports regions and profiles: `{{ssm:/param:region:profile}}`

- **AWS S3**: `{{s3:bucket-name:object-key}}`
  - Optional base64 encoding: `{{s3:bucket:key:true}}`

- **HashiCorp Vault**: `{{vault:/secret/path:key}}`
  - Supports token-based authentication

### Environment Variables

Environment variables use double-brace syntax with optional default values:

- **Basic**: `{{env(VAR_NAME)}}`
- **With default**: `{{env(VAR_NAME):default_value}}`

Examples:
```yaml
database:
  host: "{{env(DB_HOST):localhost}}"
  port: "{{env(DB_PORT):5432}}"
  timeout: "{{env(DB_TIMEOUT):30}}"
logging:
  level: "{{env(LOG_LEVEL):INFO}}"
```

### Authentication

Secret resolution uses the default credential chains for each provider:

- **Google Cloud**: `GOOGLE_APPLICATION_CREDENTIALS` environment variable or `gcloud auth application-default login`
- **AWS**: AWS credentials file, IAM roles, or environment variables (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`)
- **Vault**: `VAULT_TOKEN` environment variable or vault agent

If authentication fails or secrets cannot be resolved, the original interpolation string is preserved in the output.

## File Merge Modes

The CLI supports two different merge modes to handle multiple YAML files:

### All Files Mode (default)

Merges all YAML files in each directory into a single output file per leaf. This is the traditional behavior and matches the original Adobe HIML tool.

**Use cases:**
- Single application with environment-specific overrides
- Monolithic configuration where all settings go into one file
- Traditional deployment scenarios

**Output:** One file per leaf directory (e.g., `cluster1.yaml`, `cluster2.yaml`)

### Same-Named Files Mode

Merges only same-named YAML files across the hierarchy, producing multiple output files per leaf. This enables more granular configuration management.

**Use cases:**
- Microservices with separate configuration files per service
- Multi-application deployments
- Separating concerns (database config, API config, etc.)

**Output:** Multiple files per leaf directory (e.g., `cluster1-app1.yaml`, `cluster1-app2.yaml`)

**Example structure:**
```
config/
├── database.yaml       # Base database config
├── api.yaml           # Base API config
└── env=prod/
    ├── database.yaml  # Production database overrides
    ├── api.yaml       # Production API overrides
    └── region=us-east-1/
        ├── database.yaml  # Region-specific database config
        └── api.yaml       # Region-specific API config
```

With `--merge-mode same-named-files`, this produces:
- `us-east-1-database.yaml` (merged from all database.yaml files)
- `us-east-1-api.yaml` (merged from all api.yaml files)

## Troubleshooting

### Common Issues

**No output files generated:**
- Check that your directory structure matches the specified `--levels` and `--leaf-directories`
- Ensure YAML files exist in the hierarchy
- Verify the path argument points to the correct root directory

**Secret resolution failures:**
- Check authentication credentials for the respective cloud providers
- Verify secret paths and permissions
- Original interpolation strings are preserved if resolution fails

**Interpolation not working:**
- Use double braces for environment variables: `{{env(VAR_NAME)}}`
- Use double braces for secrets: `{{ssm:/path/to/secret}}`
- Check for syntax errors in interpolation expressions

### Debug Mode

Run with verbose logging to see detailed processing information:

```bash
# Note: Add --verbose flag when implemented, or check logs in the terminal output
himl.cli config --output-dir output --levels env --leaf-directories env
```

## Documentation

For comprehensive documentation, examples, and .NET library usage, see the main repository:

**Repository:** https://github.com/aniongithub/himl.net

**Features covered in the main documentation:**
- .NET library usage and Microsoft.Extensions.Configuration integration
- Advanced interpolation examples
- Deep merge strategies
- Complete secret manager configurations
- File inheritance patterns
- Programming examples
