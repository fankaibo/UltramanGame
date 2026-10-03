#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
build_mode="development"
if [ "${1:-}" = "--release" ]; then
  build_mode="release"
elif [ "${1:-}" != "" ]; then
  echo "用法：bash scripts/build_macos.sh [--release]" >&2
  exit 2
fi
if [ "$build_mode" = "release" ]; then
  export ULTRAMAN_RELEASE_BUILD=1
else
  unset ULTRAMAN_RELEASE_BUILD || true
fi
game_version=$(awk '/^m_EditorVersion:/ {print $2}' unity/ProjectSettings/ProjectVersion.txt)
game_editor=${UNITY_EDITOR:-"/Applications/Unity/Hub/Editor/$game_version/Unity.app/Contents/MacOS/Unity"}
if [ ! -x "$game_editor" ]; then
  echo "未找到 Unity $game_version。可用 UNITY_EDITOR 指定编辑器可执行文件。" >&2
  exit 1
fi
mkdir -p logs
bash scripts/build_native.sh
game_python=python3
if [ -x .venv/bin/python ]; then game_python=.venv/bin/python; fi
"$game_python" scripts/generate_voice.py
# Keep an interrupted/licensing-stalled Unity invocation from surviving the
# wrapper and being mistaken for the game's memory use on the next run.
unity_pid=""
cleanup_unity() {
  if [ -n "$unity_pid" ] && kill -0 "$unity_pid" 2>/dev/null; then
    kill -TERM "$unity_pid" 2>/dev/null || true
  fi
}
trap cleanup_unity INT TERM EXIT
"$game_editor" -batchmode -projectPath "$PWD/unity" \
  -executeMethod UltramanGame.Editor.ProjectSetup.BuildMac -quit -logFile "$PWD/logs/unity-build.log" &
unity_pid=$!
set +e
wait "$unity_pid"
unity_rc=$?
set -e
unity_pid=""
if [ "$unity_rc" -ne 0 ]; then exit "$unity_rc"; fi
"$game_python" scripts/sign_macos.py
echo "构建完成：unity/Builds/TigaTraining.app"
echo "构建模式：$build_mode"
