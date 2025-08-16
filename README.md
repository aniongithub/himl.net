# himl.net

A **HI**erarchical YA**ML** configuration system for .NET, inspired by [Adobe HIML](https://github.com/adobe/himl).

Latest version is: 1.0.0 (Pre-release)

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
    - [File inheritance](#file-inheritance)
    - [Merge strategies](#merge-strategies)
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
himl --help
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
    ListMergeStrategy = ListMergeStrategy.AppendUnique
};

var result = await processor.ProcessAsync("examples/simple/production", options);
Console.WriteLine(result.Output);
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

The `himl.cli` CLI tool (distributed as the `himl.cli` package) implements the upstream `himl-config-merger` behavior — it scans a root configuration tree and writes one merged file per leaf into an output directory. Use the `--levels` option to define which path segments (e.g. `env region cluster`) define leaves.

Required options
- `--output-dir` (or `-d`): output directory where generated files will be stored
- `--levels`: a space-separated list of level keys used to compute leaf directories (eg: `env region cluster`)

Basic usage (config-merger mode):

```sh
# run as a global tool
himl examples/complex --output-dir merged_output --levels env region cluster

# run as a dotnet tool by package name
dotnet himl.cli examples/complex --output-dir merged_output --levels env region cluster
```

This will produce a directory structure like:

```
merged_output
├── dev
│   ├── us-east-1
│   │   ├── cluster1.yaml
│   │   └── cluster2.yaml
│   └── us-west-2
│       └── cluster1.yaml
└── prod
    └── eu-west-2
        └── ireland1.yaml
```

Other useful options
- `--format` / `-f`: output format (`yaml` or `json`, default `yaml`)
- `--filter`: include only these top-level keys
- `--exclude`: exclude these top-level keys
- `--skip-interpolation-resolving`: skip interpolation resolution
- `--skip-secrets`: skip secret resolution
- `--cwd`: working directory to resolve relative paths
- `--enclosing-key` / `--remove-enclosing-key`: wrap or unwrap output under a key
- `--list-merge-strategy`: Override | Append | Prepend | AppendUnique

Notes
- The CLI writes merged files to an output directory. Use the `--levels` option to specify the hierarchy levels used to compute leaves (segments like `env=dev` are expected in directory names).

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
  connectionString: "${env:CONNECTION_STRING}"
  timeout: "${env:DB_TIMEOUT:30}"  # Default value of 30
logging:
  level: "${env:LOG_LEVEL:Information}"
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
  
    // AWS configuration
    DefaultAwsProfile = "production"
};
```
