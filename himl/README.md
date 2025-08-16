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
