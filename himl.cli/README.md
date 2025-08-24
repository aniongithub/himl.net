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

## Documentation

For full documentation and examples, see the repository README: https://github.com/aniongithub/himl.net
