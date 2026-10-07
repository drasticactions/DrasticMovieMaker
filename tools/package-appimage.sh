#!/usr/bin/env bash
# Packages the desktop Linux app (NativeAOT, linux-x64) as an AppImage: <out>/DrasticMovieMaker-<version>-x86_64.AppImage.
#
# Everything is built in Ubuntu 22.04 (tools/linux/container.sh; run directly inside it), so the package needs glibc
# 2.35 at most. It carries the bundled FFmpeg (tools/ffmpeg/out/linux-x64, built first when missing) with its libva
# stand-ins in usr/bin/fallback; libva itself and its drivers come from the system when it has them. The .desktop
# file lists the project types and video, audio and pictures, which take effect once the AppImage is integrated
# (appimaged, AppImageLauncher).
#
# appimagetool (pinned, checksum verified) is downloaded once into the cache folder; it runs without FUSE.
# With AMM_APPIMAGE_SIGN_KEY set (a gpg key id), appimagetool signs the image; without it the image is unsigned.
# Usage: tools/package-appimage.sh [output folder, default out/]
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
out=$(mkdir -p "${1:-$root/out}" && cd "${1:-$root/out}" && pwd)

if [[ ${AMM_IN_CONTAINER:-0} != 1 ]] && ! grep -qs '^VERSION_ID="22.04"' /etc/os-release; then
  exec "$root/tools/linux/container.sh" "$root/tools/package-appimage.sh" "$out"
fi

version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/Directory.Build.props" | head -n 1)

tool_version=1.9.1
tool_sha256=ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0
cache=${XDG_CACHE_HOME:-$HOME/.cache}/avamoviemaker
tool=$cache/appimagetool-$tool_version-x86_64.AppImage

if [[ ! -x $tool ]]; then
  mkdir -p "$cache"
  echo "Downloading appimagetool $tool_version"
  curl -fsSL -o "$tool.part" "https://github.com/AppImage/appimagetool/releases/download/$tool_version/appimagetool-x86_64.AppImage"
  echo "$tool_sha256  $tool.part" | sha256sum -c --quiet
  chmod +x "$tool.part"
  mv "$tool.part" "$tool"
fi

ffmpeg=$root/tools/ffmpeg/out/linux-x64
if [[ ! -f $ffmpeg/libavformat.so.63 ]]; then
  "$root/tools/ffmpeg/build-desktop.sh" linux-x64
fi
"$root/tools/ffmpeg/check-payload.sh" linux-x64 "$ffmpeg"

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# Separate build folders, so the container's restore does not mix with the host's obj/ and bin/.
dotnet publish "$root/src/AvaMovieMaker" -c Release -r linux-x64 -o "$work/publish" -warnaserror \
  --artifacts-path "$work/artifacts" -p:AmmFFmpegDir="$ffmpeg/"

appdir=$work/DrasticMovieMaker.AppDir
bin=$appdir/usr/bin
share=$appdir/usr/share
doc=$share/doc/drasticmoviemaker
mkdir -p "$bin/fallback" "$share/applications" "$share/icons/hicolor/512x512/apps" \
  "$share/mime/packages" "$doc"

# The program and the native libraries it loads from its own folder (symbols and .pdb files stay out).
cp "$work/publish/DrasticMovieMaker" "$work/publish/"*.so "$work/publish/"*.so.* "$bin/"
cp "$work/publish/fallback/"* "$bin/fallback/"

