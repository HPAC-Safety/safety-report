#!/usr/bin/env bash
# Copy the AWS Lambda Web Adapter image that src/HpacSafety.Api/Dockerfile
# names into this organization's GHCR mirror, unless it is already there
# (#629).
#
#   GHCR_USER=<actor> GHCR_TOKEN=<token with packages:write> tools/build/mirror-lambda-adapter.sh
#
# The Dockerfile pulls ghcr.io/hpac-safety/aws-lambda-adapter:<tag>@<digest>,
# never public.ecr.aws: that registry limits anonymous pulls per source IP,
# and GitHub's hosted runners share their IPs with everyone else's jobs, so
# a release build was refused with 429 "Data limit exceeded" no matter how
# little this repository pulled. The tag and digest on that line are
# upstream's; `skopeo copy --preserve-digests` keeps the digest, so the
# mirror holds the identical image and Renovate can bump the line against
# upstream. release.yml's build job runs this first, so a bumped version is
# mirrored by the first release that needs it — once, from public.ecr.aws,
# and never again.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
DOCKERFILE="$REPO_ROOT/src/HpacSafety.Api/Dockerfile"
UPSTREAM='public.ecr.aws/awsguru/aws-lambda-adapter'

: "${GHCR_USER:?set GHCR_USER to the GitHub account pushing the mirror}"
: "${GHCR_TOKEN:?set GHCR_TOKEN to a token with packages:write}"

line=$(grep -E '^FROM ghcr\.io/[a-z0-9-]+/aws-lambda-adapter:[^@ ]+@sha256:[a-f0-9]{64} ' "$DOCKERFILE" || true)
if [ -z "$line" ]; then
	echo "error: $DOCKERFILE has no 'FROM ghcr.io/<org>/aws-lambda-adapter:<tag>@sha256:<digest>' line." >&2
	exit 1
fi
ref=$(echo "$line" | awk '{print $2}')
mirror=${ref%%:*}
tag=${ref#*:}
tag=${tag%%@*}
digest=${ref#*@}

if ! command -v skopeo >/dev/null 2>&1; then
	sudo apt-get update -qq
	sudo apt-get install -y -qq skopeo
fi

creds="$GHCR_USER:$GHCR_TOKEN"

if skopeo inspect --raw --creds "$creds" "docker://$mirror@$digest" >/dev/null 2>&1; then
	echo "Mirror already holds $mirror:$tag ($digest)."
	exit 0
fi

echo "Mirroring $UPSTREAM@$digest ($tag) to $mirror:$tag."
# The one pull from public.ecr.aws this version ever needs; retried because
# that is exactly where the 429 comes from. The source is named by digest
# alone: skopeo refuses a reference carrying both a tag and a digest (#631),
# and the digest is what pins the image anyway.
attempt=1
log=$(mktemp)
until skopeo copy --all --preserve-digests --retry-times 3 \
	--dest-creds "$creds" \
	"docker://$UPSTREAM@$digest" \
	"docker://$mirror:$tag" 2>&1 | tee "$log"; [ "${PIPESTATUS[0]}" -eq 0 ]; do
	# Only a refusal that can clear is worth waiting out; a malformed
	# reference or a denied push fails the same way every time.
	if ! grep -qiE '\b429\b|toomanyrequests|rate limit|limit exceeded|timeout|connection|EOF|\b5[0-9]{2}\b' "$log"; then
		echo "error: skopeo copy failed with an error a retry cannot fix." >&2
		exit 1
	fi
	if [ "$attempt" -ge 5 ]; then
		echo "error: could not copy $UPSTREAM@$digest after $attempt attempts." >&2
		exit 1
	fi
	delay=$((attempt * 60))
	echo "skopeo copy failed (attempt $attempt); retrying in ${delay}s." >&2
	sleep "$delay"
	attempt=$((attempt + 1))
done

skopeo inspect --raw --creds "$creds" "docker://$mirror@$digest" >/dev/null
echo "Mirrored $mirror:$tag ($digest)."
