#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
game_version=$(awk '/^m_EditorVersion:/ {print $2}' unity/ProjectSettings/ProjectVersion.txt)
game_editor=${UNITY_EDITOR:-"/Applications/Unity/Hub/Editor/$game_version/Unity.app/Contents/MacOS/Unity"}
if [ ! -x "$game_editor" ]; then
  echo "未找到 Unity $game_version。可用 UNITY_EDITOR 指定编辑器可执行文件。" >&2
  exit 1
fi
mkdir -p logs
game_python=python3
if [ -x .venv/bin/python ]; then game_python=.venv/bin/python; fi
"$game_python" scripts/generate_voice.py
"$game_editor" -batchmode -projectPath "$PWD/unity" \
  -executeMethod UltramanGame.Editor.ProjectSetup.BuildMac -quit -logFile "$PWD/logs/unity-build.log"
echo "构建完成：unity/Builds/TigaTraining.app"
