# himl.cli

Command-line interface for himl.net that provides exact parity with the original Adobe HIML `himl-config-merger` tool. This package is published as a .NET tool.

## Installation

Install the CLI tool from NuGet:

```bash
dotnet tool install -g himl.cli
```

Or install from a local nupkg folder (useful for testing):

```bash
dotnet tool install --global --add-source ./nupkg himl.cli --version 1.0.0
```

## Usage

The CLI generates configuration files from hierarchical YAML, exactly matching the behavior of `himl-config-merger`.

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

### Required Arguments

- `path` - The configs directory to process
- `--output-dir` - Output directory where generated configs will be saved
- `--levels` - Hierarchy levels (e.g., env, region, cluster)
- `--leaf-directories` - Leaf directories that define output files (e.g., cluster)

### Optional Arguments

- `--enable-parallel` - Process config using multiprocessing
- `--filter-rules-key` - Keep only these keys from the generated data, based on the configured filter key

## Secret Resolution

The CLI automatically resolves secrets from various cloud providers during configuration processing. Your YAML files can reference secrets using interpolation syntax:

```yaml
# Example configuration with secrets
database:
  host: "prod-db.example.com"
  password: "${gcp-sm://my-project/db-password/latest}"
  
api:
  key: "${ssm:/app/api-key}"
  
ssl:
  certificate: "${vault:/secret/data/ssl:cert}"
  
backup:
  credentials: "${s3:backup-bucket:service-account.json}"
```

### Supported Secret Managers

- **Google Secret Manager**: `${gcp-sm://project-id/secret-name/version}`
  - Format: `gcp-sm://PROJECT_ID/SECRET_NAME/VERSION`
  - Version can be "latest" or a specific version number
  - Requires Google Cloud authentication (service account or user credentials)

- **AWS Systems Manager Parameter Store**: `${ssm:/parameter/path}`
  - Supports regions and profiles: `${ssm:/param:region:profile}`

- **AWS S3**: `${s3:bucket-name:object-key}`
  - Optional base64 encoding: `${s3:bucket:key:true}`

- **HashiCorp Vault**: `${vault:/secret/path:key}`
  - Supports token-based authentication

### Authentication

Secret resolution uses the default credential chains for each provider:

- **Google Cloud**: `GOOGLE_APPLICATION_CREDENTIALS` environment variable or `gcloud auth application-default login`
- **AWS**: AWS credentials file, IAM roles, or environment variables (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`)
- **Vault**: `VAULT_TOKEN` environment variable or vault agent

If authentication fails or secrets cannot be resolved, the original interpolation string is preserved in the output.

## Documentation

For full documentation and examples, see the repository README: https://github.com/aniongithub/himl.net
