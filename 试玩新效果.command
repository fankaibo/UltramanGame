#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
exec ./启动游戏.command --app "$PWD/unity/Builds/TigaTraining-candidate.app" "$@"
