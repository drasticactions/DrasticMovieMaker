#!/usr/bin/env bash
# Builds FFmpeg and its codec libraries as the app's bundled shared libraries, from the pinned sources (versions.env)
# and components (components.sh).
#
#   tools/ffmpeg/build-desktop.sh linux-x64     in Ubuntu 22.04 (re-runs itself in tools/linux/container.sh)
#   tools/ffmpeg/build-desktop.sh osx-arm64     on macOS with the Xcode command line tools, meson, ninja, pkg-config
#   tools/ffmpeg/build-desktop.sh win-x64       in an MSYS2 UCRT64 shell (toolchain, nasm, make, pkgconf, meson)
#
#   FFMPEG_DESKTOP_WORK=/path ...   put sources and objects elsewhere (default work/<target>)
#   FFMPEG_DESKTOP_CLEAN=1 ...      start again
#
# x264, libvpx, Opus, dav1d and zlib (and libdrm on Linux) are linked statically into the FFmpeg libraries, so the
# payload is the five FFmpeg libraries, found by each other through the OS's relative lookup ($ORIGIN, @rpath, the
# same folder). Output (not committed) in out/<target>: the libraries, manifest.txt (enabled components),
# BUILDINFO.txt (source revisions and the configure line, for the GPL source offer) and, on Linux, fallback/ with the
# libva stubs (linux/libva-stub.c). check-payload.sh then fails on any system dependency beyond the allowed ones.
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=versions.env
. "$HERE/versions.env"

TARGET="${1:-}"
case "$TARGET" in
  linux-x64 | osx-arm64 | win-x64) ;;
  *)
    echo "Usage: $0 linux-x64|osx-arm64|win-x64" >&2
    exit 2
    ;;
esac

# The Linux libraries must not need a glibc newer than 2.35: they build in Ubuntu 22.04.
if [ "$TARGET" = linux-x64 ] && [ "${AMM_IN_CONTAINER:-0}" != 1 ] \
    && ! grep -qs '^VERSION_ID="22.04"' /etc/os-release; then
  exec "$HERE/../linux/container.sh" "$HERE/build-desktop.sh" "$@"
fi

WORK="${FFMPEG_DESKTOP_WORK:-$HERE/work/$TARGET}"
OUT="${FFMPEG_DESKTOP_OUT:-$HERE/out/$TARGET}"
JOBS="${JOBS:-$(nproc 2>/dev/null || sysctl -n hw.ncpu)}"

if [ "${FFMPEG_DESKTOP_CLEAN:-0}" = 1 ]; then
  rm -rf "$WORK" "$OUT"
fi
mkdir -p "$WORK/src" "$WORK/stamp" "$OUT"

PREFIX="$WORK/prefix"
mkdir -p "$PREFIX/lib/pkgconfig" "$PREFIX/include"
export PKG_CONFIG_PATH="$PREFIX/lib/pkgconfig"

# --- Target settings ------------------------------------------------------------------------------------------------
case "$TARGET" in
  linux-x64)
    FLAGS="-O2 -fPIC"
    VPX_TARGET=x86_64-linux-gcc
    ;;
  osx-arm64)
    export MACOSX_DEPLOYMENT_TARGET=15.0
    # Only our own prefix: Homebrew's .pc files (x264, opus, dav1d …) must not leak into the payload.
    export PKG_CONFIG_LIBDIR="$PREFIX/lib/pkgconfig"
    FLAGS="-O2 -fPIC -arch arm64 -mmacosx-version-min=15.0"
    VPX_TARGET=arm64-darwin24-gcc
    ;;
  win-x64)
    FLAGS="-O2"
    VPX_TARGET=x86_64-win64-gcc
    ;;
esac
export CFLAGS="$FLAGS -I$PREFIX/include"
export CXXFLAGS="$FLAGS -I$PREFIX/include"
export LDFLAGS="-L$PREFIX/lib"

# --- Helpers --------------------------------------------------------------------------------------------------------
done_step() { [ -f "$WORK/stamp/$1" ]; }
mark() { touch "$WORK/stamp/$1"; }

fetch_tar() { # name url tar-flag
  local dir="$WORK/src/$1"
  if [ ! -d "$dir" ]; then
    rm -rf "$dir.tmp" && mkdir -p "$dir.tmp"
    curl -fsSL --retry 3 --connect-timeout 30 --speed-time 60 --speed-limit 1000 "$2" | tar -x --strip-components=1 -C "$dir.tmp" -f - ${3:-}
    mv "$dir.tmp" "$dir"
  fi
}

meson_static() { # source-dir [options...]
  local src="$1"
  shift
  (cd "$src" && rm -rf build \
    && meson setup build --prefix="$PREFIX" --libdir=lib --default-library=static --buildtype=release \
         -Db_staticpic=true "$@" \
    && ninja -C build install)
}

# --- zlib -----------------------------------------------------------------------------------------------------------
if ! done_step zlib; then
  fetch_tar zlib "$ZLIB_URL" -z
  (cd "$WORK/src/zlib" && ./configure --static --prefix="$PREFIX" && make -j"$JOBS" install)
  mark zlib
