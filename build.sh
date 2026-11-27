#!/usr/bin/env bash
# Build + test in the official .NET SDK container (no local SDK needed).
#   ./build.sh          run tests, then publish dist/Zoe (Zoe.exe + sprites + zoe.json)
#   ./build.sh test     tests only
set -euo pipefail
cd "$(dirname "$0")"
run() {
  docker run --rm -v "$PWD":/src -v zoe-nuget:/root/.nuget -w /src \
    mcr.microsoft.com/dotnet/sdk:10.0 bash -c "$1; rc=\$?; chown -R $(id -u):$(id -g) /src; exit \$rc"
}
run "dotnet test tests/Zoe.Core.Tests --nologo -v q"
[[ "${1:-}" == test ]] && exit 0
rm -rf dist/Zoe
run "dotnet publish src/Zoe.App -c Release -o dist/Zoe --nologo -v q"
cp -r sprites zoe.json deploy/install.ps1 dist/Zoe/
rm -f dist/Zoe/*.pdb
ls -la dist/Zoe
