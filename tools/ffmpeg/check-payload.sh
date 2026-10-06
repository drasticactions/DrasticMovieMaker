#!/usr/bin/env bash
# Checks a desktop FFmpeg payload (build-desktop.sh output) for dependencies the app cannot count on: every library may
# need only the other FFmpeg libraries and the OS's own system libraries.
#
#   linux-x64   DT_NEEDED within glibc (libc, libm, libpthread, libdl, the loader) and libva/libva-drm; no glibc
#               symbol newer than 2.35; the fallback stubs export every libva symbol the payload imports.
#   osx-arm64   otool -L lists only @rpath FFmpeg libraries and /usr/lib or /System/Library entries; no library needs
#               a macOS newer than 15.0.
#   win-x64     imports only Windows system DLLs (no MSYS2 or MinGW runtime DLL).
#
# Usage: tools/ffmpeg/check-payload.sh <target> <folder>
set -euo pipefail

TARGET="$1"
DIR="$2"
fail=0
bad() {
  echo "payload: $*" >&2
  fail=1
}

case "$TARGET" in
  linux-x64)
    allowed=" libc.so.6 libm.so.6 libpthread.so.0 libdl.so.2 ld-linux-x86-64.so.2 libva.so.2 libva-drm.so.2 \
libavutil.so.61 libswresample.so.7 libswscale.so.10 libavcodec.so.63 libavformat.so.63 "
    for lib in "$DIR"/lib*.so.*; do
      for need in $(readelf -d "$lib" | sed -n 's/.*(NEEDED).*\[\(.*\)\]/\1/p'); do
        [[ $allowed == *" $need "* ]] || bad "$(basename "$lib") needs $need"
      done
      newest=$(objdump -T "$lib" | grep -o 'GLIBC_[0-9.]*' | sort -uV | tail -1)
      if [[ -n $newest ]] && [[ $(printf '%s\n' "$newest" GLIBC_2.35 | sort -V | tail -1) != GLIBC_2.35 ]]; then
        bad "$(basename "$lib") needs $newest (newer than glibc 2.35)"
      fi
      [[ $(readelf -d "$lib" | sed -n 's/.*R\(UN\)\{0,1\}PATH.*\[\(.*\)\]/\2/p') == '$ORIGIN' ]] \
        || bad "$(basename "$lib") does not look for its siblings in \$ORIGIN"
    done
    # Every libva import (with its version) must be exported by the stub with the same soname.
    for stub in libva.so.2 libva-drm.so.2; do
      [[ -f $DIR/fallback/$stub ]] || { bad "fallback/$stub is missing"; fail=2; }
    done
    [[ $fail == 2 ]] && exit 1
    imports=$(for lib in "$DIR"/lib*.so.*; do nm -D --undefined-only "$lib" | awk '{print $2}'; done | grep '^va' | sort -u)
    core=" $(nm -D --defined-only "$DIR/fallback/libva.so.2" | awk '{print $3}' | sed 's/@@/@/' | tr '\n' ' ') "
    drm=" $(nm -D --defined-only "$DIR/fallback/libva-drm.so.2" | awk '{print $3}' | sed 's/@@/@/' | tr '\n' ' ') "
    for sym in $imports; do
      [[ $core == *" $sym "* || $drm == *" $sym "* ]] || bad "the libva stubs do not export $sym"
    done
    ;;
  osx-arm64)
    for lib in "$DIR"/lib*.dylib; do
      while read -r dep _; do
        case "$dep" in
          @rpath/libavutil.61.dylib | @rpath/libswresample.7.dylib | @rpath/libswscale.10.dylib | \
            @rpath/libavcodec.63.dylib | @rpath/libavformat.63.dylib | /usr/lib/* | /System/Library/*) ;;
          *) bad "$(basename "$lib") links $dep" ;;
        esac
      done < <(otool -L "$lib" | tail -n +2)
      minos=$(vtool -show-build "$lib" | awk '$1 == "minos" {print $2}')
      [[ $(printf '%s\n' "$minos" 15.0 | sort -V | tail -1) == 15.0 ]] || bad "$(basename "$lib") needs macOS $minos (newer than 15.0)"
    done
    ;;
  win-x64)
    for lib in "$DIR"/*.dll; do
      for dep in $(objdump -p "$lib" | sed -n 's/^\s*DLL Name: //p'); do
        case "${dep,,}" in
          libgcc* | libwinpthread* | libstdc++* | msys-* | libssp* | zlib1.dll | libiconv* | liblzma* | libbz2*)
            bad "$(basename "$lib") imports $dep" ;;
        esac
      done
    done
    ;;
  *)
    echo "Usage: $0 linux-x64|osx-arm64|win-x64 <folder>" >&2
    exit 2
    ;;
esac

if [ "$fail" != 0 ]; then
  echo "payload: $DIR has dependencies the app cannot rely on" >&2
  exit 1
fi
echo "payload: $DIR OK ($TARGET)"
