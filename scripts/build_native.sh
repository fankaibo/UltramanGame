#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p unity/Assets/Plugins/macOS
# Link both plugins to a temporary directory first. A failed SDK/linker run
# must not truncate a working plugin in the Unity project.
native_stage=$(mktemp -d "${TMPDIR:-/tmp}/ultraman-native.XXXXXX")
trap 'rm -rf "$native_stage"' EXIT
xcrun clang++ -dynamiclib -fobjc-arc -framework Cocoa -arch arm64 -arch x86_64 \
  -mmacosx-version-min=11.0 -Wno-deprecated-declarations native/MusicPicker.mm \
  -o "$native_stage/libUltramanMusicPicker.dylib"
xcrun clang++ -dynamiclib -fobjc-arc -framework Foundation -framework Vision -framework CoreVideo \
  -arch arm64 -arch x86_64 -mmacosx-version-min=11.0 native/PersonCutout.mm \
  -o "$native_stage/libUltramanPhoto.dylib"
cp "$native_stage/libUltramanMusicPicker.dylib" unity/Assets/Plugins/macOS/libUltramanMusicPicker.dylib
cp "$native_stage/libUltramanPhoto.dylib" unity/Assets/Plugins/macOS/libUltramanPhoto.dylib
