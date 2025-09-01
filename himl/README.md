# himl

Core library for himl.net (Hierarchical YAML configuration for .NET).

## Installation

Install the library package:

```sh
dotnet add package himl
```

Or via Package Manager Console:

```powershell
Install-Package himl
```

## Basic Usage

Merge `simple/default.yaml` with `simple/production/env.yaml`:

```csharp
using himl;
using himl.core;

var processor = new ConfigurationProcessor(/* dependencies via DI */);
var options = new HimlOptions
{
    OutputFormat = OutputFormat.Yaml,
    ListMergeStrategy = ListMergeStrategy.AppendUnique,
    MergeMode = MergeMode.AllFiles  // Default: merge all files into one output
};

var result = await processor.ProcessAsync("examples/simple/production", options);
Console.WriteLine(result.Output);
```

### File Merge Modes

The library supports two different merge modes:

**All Files Mode (default)**: Merges all YAML files in each directory into a single output:

```csharp
var options = new HimlOptions
{
    MergeMode = MergeMode.AllFiles  // Traditional single output
};

var result = await processor.ProcessAsync("config/env=prod", options);
Console.WriteLine(result.Output);  // Single merged configuration
```

**Same-Named Files Mode**: Merges only same-named YAML files across the hierarchy, producing multiple outputs:

```csharp
var options = new HimlOptions
{
    MergeMode = MergeMode.SameNamedFiles  // Multiple outputs per leaf
};

var result = await processor.ProcessAsync("config/env=prod/region=us-east-1", options);

// Access individual configurations
foreach (var output in result.MultipleOutputs)
{
    var filename = output.Key;        // e.g., "app1", "app2", "database"
    var config = output.Value;        // Merged configuration for that file
    
    Console.WriteLine($"=== {filename} Configuration ===");
    Console.WriteLine(result.MultipleFormattedOutputs[filename]);
}
```

### Using with Microsoft.Extensions.Configuration

himl.net integrates seamlessly with the .NET configuration system:

```csharp
using himl.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add HIML configuration source
builder.Configuration.AddHiml("config/production", options =>
{
    options.ListMergeStrategy = ListMergeStrategy.AppendUnique;
    options.DictMergeStrategy = DictMergeStrategy.Merge;
    options.MergeMode = MergeMode.AllFiles;  // Default merge mode
});

var host = builder.Build();

// Access configuration values
var config = host.Services.GetRequiredService<IConfiguration>();
var dbConnectionString = config["Database:ConnectionString"];
var logLevel = config["Logging:LogLevel:Default"];
```

You can also chain multiple HIML sources with different strategies:

```csharp
builder.Configuration
    .AddHiml("config/defaults")  // Base configuration
    .AddHiml("config/environment/production", options =>
    {
        options.SkipInterpolations = false;
        options.MergeMode = MergeMode.SameNamedFiles;  // Multiple outputs if needed
    })
    .AddHiml("config/overrides", optional: true);  // Optional overrides
```

### Secret Resolution

The library supports multiple secret managers for resolving secrets in your configuration:

```csharp
// Enable secret resolution in configuration
builder.Configuration.AddHiml("config/production", options =>
{
    options.SkipSecrets = false;  // Enable secret resolution (default)
    options.DefaultAwsProfile = "production";  // Optional AWS profile
});
```

Your YAML configuration files can reference secrets using interpolation syntax:

```yaml
# config/production/database.yaml
database:
  host: "prod-db.example.com"
  password: "{{gcp-sm://my-project/db-password/latest}}"
  
api:
  key: "{{ssm:/app/api-key}}"
  
ssl:
  certificate: "{{vault:/secret/data/ssl:cert}}"
  
backup:
  credentials: "{{s3:backup-bucket:credentials.json}}"

# Environment variables with defaults
environment:
  log_level: "{{env(LOG_LEVEL):INFO}}"
  database_url: "{{env(DATABASE_URL):localhost:5432}}"
```

**Supported secret managers:**
- **Google Secret Manager**: `{{gcp-sm://project-id/secret-name/version}}`
- **AWS Systems Manager**: `{{ssm:/parameter/path}}`
- **AWS S3**: `{{s3:bucket-name:object-key}}`
- **HashiCorp Vault**: `{{vault:/secret/path:key}}`
- **Environment Variables**: `{{env(VAR_NAME):default_value}}`

Authentication is handled through the respective cloud provider SDKs using default credential chains.

### Configuration Options

The library supports extensive configuration options for customizing merge behavior:

```csharp
var options = new HimlOptions
{
    // Input/Output formats
    InputFormat = InputFormat.Yaml,
    OutputFormat = OutputFormat.Json,
    WorkingDirectory = "/app/config",
    
    // File merge mode
    MergeMode = MergeMode.AllFiles,        // or MergeMode.SameNamedFiles
    
    // Merge strategies
    ListMergeStrategy = ListMergeStrategy.AppendUnique,  // How to merge lists
    DictMergeStrategy = DictMergeStrategy.Merge,         // How to merge dictionaries
    
    // Filtering
    Filters = new[] { "Database", "Logging" },           // Include only these keys
    ExcludeKeys = new[] { "Secrets", "Internal" },       // Exclude these keys
    
    // Processing control
    SkipInterpolations = false,        // Enable variable interpolation
    SkipSecrets = false,               // Enable secret resolution
    
    // Output wrapping
    EnclosingKey = "MyApp",            // Wrap output under this key
    RemoveEnclosingKey = "Wrapper",    // Remove this wrapper key
    
    // Formatting
    MultiLineString = true,            // Use YAML multi-line strings
    
    // Cloud provider settings
    DefaultAwsProfile = "production"   // Default AWS profile for secrets
};

var result = await processor.ProcessAsync("config/production", options);
```

### Using the CLI

The `himl.cli` CLI tool provides exact parity with the original Adobe HIML `himl-config-merger` tool and supports the same merge modes as the library.

Install the CLI tool:

```sh
dotnet tool install -g himl.cli
```

Basic usage:

```sh
himl.cli <path> --output-dir <output-dir> --levels <levels...> --leaf-directories <leaf-directories...>
```

**All Files Mode** (default):
```sh
himl.cli examples/complex --output-dir /tmp/output --levels env region cluster --leaf-directories cluster --merge-mode all-files
```

**Same-Named Files Mode**:
```sh
himl.cli examples/multi-file --output-dir /tmp/output --levels env region --leaf-directories region --merge-mode same-named-files
```

## Documentation

For comprehensive documentation, examples, and advanced usage patterns, see the main repository:

**Repository:** https://github.com/aniongithub/himl.net

**Key topics covered:**
- Advanced interpolation and variable substitution
- Deep merge strategies and configuration
- Complete secret manager integration examples
- Microsoft.Extensions.Configuration patterns
- CLI usage and automation scenarios
- File inheritance and extension patterns
