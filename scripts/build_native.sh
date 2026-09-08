#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p unity/Assets/Plugins/macOS
xcrun clang++ -dynamiclib -fobjc-arc -framework Cocoa -arch arm64 -arch x86_64 \
  -mmacosx-version-min=11.0 -Wno-deprecated-declarations native/MusicPicker.mm \
  -o unity/Assets/Plugins/macOS/libUltramanMusicPicker.dylib
