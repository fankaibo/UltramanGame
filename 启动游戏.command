#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
if [ ! -x .venv/bin/python ]; then
  echo "识别环境尚未准备好，请先在项目目录运行 ./scripts/setup.sh。"
  read -r -p "按回车关闭。" || true
  exit 1
fi
if ! .venv/bin/python scripts/launch_game.py "$@"; then
  read -r -p "启动失败，按回车关闭。" || true
  exit 1
fi
