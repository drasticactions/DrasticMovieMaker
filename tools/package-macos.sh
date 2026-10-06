#!/usr/bin/env bash
# Packages the desktop macOS app (NativeAOT, osx-arm64, macOS 15+) as AvaMovieMaker.app in
# <out>/AvaMovieMaker-<version>-arm64.dmg. Run on an Apple silicon Mac with the .NET SDK and the Xcode command line
# tools. The bundled FFmpeg comes from tools/ffmpeg/out/osx-arm64 (built first when missing) and goes to
# Contents/Frameworks.
#
# Signing hooks, unsigned by default:
#   AMM_MACOS_SIGN_IDENTITY  a "Developer ID Application: ..." identity: every dylib, then the app, is signed with the
#                            hardened runtime and macos/AvaMovieMaker.entitlements.
#   AMM_NOTARY_PROFILE       a notarytool keychain profile (xcrun notarytool store-credentials): the dmg is notarized
#                            and stapled. Needs AMM_MACOS_SIGN_IDENTITY.
# Unsigned, the app opens the first time with right-click > Open.
# Usage: tools/package-macos.sh [output folder, default out/]
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
out=$(mkdir -p "${1:-$root/out}" && cd "${1:-$root/out}" && pwd)
version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/Directory.Build.props" | head -n 1)

ffmpeg=$root/tools/ffmpeg/out/osx-arm64
if [[ ! -f $ffmpeg/libavformat.63.dylib ]]; then
  "$root/tools/ffmpeg/build-desktop.sh" osx-arm64
fi
"$root/tools/ffmpeg/check-payload.sh" osx-arm64 "$ffmpeg"

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# FFmpeg goes to Frameworks below, not next to the executable, so the publish leaves it out.
dotnet publish "$root/src/AvaMovieMaker" -c Release -r osx-arm64 -o "$work/publish" -warnaserror \
  -p:AmmFFmpegDir="$work/no-ffmpeg/"

app=$work/AvaMovieMaker.app
contents=$app/Contents
mkdir -p "$contents/MacOS" "$contents/Frameworks" "$contents/Resources"

# The program and the native libraries it loads from its own folder (symbols and .pdb files stay out).
cp "$work/publish/AvaMovieMaker" "$work/publish/"*.dylib "$contents/MacOS/"
cp "$ffmpeg/"*.dylib "$contents/Frameworks/"
install_name_tool -add_rpath "@executable_path/../Frameworks" "$contents/MacOS/AvaMovieMaker"

# Every native library must run on the minimum macOS (Info.plist's LSMinimumSystemVersion), and the Linux GPU paths
# (EGL, Vulkan) must not be imported: the trimmer drops them on macOS.
for bin in "$contents/MacOS/"* "$contents/Frameworks/"*.dylib; do
  minos=$(vtool -show-build "$bin" | awk '$1 == "minos" {print $2}')
  if [[ $(printf '%s\n' "$minos" 15.0 | sort -V | tail -1) != 15.0 ]]; then
    echo "$(basename "$bin") needs macOS $minos, newer than the app's minimum (15.0)." >&2
    exit 1
  fi
done
if strings "$contents/MacOS/AvaMovieMaker" | grep -E '^lib(EGL|vulkan)\.so'; then
  echo "The macOS executable still imports a Linux GPU library." >&2
  exit 1
fi

sed "s/@VERSION@/$version/g" "$root/tools/macos/Info.plist.in" > "$contents/Info.plist"
plutil -lint "$contents/Info.plist"

# The icon, from the 512 px PNG (sips scales it; iconutil packs the set).
iconset=$work/AppIcon.iconset
mkdir -p "$iconset"
png=$root/tools/icons/icon-512.png
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" "$png" --out "$iconset/icon_${size}x${size}.png" >/dev/null
  double=$((size * 2))
  [[ $double -le 512 ]] && sips -z "$double" "$double" "$png" --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
done
cp "$png" "$iconset/icon_512x512@2x.png"
iconutil -c icns "$iconset" -o "$contents/Resources/AppIcon.icns"

# Licenses, the FFmpeg build's configure line and sources, and the exact revision (the GPL source offer).
cp "$root/LICENSE.md" "$root/THIRD-PARTY-NOTICES.md" "$contents/Resources/"
cp "$ffmpeg/BUILDINFO.txt" "$contents/Resources/FFMPEG-BUILDINFO.txt"
revision=${GITHUB_SHA:-$(git -C "$root" rev-parse HEAD 2>/dev/null || echo unknown)}
{
  echo "AvaMovieMaker $version, licensed under the GNU GPL version 3 or later (LICENSE.md)."
  echo "Built from revision $revision${GITHUB_REPOSITORY:+ of ${GITHUB_SERVER_URL}/${GITHUB_REPOSITORY}}."
  echo "FFmpeg build (pinned sources, configure options): tools/ffmpeg (build-desktop.sh) in that revision;"
  echo "its configure line and source revisions are in FFMPEG-BUILDINFO.txt."
  echo "Third-party components and their licenses: THIRD-PARTY-NOTICES.md."
} > "$contents/Resources/SOURCE.txt"

if [[ -n ${AMM_MACOS_SIGN_IDENTITY:-} ]]; then
  sign=(codesign --force --options runtime --timestamp --sign "$AMM_MACOS_SIGN_IDENTITY")
  for lib in "$contents/Frameworks/"*.dylib "$contents/MacOS/"*.dylib; do
    "${sign[@]}" "$lib"
  done
  "${sign[@]}" --entitlements "$root/tools/macos/AvaMovieMaker.entitlements" "$app"
  codesign --verify --deep --strict "$app"
else
  # Apple silicon runs no code without a signature, and install_name_tool above broke the linker's one: sign ad hoc
  # (no identity), which Gatekeeper still treats as unsigned.
  codesign --force --sign - "$contents/Frameworks/"*.dylib "$contents/MacOS/"*.dylib
  codesign --force --sign - "$app"
  codesign --verify --deep --strict "$app"
  echo "AMM_MACOS_SIGN_IDENTITY is not set: the app is unsigned (ad hoc signature)."
fi

stage=$work/dmg
mkdir -p "$stage"
cp -R "$app" "$stage/"
ln -s /Applications "$stage/Applications"
dmg=$out/AvaMovieMaker-$version-arm64.dmg
rm -f "$dmg"
hdiutil create -volname AvaMovieMaker -srcfolder "$stage" -format UDZO -ov "$dmg"

if [[ -n ${AMM_NOTARY_PROFILE:-} ]]; then
  [[ -n ${AMM_MACOS_SIGN_IDENTITY:-} ]] || { echo "Notarizing needs AMM_MACOS_SIGN_IDENTITY." >&2; exit 1; }
  codesign --force --timestamp --sign "$AMM_MACOS_SIGN_IDENTITY" "$dmg"
  xcrun notarytool submit "$dmg" --keychain-profile "$AMM_NOTARY_PROFILE" --wait
  xcrun stapler staple "$dmg"
fi
echo "$dmg"
