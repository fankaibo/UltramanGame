#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
game_version=$(awk '/^m_EditorVersion:/ {print $2}' unity/ProjectSettings/ProjectVersion.txt)
game_editor=${UNITY_EDITOR:-"/Applications/Unity/Hub/Editor/$game_version/Unity.app/Contents/MacOS/Unity"}
game_run=$(date -u +%Y%m%dT%H%M%SZ)
game_output="$PWD/artifacts/guard-impact-$game_run"
mkdir -p "$game_output" logs

"$game_editor" -batchmode -projectPath "$PWD/unity" \
  -executeMethod UltramanGame.Editor.GuardImpactReview.Render \
  --guard-output "$game_output/roster" -quit -logFile "$game_output/roster.log"
"$game_editor" -batchmode -projectPath "$PWD/unity" \
  -executeMethod UltramanGame.Editor.ExchangeReview.After \
  --exchange-output "$game_output/exchange" -quit -logFile "$game_output/exchange.log"
bash scripts/check.sh > "$game_output/checks.log" 2>&1
bash scripts/build_macos.sh > "$game_output/build.log" 2>&1
.venv/bin/python scripts/cinematic_player_check.py > "$game_output/player-check.log" 2>&1
.venv/bin/python scripts/guided_player_check.py --output "$game_output/guided" \
  --log "$game_output/guided-player.log" > "$game_output/guided-check.log" 2>&1
printf '防御演出验证完成，记录：%s\n' "$game_output"
