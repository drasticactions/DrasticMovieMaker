#!/usr/bin/env bash
# Runs a command in the Linux build image (Ubuntu 22.04, see Containerfile), so what it builds needs glibc 2.35 at
# most. The repository is mounted at its own path and the command runs in the current folder; NuGet packages and
# downloaded tools (~/.cache/avamoviemaker) are cached on the host. Uses podman, else docker. The image is built on
# first use and again when Containerfile, deps.sh or global.json change.
# Usage: tools/linux/container.sh <command> [args...]
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
root=$(cd "$here/../.." && pwd)

engine=${AMM_CONTAINER_ENGINE:-$(command -v podman || command -v docker || true)}
if [[ -z $engine ]]; then
  echo "Neither podman nor docker was found; install one, or run on Ubuntu 22.04 with tools/linux/deps.sh." >&2
  exit 1
fi

tag=$(cat "$here/Containerfile" "$here/deps.sh" "$root/global.json" | sha256sum | cut -c1-12)
image=localhost/avamoviemaker-linux-build:$tag
if ! "$engine" image exists "$image" 2>/dev/null && ! "$engine" image inspect "$image" >/dev/null 2>&1; then
  context=$(mktemp -d)
  trap 'rm -rf "$context"' EXIT
  cp "$here/Containerfile" "$here/deps.sh" "$root/global.json" "$context/"
  "$engine" build -t "$image" -f "$context/Containerfile" "$context"
fi

nuget=${NUGET_PACKAGES:-$HOME/.nuget/packages}
cache=${XDG_CACHE_HOME:-$HOME/.cache}/avamoviemaker
mkdir -p "$nuget" "$cache"
args=(--rm -v "$root:$root" -v "$nuget:/nuget" -e NUGET_PACKAGES=/nuget -v "$cache:/tmp/.cache/avamoviemaker"
  -e AMM_IN_CONTAINER=1 -w "$PWD")
# Signing the AppImage uses the caller's gpg keys.
if [[ -n ${AMM_APPIMAGE_SIGN_KEY:-} ]]; then
  args+=(-v "${GNUPGHOME:-$HOME/.gnupg}:/tmp/.gnupg" -e GNUPGHOME=/tmp/.gnupg -e "AMM_APPIMAGE_SIGN_KEY=$AMM_APPIMAGE_SIGN_KEY")
fi
if [[ $(basename "$engine") == docker ]]; then
  # Docker runs as root inside; run as the caller so the files it writes stay theirs.
  args+=(--user "$(id -u):$(id -g)" -e HOME=/tmp)
else
  args+=(--userns=keep-id -e HOME=/tmp)
fi
[[ -t 0 && -t 1 ]] && args+=(-it)
for var in JOBS GITHUB_SHA GITHUB_SERVER_URL GITHUB_REPOSITORY; do
  [[ -n ${!var:-} ]] && args+=(-e "$var=${!var}")
done
exec "$engine" run "${args[@]}" "$image" "$@"