# Nothing in the package may need a glibc newer than the baseline.
newest=$(for f in "$bin"/DrasticMovieMaker "$bin"/*.so* "$bin"/fallback/*; do objdump -T "$f"; done \
  | grep -o 'GLIBC_[0-9.]*' | sort -uV | tail -1)
if [[ $(printf '%s\n' "$newest" GLIBC_2.35 | sort -V | tail -1) != GLIBC_2.35 ]]; then
  echo "The package needs $newest, newer than glibc 2.35." >&2
  exit 1
fi
echo "Newest glibc symbol: $newest"

icons=$root/tools/icons
cp "$icons/icon-512.png" "$share/icons/hicolor/512x512/apps/com.drasticactions.moviemaker.png"
cp "$icons/icon-512.png" "$appdir/com.drasticactions.moviemaker.png"

# Licenses, the FFmpeg build's configure line and sources, and the exact revision (the GPL source offer).
cp "$root/LICENSE.md" "$root/THIRD-PARTY-NOTICES.md" "$doc/"
cp "$ffmpeg/BUILDINFO.txt" "$doc/FFMPEG-BUILDINFO.txt"
revision=${GITHUB_SHA:-$(git -C "$root" rev-parse HEAD 2>/dev/null || echo unknown)}
dirty=$([[ -z ${GITHUB_SHA:-} ]] && [[ -n $(git -C "$root" status --porcelain 2>/dev/null) ]] && echo " (with local changes)" || true)
{
  echo "Drastic Movie Maker $version, licensed under the GNU GPL version 3 or later (LICENSE.md)."
  echo "Built from revision $revision$dirty${GITHUB_REPOSITORY:+ of ${GITHUB_SERVER_URL}/${GITHUB_REPOSITORY}}."
  echo "FFmpeg build (pinned sources, configure options): tools/ffmpeg (build-desktop.sh) in that revision;"
  echo "its configure line and source revisions are in FFMPEG-BUILDINFO.txt."
  echo "Third-party components and their licenses: THIRD-PARTY-NOTICES.md."
} > "$doc/SOURCE.txt"

# The project and package types (shared-mime-info), so files open in the app once it is integrated.
cat > "$share/mime/packages/com.drasticactions.moviemaker.xml" <<'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<mime-info xmlns="http://www.freedesktop.org/standards/shared-mime-info">
  <mime-type type="application/x-drasticmoviemaker-project">
    <comment>Drastic Movie Maker project</comment>
    <sub-class-of type="application/json"/>
    <generic-icon name="com.drasticactions.moviemaker"/>
    <glob pattern="*.dmmproj"/>
  </mime-type>
  <mime-type type="application/x-drasticmoviemaker-package">
    <comment>Drastic Movie Maker project package</comment>
    <sub-class-of type="application/zip"/>
    <generic-icon name="com.drasticactions.moviemaker"/>
    <glob pattern="*.dmmpkg"/>
  </mime-type>
</mime-info>
EOF

cat > "$share/applications/com.drasticactions.moviemaker.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Drastic Movie Maker
Comment=Make movies from your videos, pictures and music
Exec=DrasticMovieMaker %F
Icon=com.drasticactions.moviemaker
Categories=AudioVideo;Video;AudioVideoEditing;
MimeType=application/x-drasticmoviemaker-project;application/x-drasticmoviemaker-package;video/*;audio/*;image/*;
StartupWMClass=DrasticMovieMaker
Terminal=false
EOF
cp "$share/applications/com.drasticactions.moviemaker.desktop" "$appdir/"

# .NET needs the system's ICU for culture data; where there is none (minimal systems), the app runs with invariant
# culture data instead of not starting.
cat > "$appdir/AppRun" <<'EOF'
#!/bin/sh
here=$(dirname "$(readlink -f "$0")")
if [ -z "${DOTNET_SYSTEM_GLOBALIZATION_INVARIANT:-}" ] \
    && ! { /sbin/ldconfig -p 2>/dev/null || ldconfig -p 2>/dev/null; } | grep -q 'libicuuc\.so\.[0-9]'; then
  export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
fi
exec "$here/usr/bin/DrasticMovieMaker" "$@"
EOF
chmod +x "$appdir/AppRun"

sign=()
[[ -n ${AMM_APPIMAGE_SIGN_KEY:-} ]] && sign=(--sign --sign-key "$AMM_APPIMAGE_SIGN_KEY")
image=$out/DrasticMovieMaker-$version-x86_64.AppImage
ARCH=x86_64 APPIMAGE_EXTRACT_AND_RUN=1 "$tool" --no-appstream "${sign[@]}" "$appdir" "$image"
echo "$image"
