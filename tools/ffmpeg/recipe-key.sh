#!/usr/bin/env bash
# Prints the key of the desktop FFmpeg recipe: a hash of the committed files that decide what build-desktop.sh builds.
# ffmpeg-desktop.yml names its cache and its "ffmpeg-<rid>-<key>" artifacts with it, and the package workflows look the
# artifacts up by it, so a package always gets FFmpeg built from the recipe it was built with.
# Usage: tools/ffmpeg/recipe-key.sh
set -euo pipefail
cd "$(dirname "$0")/../.."
git ls-files -s tools/ffmpeg/versions.env tools/ffmpeg/components.sh tools/ffmpeg/build-desktop.sh \
  tools/ffmpeg/check-payload.sh tools/ffmpeg/linux tools/linux/deps.sh | git hash-object --stdin | cut -c1-16
