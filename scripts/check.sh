#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_CLI_HOME="$PWD/.cache/dotnet"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
if [ -x .venv/bin/python ]; then
  game_python=.venv/bin/python
else
  game_python=python3
fi
"$game_python" -m unittest discover -s tests -v
dotnet run --project tests/CoreChecks --configuration Release
"$game_python" scripts/integration_check.py