fi

# --- x264 -----------------------------------------------------------------------------------------------------------
if ! done_step x264; then
  if [ ! -d "$WORK/src/x264" ]; then
    git clone --quiet "$X264_URL" "$WORK/src/x264"
  fi
  X264_THREADS=""
  [ "$TARGET" = win-x64 ] && X264_THREADS=--enable-win32thread
  (cd "$WORK/src/x264" && git reset --quiet --hard "$X264_COMMIT" \
    && ./configure --prefix="$PREFIX" --enable-static --enable-pic --disable-cli --disable-opencl \
         $X264_THREADS --extra-cflags="$FLAGS" \
    && make -j"$JOBS" install-lib-static)
  mark x264
fi

# --- libvpx ---------------------------------------------------------------------------------------------------------
# Built, then installed: $PREFIX/include is on CFLAGS, so headers a parallel "make install" is still copying (or a
# failed run left half copied) would shadow the source tree's own and fail the build.
if ! done_step libvpx; then
  fetch_tar libvpx "$LIBVPX_URL" -z
  rm -rf "$PREFIX/include/vpx"
  (cd "$WORK/src/libvpx" && rm -rf amm-build && mkdir -p amm-build && cd amm-build \
    && ../configure --prefix="$PREFIX" --target="$VPX_TARGET" --enable-static --disable-shared --enable-pic \
         --disable-install-bins --disable-examples --disable-tools --disable-docs --disable-unit-tests \
         --disable-dependency-tracking --enable-vp8 --enable-vp9 --enable-multithread \
    && make -j"$JOBS" && make install)
  mark libvpx
fi

# --- Opus -----------------------------------------------------------------------------------------------------------
if ! done_step opus; then
  fetch_tar opus "$OPUS_URL" -z
  (cd "$WORK/src/opus" && ./configure --prefix="$PREFIX" --enable-static --disable-shared --with-pic \
      --disable-doc --disable-extra-programs \
    && make -j"$JOBS" install)
  mark opus
fi

# --- dav1d (AV1 and AVIF decode) ------------------------------------------------------------------------------------
if ! done_step dav1d; then
  fetch_tar dav1d "$DAV1D_URL" -z
  meson_static "$WORK/src/dav1d" -Denable_tools=false -Denable_tests=false -Denable_examples=false
  mark dav1d
fi

# --- libdrm (Linux: VA-API frames mapped to DMA-BUF for the zero-copy preview) ----------------------------------------
if [ "$TARGET" = linux-x64 ] && ! done_step libdrm; then
  fetch_tar libdrm "$LIBDRM_URL" -J
  meson_static "$WORK/src/libdrm" -Dintel=disabled -Dradeon=disabled -Damdgpu=disabled -Dnouveau=disabled \
    -Dvmwgfx=disabled -Domap=disabled -Dexynos=disabled -Dfreedreno=disabled -Dtegra=disabled -Dvc4=disabled \
    -Detnaviv=disabled -Dcairo-tests=disabled -Dman-pages=disabled -Dvalgrind=disabled -Dudev=false -Dtests=false \
    -Dinstall-test-programs=false
  mark libdrm
fi

# --- FFmpeg ---------------------------------------------------------------------------------------------------------
# components.sh starts with --disable-everything, so the target's hardware pieces come after it.
COMPONENTS="$(sh "$HERE/components.sh")"
case "$TARGET" in
  linux-x64)
    # VA-API decode (the hwaccels of the enabled decoders) and the h264_vaapi encoder. libva is the system's (or the
    # fallback stubs); libdrm is linked in.
    HARDWARE="--enable-pthreads --enable-vaapi --enable-libdrm \
--enable-hwaccel=h264_vaapi,hevc_vaapi,mpeg2_vaapi,mpeg4_vaapi,vc1_vaapi,wmv3_vaapi,vp8_vaapi,vp9_vaapi,av1_vaapi \
--enable-encoder=h264_vaapi"
    LINK="-L$PREFIX/lib -Wl,--as-needed -Wl,-z,noexecstack"
    ;;
  osx-arm64)
    HARDWARE="--arch=arm64 --cc=clang --enable-pthreads --enable-videotoolbox \
--enable-hwaccel=h264_videotoolbox,hevc_videotoolbox,mpeg2_videotoolbox,mpeg4_videotoolbox,vp9_videotoolbox,av1_videotoolbox,prores_videotoolbox \
--enable-encoder=h264_videotoolbox --install-name-dir=@rpath"
    LINK="-L$PREFIX/lib -Wl,-rpath,@loader_path"
    ;;
  win-x64)
    HARDWARE="--target-os=mingw32 --arch=x86_64 --enable-w32threads --enable-d3d11va --enable-dxva2 \
--enable-mediafoundation \
--enable-hwaccel=h264_d3d11va,h264_d3d11va2,h264_dxva2,hevc_d3d11va,hevc_d3d11va2,hevc_dxva2,mpeg2_d3d11va,mpeg2_d3d11va2,mpeg2_dxva2,vc1_d3d11va,vc1_d3d11va2,vc1_dxva2,wmv3_d3d11va,wmv3_d3d11va2,wmv3_dxva2,vp9_d3d11va,vp9_d3d11va2,vp9_dxva2,av1_d3d11va,av1_d3d11va2,av1_dxva2 \
--enable-encoder=h264_mf"
    # -static: winpthreads (behind MinGW's clock_gettime and nanosleep, and libvpx's threads), libgcc and libstdc++
    # come from their archives, so the DLLs import only Windows' own.
    LINK="-L$PREFIX/lib -static"
    ;;
