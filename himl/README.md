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
    ListMergeStrategy = ListMergeStrategy.AppendUnique
};

var result = await processor.ProcessAsync("examples/simple/production", options);
Console.WriteLine(result.Output);
```

### Using with Microsoft.Extensions.Configuration

himl.net integrates seamlessly with the .NET configuration system:

```csharp
using himl.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add HIML configuration source using the complex example
builder.Configuration.AddHiml("examples/complex/env=dev/region=us-east-1/cluster=cluster1", options =>
{
    options.ListMergeStrategy = ListMergeStrategy.AppendUnique;
    options.DictMergeStrategy = DictMergeStrategy.Merge;
});

var host = builder.Build();

// Access configuration values from the complex example
var config = host.Services.GetRequiredService<IConfiguration>();
var environment = config["env"];              // "dev"
var region = config["region"];               // "us-east-1"
var cluster = config["cluster"];             // "cluster1"
var clusterName = config["cluster_info:name"];           // "default"
var nodeType = config["cluster_info:node_type"];         // "c3.2xlarge"
var cpuMetric = config["cluster_metrics:0:metric"];      // "cpu"
var cpuValue = config["cluster_metrics:0:value"];        // "90"
```

You can also chain multiple HIML sources with different merge strategies:

```csharp
builder.Configuration
    .AddHiml("examples/complex")  // Base configuration
    .AddHiml("examples/complex/env=dev/region=us-east-1/cluster=cluster1", options =>
    {
        options.SkipInterpolations = false;
    })
    .AddHiml("examples/complex/overrides", optional: true);  // Optional overrides
```

For full documentation and examples, see the repository README: https://github.com/aniongithub/himl.net

### Using the CLI

The `himl.cli` CLI tool provides exact parity with the original Adobe HIML `himl-config-merger` tool. It generates configuration files from hierarchical YAML.

Install the CLI tool:

```sh
dotnet tool install -g himl.cli
```

Basic usage:

```sh
himl.cli <path> --output-dir <output-dir> --levels <levels...> --leaf-directories <leaf-directories...>
```

Example:

```sh
himl.cli examples/complex --output-dir /tmp/output --levels env region cluster --leaf-directories cluster
```

For full documentation and examples, see the repository README: https://github.com/aniongithub/himl.net
