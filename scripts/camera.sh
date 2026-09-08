#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
export MPLCONFIGDIR="$PWD/.cache/matplotlib"
if [ ! -x .venv/bin/python ]; then
  echo '请先运行 ./scripts/setup.sh'
  exit 1
fi
exec .venv/bin/python -m vision "$@"
