#!/usr/bin/env bash
# Build the Worker's container image (ADR-0118): publish the Worker, then wrap
# the output in src/HpacSafety.Worker/Dockerfile, which adds Ubuntu's ffmpeg.
#
#   tools/build-worker-image.sh <image>[:tag]
#
# The one way the image is built — by dev-up.sh, CI, and deploy-worker.yml — so
# the three cannot drift apart. The build context is the publish output alone.
set -euo pipefail

if [ "$#" -ne 1 ]; then
	echo "usage: tools/build-worker-image.sh <image>[:tag]" >&2
	exit 2
fi

IMAGE="$1"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUTPUT="$REPO_ROOT/artifacts/worker"

rm -rf "$OUTPUT"

# Published for the Linux architecture the local Docker engine builds, which is
# x86_64 for the Fargate task and arm64 on an Apple-silicon development machine.
# Naming one runtime keeps only its native libraries; a portable publish carries
# Windows, macOS, and musl builds of every native dependency (~190 MB) that the
# image can never load. Framework-dependent, started by `dotnet`, so no app host.
case "$(docker info --format '{{.Architecture}}')" in
	x86_64 | amd64) RUNTIME=linux-x64 ;;
	aarch64 | arm64) RUNTIME=linux-arm64 ;;
	*)
		echo "error: no Worker runtime for this Docker engine's architecture." >&2
		exit 1
		;;
esac

dotnet publish "$REPO_ROOT/src/HpacSafety.Worker/HpacSafety.Worker.csproj" \
	--configuration Release \
	--runtime "$RUNTIME" \
	--self-contained false \
	--output "$OUTPUT" \
	-p:UseAppHost=false

docker build \
	--file "$REPO_ROOT/src/HpacSafety.Worker/Dockerfile" \
	--tag "$IMAGE" \
	"$OUTPUT"
