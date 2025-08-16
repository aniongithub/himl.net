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

For full documentation and examples, see the repository README: https://github.com/aniongithub/himl.net

### Using the CLI

The `himl` CLI tool (distributed as the `himl.cli` package) implements the upstream `himl-config-merger` behavior — it scans a root configuration tree and writes one merged file per leaf into an output directory. Use the `--levels` option to define which path segments (e.g. `env region cluster`) define leaves.

Example:

```sh
himl examples/complex --output-dir merged_output --levels env region cluster
```

For full documentation and examples, see the repository README: https://github.com/aniongithub/himl.net
