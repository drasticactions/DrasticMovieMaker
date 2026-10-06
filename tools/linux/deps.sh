#!/bin/sh
# Installs what the Linux builds need on Ubuntu 22.04 (glibc 2.35, the oldest system the app supports): the FFmpeg
# recipe's toolchain (tools/ffmpeg/build-desktop.sh linux-x64), the NativeAOT toolchain and, with --dotnet, the .NET
# SDK that global.json names. The build image (Containerfile) runs this; so do the workflows' ubuntu:22.04 jobs.
# Usage: tools/linux/deps.sh [--dotnet <global.json>]
set -eu

export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y --no-install-recommends \
  build-essential nasm pkg-config meson ninja-build git curl ca-certificates xz-utils file patchelf python3 \
  libva-dev clang zlib1g-dev libicu70 gnupg
rm -rf /var/lib/apt/lists/*

if [ "${1:-}" = --dotnet ]; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --jsonfile "$2" --install-dir /usr/share/dotnet
  ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet
  rm /tmp/dotnet-install.sh
fi
