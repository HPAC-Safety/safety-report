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

echo "Building the API container image"
dotnet publish src/HpacSafety.Api/HpacSafety.Api.csproj \
	--configuration Release \
	/t:PublishContainer \
	-p:ContainerRepository=hpacsafety-api \
	-p:ContainerImageTag=dev

echo "Starting containers"
# The web container runs Vite's own dev server (npm ci && npm run dev)
# against a bind-mounted repo, so edits on disk hot-reload in the browser
# without a rebuild or restart. postgres data and web's node_modules both
# survive restarts via named volumes.
#
# Detached on purpose: this script brings the environment up, waits until it
# is actually serving, and returns. Logs are `docker compose logs -f`.
docker compose up --build --detach

echo "Waiting for the API and the web dev server"

# The web container runs `npm ci` on a cold node_modules volume, which is the
# slow part of a first start.
WAITED=0
LIMIT=300

while [ "$WAITED" -lt "$LIMIT" ]; do
	API_UP=$(curl --silent --fail --max-time 2 http://localhost:8080/health >/dev/null 2>&1 && echo yes || echo no)
	WEB_UP=$(curl --silent --fail --max-time 2 http://localhost:5173/ >/dev/null 2>&1 && echo yes || echo no)

	if [ "$API_UP" = yes ] && [ "$WEB_UP" = yes ]; then
		echo
		echo "Web  http://localhost:5173"
		echo "API  http://localhost:8080"
		echo "Logs docker compose logs -f     Stop ./dev-up.sh --down"
		exit 0
	fi

	sleep 2
	WAITED=$((WAITED + 2))
done

echo "error: the containers did not come up within ${LIMIT}s. Check: docker compose logs" >&2
exit 1
