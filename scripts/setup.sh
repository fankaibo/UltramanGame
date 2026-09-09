#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
export UV_CACHE_DIR="$PWD/.cache/uv"
if ! command -v uv >/dev/null; then
  echo '需要 uv 管理项目 Python 环境。安装方法见 README。'
  exit 1
fi
uv venv --python 3.12 --allow-existing .venv
uv pip sync --python .venv/bin/python requirements.lock.txt
.venv/bin/python scripts/repair_mediapipe_metadata.py
uv pip check --python .venv/bin/python
.venv/bin/python scripts/download_model.py
echo '环境就绪。运行 ./scripts/camera.sh 启动前置摄像头。'
