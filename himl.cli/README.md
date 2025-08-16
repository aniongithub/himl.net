# himl.cli

Command-line interface for himl.net. This package is published as a .NET tool.

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

The CLI scans a HIML configuration tree and writes one merged file per leaf into an output directory.

Example:

```bash
dotnet himl.cli examples/complex --output-dir merged_output --levels env region cluster
```

Install as a .NET tool:

```bash
dotnet tool install -g himl.cli
```

Or install from a local nupkg folder (useful for testing):

```bash
dotnet tool install --global --add-source ./nupkg himl.cli --version 1.0.0
```

For full documentation and examples, see the repository README: https://github.com/aniongithub/himl.net
