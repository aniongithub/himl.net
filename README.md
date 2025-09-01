# himl.net

A **HI**erarchical YA**ML** configuration system for .NET, inspired by [Adobe HIML](https://github.com/adobe/himl).

## Description

A .NET library which allows you to merge hierarchical config files using YAML syntax. It offers deep merge, variable interpolation, secrets retrieval from cloud providers (AWS SSM, S3, Vault), and seamless integration with Microsoft.Extensions.Configuration.

It is ideal if you want to structure your hierarchy in such a way that you avoid duplication. You can define a structure for your configuration using a hierarchy such environment/project/cluster/app. It is up to you what layers you want to use in this hierarchy. The tool will read all YAML files starting from the root (where default values would be) all the way to the leaf (where most specific values would be, which will take precedence).

The idea came from puppet's hiera, originally implemented by [Adobe HIML](https://github.com/adobe/himl) for Python.

[![CI Build](https://github.com/aniongithub/himl.net/actions/workflows/ci-build.yml/badge.svg)](https://github.com/aniongithub/himl.net/actions/workflows/ci-build.yml)

## Table of Contents

- [himl.net](#himlnet)
  - [Description](#description)
  - [Table of Contents](#table-of-contents)
  - [Installation](#installation)
    - [Using NuGet Package](#using-nuget-package)
    - [Using .NET Global Tool](#using-net-global-tool)
    - [From Source](#from-source)
  - [Development](#development)
  - [Examples](#examples)
    - [Using the .NET library](#using-the-net-library)
    - [Using Microsoft.Extensions.Configuration](#using-microsoftextensionsconfiguration)
    - [Using the CLI](#using-the-cli)
  - [Features](#features)
    - [Interpolation](#interpolation)
      - [Interpolating simple values](#interpolating-simple-values)
      - [Interpolating whole objects](#interpolating-whole-objects)
      - [Environment variables](#environment-variables)
    - [Deep merge](#deep-merge)
    - [Secrets retrieval](#secrets-retrieval)
      - [AWS SSM](#aws-ssm)
      - [AWS S3](#aws-s3)
      - [Vault](#vault)
      - [Google Secret Manager](#google-secret-manager)
    - [File inheritance](#file-inheritance)
    - [Merge strategies](#merge-strategies)
    - [File merge modes](#file-merge-modes)
  - [Configuration](#configuration)

## Installation

### Using NuGet Package

```sh
dotnet add package himl
```

Or via Package Manager Console:

```powershell
Install-Package himl
```

### Using .NET Global Tool

Install the CLI tool (published as the `himl.cli` package):

```sh
dotnet tool install -g himl.cli
```

Or install from a local folder containing the packed nupkgs (useful for testing):

```sh
dotnet tool install --global --add-source ./nupkg himl.cli --version 1.0.0
```

Then use the tool:

```sh
himl.cli --help
```

### From Source

```sh
git clone https://github.com/aniongithub/himl.net
cd himl.net
dotnet build
```

## Development

Dev Containers are the recommended way to develop the code in this repository. They provide a reproducible development environment (OS, tooling, and extensions) that matches CI and other contributors, so you can avoid the typical "works on my machine" problems. This project includes a devcontainer configuration and a helper Compose file to run dependent services (for example, a local Vault instance used by tests).

- Overview: https://code.visualstudio.com/docs/devcontainers/containers
- Dev Containers tutorial / installation: https://code.visualstudio.com/docs/devcontainers/tutorial

Using a devcontainer allows you to open the repository in VS Code and have the correct .NET SDK, tooling, and services available automatically.

**YMMV with other development methods as these will not be supported in any way.**

## Examples

### Using the .NET library

This will merge `simple/default.yaml` with `simple/production/env.yaml`:

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

For multiple outputs using same-named file merging:

```csharp
var options = new HimlOptions
{
    MergeMode = MergeMode.SameNamedFiles  // Produce multiple outputs
};

var result = await processor.ProcessAsync("examples/multi-file/env=dev/region=us-east-1", options);

// Access individual application configurations
foreach (var output in result.MultipleOutputs)
{
    Console.WriteLine($"=== {output.Key} Configuration ===");
    Console.WriteLine(result.MultipleFormattedOutputs[output.Key]);
}
```

Directory structure:

```
examples/simple
├── default.yaml
└── production
    └── env.yaml
```

The example showcases deep merging of dictionaries and lists.

`examples/simple/default.yaml`:

```yaml
---
env: default
deep:
  key1: v1
  key2: v2
deep_list:
  - item1
  - item2
```

`examples/simple/production/env.yaml`:

```yaml
---
env: prod
deep:
  key3: v3
deep_list:
  - item3
```

Result:

```yaml
env: prod
deep:
  key1: v1
  key2: v2
  key3: v3
deep_list:
- item1
- item2
- item3
```

### Using Microsoft.Extensions.Configuration

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

You can also use multiple HIML sources with different merge strategies:

```csharp
builder.Configuration
    .AddHiml("config/defaults")
    .AddHiml("config/environment/production", options =>
    {
        options.SkipInterpolations = false;
    })
    .AddHiml("config/overrides", optional: true);
```

### Using the CLI

The `himl.cli` CLI tool provides exact parity with the original Adobe HIML `himl-config-merger` tool. It generates configuration files from hierarchical YAML by scanning a root configuration tree and writing one merged file per leaf into an output directory.

#### Installation

```sh
dotnet tool install -g himl.cli
```

#### Basic Usage

```sh
himl.cli <path> --output-dir <output-dir> --levels <levels...> --leaf-directories <leaf-directories...>
```

#### Required Arguments

- `path` - The configs directory to process
- `--output-dir` - Output directory where generated configs will be saved  
- `--levels` - Hierarchy levels (e.g., env region cluster)
- `--leaf-directories` - Leaf directories that define output files (e.g., cluster)

#### Optional Arguments

- `--enable-parallel` - Process config using multiprocessing
- `--filter-rules-key` - Keep only these keys from the generated data, based on the configured filter key
- `--merge-mode` - File merging mode: `all-files` (default) or `same-named-files`

#### Examples

Process a complex hierarchy with environment, region, and cluster levels:

```sh
himl.cli examples/complex --output-dir /tmp/output --levels env region cluster --leaf-directories cluster
```

This will generate output files named after the leaf directory values (e.g., `cluster1.yaml`, `cluster2.yaml`, `ireland1.yaml`).

Process a simple environment-based hierarchy:

```sh
himl.cli test-config --output-dir /tmp/output --levels env --leaf-directories env
```

This will generate files like `dev.yaml` based on directories like `env=dev/`.

#### Merge Modes

The CLI supports two different merge modes:

**All Files Mode (default)**: Merges all YAML files in each directory into a single output file per leaf.

```sh
himl.cli examples/complex --output-dir /tmp/output --levels env region cluster --leaf-directories cluster --merge-mode all-files
```

This generates: `cluster1.yaml`, `cluster2.yaml`, etc., with all configuration merged into each file.

**Same-Named Files Mode**: Merges only same-named YAML files across the hierarchy, producing multiple output files per leaf.

```sh
himl.cli examples/multi-file --output-dir /tmp/output --levels env region --leaf-directories region --merge-mode same-named-files
```

With this structure:
```
examples/multi-file/
├── app1.yaml
├── app2.yaml
└── env=dev/
    ├── app1.yaml
    ├── app2.yaml
    └── region=us-east-1/
        ├── app1.yaml
        └── app2.yaml
```

This generates: `us-east-1-app1.yaml`, `us-east-1-app2.yaml`, where each file contains only the merged configuration from files with the same name across the hierarchy.

## Features

### Interpolation

Variable interpolation allows you to define values once and reuse them throughout your configuration files. Unlike YAML anchors, these interpolations work across multiple files.

#### Interpolating simple values

`config/default.yaml`:

```yaml
database:
  host: "${env:DB_HOST:localhost}"
  port: 5432
api:
  baseUrl: "https://${database.host}:8080"
  endpoints:
    users: "${api.baseUrl}/users"
    orders: "${api.baseUrl}/orders"
```

#### Interpolating whole objects

```yaml
projects:
  webapp1:
    tagging:
      Owner: "Web Service Team"
      Environment: "dev"
      CostCenter: "123"
  datastore:
    tagging:
      Owner: "Backend Team"
      Environment: "dev"
      CostCenter: "455"

# Copy the entire tagging object
myapp:
  tags: "${projects.webapp1.tagging}"

# Dynamic interpolation
selectedProject: "webapp1"
dynamicTags: "${projects.${selectedProject}.tagging}"
```

#### Environment variables

```yaml
database:
  connectionString: "{{env(CONNECTION_STRING)}}"
  timeout: "{{env(DB_TIMEOUT):30}}"  # Default value of 30
  host: "{{env(DB_HOST):localhost}}"
logging:
  level: "{{env(LOG_LEVEL):Information}}"
```

### Deep merge

Dictionary and list values are deep-merged across the hierarchy:

```csharp
var options = new HimlOptions
{
    DictMergeStrategy = DictMergeStrategy.Merge,  // Deep merge dictionaries
    ListMergeStrategy = ListMergeStrategy.AppendUnique  // Append unique items
};
```

### Secrets retrieval

#### AWS SSM

```yaml
database:
  password: "${ssm:/app/database/password}"
  credentials: "${ssm:/app/database/credentials:us-west-2:myprofile}"
```

#### AWS S3

```yaml
certificate:
  content: "${s3:my-certs-bucket:certs/app.pem:true}"  # base64 encoded
config:
  template: "${s3:config-bucket:templates/app.json}"
```

#### Vault

```yaml
database:
  password: "${vault:/secret/data/database:password}"
auth:
  token: "${vault.token:my-policy}"
secrets: "${vault:/secret/data/app}"  # Entire secret object
```

#### Google Secret Manager

```yaml
database:
  password: "${gcp-sm://my-project/db-password/latest}"
  apiKey: "${gcp-sm://my-project/api-key/3}"  # Specific version
application:
  serviceAccount: "${gcp-sm://prod-project/service-account/latest}"
```

### File inheritance

Use the `extends` keyword to inherit from other files:

```yaml
# config/production/database.yaml
extends: ../base/database.yaml
database:
  host: "prod-db-server"  # Override base value
  pool:
    maxSize: 50  # Add new value
```

### Merge strategies

Control how lists and dictionaries are merged:

```csharp
var options = new HimlOptions
{
    ListMergeStrategy = ListMergeStrategy.Override,     // Replace entire list
    // ListMergeStrategy = ListMergeStrategy.Append,    // Append to list
    // ListMergeStrategy = ListMergeStrategy.Prepend,   // Prepend to list
    // ListMergeStrategy = ListMergeStrategy.AppendUnique, // Append unique items
  
    DictMergeStrategy = DictMergeStrategy.Merge,        // Deep merge
    // DictMergeStrategy = DictMergeStrategy.Override,  // Replace entire dict
};
```

### File merge modes

Control how multiple files are processed and merged:

```csharp
var options = new HimlOptions
{
    MergeMode = MergeMode.AllFiles,        // Merge all YAML files into one output (default)
    // MergeMode = MergeMode.SameNamedFiles, // Merge only same-named files, multiple outputs
};
```

**All Files Mode (default)**: Merges all YAML files in each directory into a single configuration. Later files override values from earlier files.

**Same-Named Files Mode**: Groups files by name across the hierarchy and merges only files with the same name together. This produces multiple output configurations, one for each unique filename.

```csharp
// Example: Same-named files mode
var processor = new ConfigurationProcessor(/* dependencies */);
var options = new HimlOptions { MergeMode = MergeMode.SameNamedFiles };
var result = await processor.ProcessAsync("config/env=prod/region=us-east-1", options);

// Access multiple outputs
foreach (var output in result.MultipleOutputs)
{
    var filename = output.Key;        // e.g., "app1", "app2"
    var config = output.Value;        // Merged configuration for that file
    Console.WriteLine($"Configuration for {filename}:");
    Console.WriteLine(result.MultipleFormattedOutputs[filename]);
}
```

## Configuration

himl.net supports extensive configuration options:

```csharp
var options = new HimlOptions
{
    InputFormat = InputFormat.Yaml,
    OutputFormat = OutputFormat.Json,
    WorkingDirectory = "/app/config",
  
    // Filtering
    Filters = new[] { "Database", "Logging" },          // Include only these keys
    ExcludeKeys = new[] { "Secrets", "Internal" },      // Exclude these keys
  
    // Processing
    SkipInterpolations = false,
    SkipSecrets = false,
  
    // Wrapping
    EnclosingKey = "MyApp",                            // Wrap output under this key
    RemoveEnclosingKey = "Wrapper",                    // Remove this wrapper key
  
    // Output formatting
    MultiLineString = true,                            // Use YAML multi-line strings
  
    // Merge strategies
    ListMergeStrategy = ListMergeStrategy.AppendUnique, // How to merge lists
    DictMergeStrategy = DictMergeStrategy.Merge,        // How to merge dictionaries
    MergeMode = MergeMode.AllFiles,                     // File merge mode
  
    // AWS configuration
    DefaultAwsProfile = "production"
};
```
