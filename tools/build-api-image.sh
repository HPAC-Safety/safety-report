#!/usr/bin/env bash
# Build the API's DEPLOYED container image: publish the API, then wrap the
# output in src/HpacSafety.Api/Dockerfile, which adds the AWS Lambda Web
# Adapter (ADR-0042). Same convention as tools/build-worker-image.sh
# (ADR-0118).
#
#   tools/build-api-image.sh <image>[:tag]
#
# NOT used by dev-up.sh: local development builds a plain Kestrel container
# with `dotnet publish /t:PublishContainer` instead — nothing local puts a
# Lambda Function URL event in front of it, so the adapter buys nothing there.
# This script is deploy-api.yml's, and CI's, for the image that actually runs
# on Lambda.
set -euo pipefail

if [ "$#" -ne 1 ]; then
	echo "usage: tools/build-api-image.sh <image>[:tag]" >&2
	exit 2
fi

IMAGE="$1"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUTPUT="$REPO_ROOT/artifacts/api"

rm -rf "$OUTPUT"

# Published for the Linux architecture the local Docker engine builds, same
# reasoning as build-worker-image.sh: x86_64 for the Lambda function
# (infra/lambda.tf pins architectures = ["x86_64"]), linux-arm64 on an
# Apple-silicon development machine building this image by hand.
case "$(docker info --format '{{.Architecture}}')" in
	x86_64 | amd64) RUNTIME=linux-x64 ;;
	aarch64 | arm64) RUNTIME=linux-arm64 ;;
	*)
		echo "error: no API runtime for this Docker engine's architecture." >&2
		exit 1
		;;
esac

dotnet publish "$REPO_ROOT/src/HpacSafety.Api/HpacSafety.Api.csproj" \
	--configuration Release \
	--runtime "$RUNTIME" \
	--self-contained false \
	--output "$OUTPUT" \
	-p:UseAppHost=false

docker build \
	--file "$REPO_ROOT/src/HpacSafety.Api/Dockerfile" \
	--tag "$IMAGE" \
	"$OUTPUT"
