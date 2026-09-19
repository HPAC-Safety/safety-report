#!/usr/bin/env sh
#
# dev-up.sh — build and run the dev environment: Postgres, API, and web,
# each in its own Docker container (docker-compose.yml).
#
# Run ./init-dev.sh first. This script assumes Docker and the .NET SDK are
# already installed and does not check for them beyond confirming Docker is
# running.
#
#     ./dev-up.sh          build and start everything
#     ./dev-up.sh --down   stop and remove the containers

set -eu

REPO_ROOT=$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)
cd "$REPO_ROOT"

if [ "${1-}" = "--down" ]; then
	docker compose down
	exit 0
fi

if ! docker info >/dev/null 2>&1; then
	echo "error: Docker is not running. Start Docker Desktop, or run: sudo systemctl start docker (Linux)." >&2
	exit 1
fi

echo "Building the web site (Vite)"
( cd src/web && npm ci && npm run build )

echo "Building the API container image"
dotnet publish src/HpacSafety.Api/HpacSafety.Api.csproj \
	--configuration Release \
	/t:PublishContainer \
	-p:ContainerRepository=hpacsafety-api \
	-p:ContainerImageTag=dev

echo "Starting containers"
# --force-recreate: `npm run build` above deletes and recreates src/web/dist
# (Vite's emptyOutDir), and the web container bind-mounts that directory
# read-only. Docker Desktop's bind mount can go stale across that
# delete+recreate, and since docker-compose.yml itself never changes, a
# plain `up` reuses the existing containers rather than remounting — the
# result is nginx serving an empty directory listing, "403 Forbidden", even
# though dist/index.html is right there on disk. postgres data survives
# recreation (named volume); this just guarantees a fresh mount every run.
docker compose up --build --force-recreate
