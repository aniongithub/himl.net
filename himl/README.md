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
