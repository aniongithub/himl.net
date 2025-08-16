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

Basic invocation:

```bash
himl examples/complex/env=dev/region=us-east-1/cluster=cluster2 --format json --filter Database --exclude Secrets
```

For full documentation and examples, see the repository README: https://github.com/aniongithub/himl.net