esac

FFMPEG_CONFIGURE="--prefix=$PREFIX --enable-shared --disable-static --enable-pic \
--disable-programs --disable-doc --disable-debug --disable-network --disable-autodetect --disable-avdevice \
--disable-avfilter --pkg-config=pkg-config --pkg-config-flags=--static \
--extra-cflags=-I$PREFIX/include --extra-ldflags=\"$LINK\" \
$COMPONENTS $HARDWARE"

if ! done_step ffmpeg; then
  fetch_tar ffmpeg "$FFMPEG_URL" -J
  # shellcheck disable=SC2086
  (cd "$WORK/src/ffmpeg" && eval ./configure $FFMPEG_CONFIGURE && make -j"$JOBS" && make install)
  mark ffmpeg
fi

# --- Output ---------------------------------------------------------------------------------------------------------
rm -rf "$OUT"
mkdir -p "$OUT"
for lib in avutil:61 swresample:7 swscale:10 avcodec:63 avformat:63; do
  name=${lib%:*}
  major=${lib#*:}
  case "$TARGET" in
    linux-x64)
      cp -L "$PREFIX/lib/lib$name.so.$major" "$OUT/"
      patchelf --set-rpath '$ORIGIN' "$OUT/lib$name.so.$major"
      ;;
    osx-arm64)
      cp "$PREFIX/lib/lib$name.$major.dylib" "$OUT/"
      ;;
    win-x64)
      cp "$PREFIX/bin/$name-$major.dll" "$OUT/"
      ;;
  esac
done

STUB_INFO=""
if [ "$TARGET" = linux-x64 ]; then
  # Stand-ins for libva.so.2 and libva-drm.so.2, which the libraries above need but the app does not ship. The app
  # loads them only when the system has no libva; every call then fails, and decode and encode use software.
  mkdir -p "$OUT/fallback"
  cc -O2 -fPIC -shared -DLIBVA_STUB_CORE -Wl,-soname,libva.so.2 -Wl,--version-script="$HERE/linux/libva-stub.map" \
    -o "$OUT/fallback/libva.so.2" "$HERE/linux/libva-stub.c"
  cc -O2 -fPIC -shared -DLIBVA_STUB_DRM -Wl,-soname,libva-drm.so.2 \
    -o "$OUT/fallback/libva-drm.so.2" "$HERE/linux/libva-stub.c"
  STUB_INFO="libva stubs: linux/libva-stub.c (built with $(cc --version | head -1))"
fi

# The components FFmpeg actually built, one "<kind> <name>" per line (configure resolves dependencies, so this can be
# more than components.sh asked for).
grep -h -E '^#define CONFIG_[A-Z0-9_]+_(DECODER|ENCODER|DEMUXER|MUXER|PARSER|PROTOCOL|BSF|HWACCEL) 1' \
    "$WORK/src/ffmpeg/config_components.h" \
  | sed -E 's/^#define CONFIG_([A-Z0-9_]+)_(DECODER|ENCODER|DEMUXER|MUXER|PARSER|PROTOCOL|BSF|HWACCEL) 1/\2 \1/' \
  | tr 'A-Z' 'a-z' | sort > "$OUT/manifest.txt"

{
  echo "target $TARGET"
  echo "compiler $(cc --version 2>/dev/null | head -1)"
  [ -f /etc/os-release ] && echo "system $(. /etc/os-release && echo "$PRETTY_NAME")"
  echo "ffmpeg $FFMPEG_VERSION $FFMPEG_URL"
  echo "x264 $X264_COMMIT $X264_URL"
  echo "libvpx $LIBVPX_VERSION $LIBVPX_URL"
  echo "opus $OPUS_VERSION $OPUS_URL"
  echo "dav1d $DAV1D_VERSION $DAV1D_URL"
  echo "zlib $ZLIB_VERSION $ZLIB_URL"
  [ "$TARGET" = linux-x64 ] && echo "libdrm $LIBDRM_VERSION $LIBDRM_URL"
  [ -n "$STUB_INFO" ] && echo "$STUB_INFO"
  [ "$TARGET" = linux-x64 ] && echo "libva headers: VA-API $(pkg-config --modversion libva) (the build system's; libva is not shipped)"
  echo "patches none"
  echo "cflags $FLAGS"
  echo "configure $FFMPEG_CONFIGURE"
} > "$OUT/BUILDINFO.txt"

"$HERE/check-payload.sh" "$TARGET" "$OUT"
ls -la "$OUT"
echo "FFmpeg for $TARGET in $OUT"
