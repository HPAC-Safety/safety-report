#!/usr/bin/env sh
#
# dev-up.sh — build and run the dev environment: Postgres, an S3-compatible
# server (private attachment storage), API, Worker, and web, each in its own
# Docker container (docker-compose.yml).
#
# Run ./init-dev.sh first. This script assumes Docker and the .NET SDK are
# already installed and does not check for them beyond confirming Docker is
# running.
#
# Every checkout of this repository — the primary one and each worktree under
# .claude/worktrees/ — is its own compose project, named after its directory,
# and they all publish the same host ports. Only one can serve at a time, so
# this script takes the ports over: containers another checkout left running
# are stopped (their volumes are kept), and a port held by anything that is
# not this repository stops the script with the holder's name.
#
#     ./dev-up.sh          build and start everything
#     ./dev-up.sh --down   stop and remove the containers
#
# Provider keys live in the primary checkout's .env (gitignored), written once:
#
#     DEEPL_API_KEY=...    answer and question translation (ADR-0109)
#     GEMINI_API_KEY=...   report summaries (ADR-0104)
#
# Compose only reads a .env beside the compose file, and every worktree is its
# own directory, so this script passes the primary checkout's file explicitly.
# Without the keys, summaries and translations fail after their retries.

set -eu

REPO_ROOT=$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd -P)
cd "$REPO_ROOT"

# Host ports docker-compose.yml publishes: postgres, s3 (API, console),
# api, web.
PORTS="5432 9000 9001 8080 5173"

# The .git directory every checkout of this repository shares.
common_git_dir() {
	(cd -- "$1" 2>/dev/null && git rev-parse --path-format=absolute --git-common-dir 2>/dev/null) || true
}

COMMON_GIT_DIR=$(common_git_dir "$REPO_ROOT")
MAIN_ROOT=$(dirname -- "$COMMON_GIT_DIR")

# Whether a compose working directory is a checkout of this repository. A
# worktree that has since been removed no longer exists on disk, so its path
# is the only evidence left.
is_this_repository() {
	case "$1" in
	"$MAIN_ROOT" | "$MAIN_ROOT"/.claude/worktrees/*) return 0 ;;
	esac
	[ -n "$COMMON_GIT_DIR" ] && [ "$(common_git_dir "$1")" = "$COMMON_GIT_DIR" ]
}

label() {
	docker inspect --format "{{index .Config.Labels \"$2\"}}" "$1"
}

# Stops whatever another checkout of this repository has running on the dev
# ports, and refuses to touch anything else. Agents bring the environment up
# from a worktree and remove the worktree afterwards, so the usual holder is a
# project whose checkout no longer exists.
reclaim_ports() {
	for PORT in $PORTS; do
		for ID in $(docker ps --quiet --filter "publish=$PORT"); do
			PROJECT=$(label "$ID" com.docker.compose.project)
			WORKING_DIR=$(label "$ID" com.docker.compose.project.working_dir)

			if [ "$WORKING_DIR" = "$REPO_ROOT" ]; then
				continue
			fi

			if [ -z "$PROJECT" ] || ! is_this_repository "$WORKING_DIR"; then
				NAME=$(docker inspect --format '{{.Name}}' "$ID")
				echo "error: port $PORT is held by container ${NAME#/}${WORKING_DIR:+ (from $WORKING_DIR)}, which is not a checkout of this repository. Stop it and re-run." >&2
				exit 1
			fi

			if [ -d "$WORKING_DIR" ]; then
				echo "Stopping $PROJECT, running from $WORKING_DIR, which holds port $PORT"
			else
				echo "Removing $PROJECT, left running by a checkout that no longer exists ($WORKING_DIR)"
			fi
			docker compose --project-name "$PROJECT" down
		done

		if [ -z "$(docker ps --quiet --filter "publish=$PORT")" ] && command -v lsof >/dev/null 2>&1; then
			HOLDER=$(lsof -nP -iTCP:"$PORT" -sTCP:LISTEN 2>/dev/null | awk 'NR == 2 { print $1 " (pid " $2 ")" }')
			if [ -n "$HOLDER" ]; then
				echo "error: port $PORT is held by $HOLDER, outside Docker. Stop it and re-run." >&2
				exit 1
			fi
		fi
	done
}

if [ "${1-}" = "--down" ]; then
	docker compose down
	exit 0
fi

if ! docker info >/dev/null 2>&1; then
	echo "error: Docker is not running. Start Docker Desktop, or run: sudo systemctl start docker (Linux)." >&2
	exit 1
fi

reclaim_ports

echo "Building the API container image"
dotnet publish src/HpacSafety.Api/HpacSafety.Api.csproj \
	--configuration Release \
	/t:PublishContainer \
	-p:ContainerRepository=hpacsafety-api \
	-p:ContainerImageTag=dev

echo "Building the Worker container image"
dotnet publish src/HpacSafety.Worker/HpacSafety.Worker.csproj \
	--configuration Release \
	/t:PublishContainer \
	-p:ContainerRepository=hpacsafety-worker \
	-p:ContainerImageTag=dev

echo "Starting containers"
# The web container runs Vite's own dev server (npm ci && npm run dev)
# against a bind-mounted repo, so edits on disk hot-reload in the browser
# without a rebuild or restart. postgres data and web's node_modules both
# survive restarts via named volumes.
#
# Detached on purpose: this script brings the environment up, waits until it
# is actually serving, and returns. Logs are `docker compose logs -f`.
#
# Always recreated: after a Docker Desktop restart a reused container can come
# back "Up" with its published ports silently gone, and compose sees no
# configuration change that would make it recreate the container itself.
ENV_FILE=""
if [ -f "$REPO_ROOT/.env" ]; then
	ENV_FILE="$REPO_ROOT/.env"
elif [ -f "$MAIN_ROOT/.env" ]; then
	ENV_FILE="$MAIN_ROOT/.env"
fi

if [ -n "$ENV_FILE" ]; then
	echo "Provider keys from $ENV_FILE"
	docker compose --env-file "$ENV_FILE" up --build --detach --force-recreate --remove-orphans
else
	echo "warning: no .env with DEEPL_API_KEY / GEMINI_API_KEY; translations and summaries will fail" >&2
	docker compose up --build --detach --force-recreate --remove-orphans
fi

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
		echo "Storage console  http://localhost:9001/rustfs/console/"
		echo "Logs docker compose logs -f     Stop ./dev-up.sh --down"
		exit 0
	fi

	# A service that exited will not come up by waiting for it. s3-init is
	# one-shot and is meant to exit, so only a non-zero exit counts.
	EXITED=$(docker compose ps --all --status exited --status dead --format '{{.Service}} {{.ExitCode}}' | awk '$2 != 0 { print $1 }')
	if [ -n "$EXITED" ]; then
		for SERVICE in $EXITED; do
			echo "error: $SERVICE exited. Its last output:" >&2
			docker compose logs --tail 30 "$SERVICE" >&2
		done
		exit 1
	fi

	sleep 2
	WAITED=$((WAITED + 2))
done

echo "error: the containers did not come up within ${LIMIT}s. Check: docker compose logs" >&2
exit 1
