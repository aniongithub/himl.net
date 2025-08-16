#!/usr/bin/env bash
set -euo pipefail

# Simple packaging script for creating .nupkg files for the solution projects.
# Usage:
#   ./scripts/pack.sh            # pack using no explicit version
#   VERSION=1.2.3 ./scripts/pack.sh

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
OUTPUT_DIR="$ROOT_DIR/nupkg"
CONFIGURATION="Release"

VERSION=${VERSION:-}

echo "Root: $ROOT_DIR"
echo "Output: $OUTPUT_DIR"

mkdir -p "$OUTPUT_DIR"

pushd "$ROOT_DIR"

# restore & build first
dotnet restore
if [ -n "$VERSION" ]; then
  dotnet build -c "$CONFIGURATION" /p:Version="$VERSION"
else
  dotnet build -c "$CONFIGURATION"
fi

projects=(
  "himl/himl.csproj"
  "himl.core/himl.core.csproj"
  "himl.cli/himl.cli.csproj"
)

for proj in "${projects[@]}"; do
  echo "Packing $proj..."
  if [ -n "$VERSION" ]; then
    dotnet pack "$proj" -c "$CONFIGURATION" -o "$OUTPUT_DIR" /p:PackageVersion="$VERSION" --no-build
  else
    dotnet pack "$proj" -c "$CONFIGURATION" -o "$OUTPUT_DIR" --no-build
  fi
done

popd

echo "Packaging complete. Packages placed in: $OUTPUT_DIR"
