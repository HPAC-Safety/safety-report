#!/usr/bin/env sh
#
# relock-providers.sh — regenerate infra/.terraform.lock.hcl by hand.
#
# Renovate cannot be trusted to do this: its terraform manager computes lock
# hashes itself instead of running terraform, and has produced a lock file
# whose checksums did not match the real published package (issue #118).
# lockFileMaintenance is disabled for terraform in renovate.json for that
# reason — run this script instead whenever the pinned provider version
# needs to move.
#
# Run this after bumping a version constraint in a .tf file, or periodically
# to pick up a newer version within the existing constraint.

set -eu

REPO_ROOT=$(CDPATH='' cd -- "$(dirname -- "$0")/.." && pwd)
cd "$REPO_ROOT/infra"

terraform providers lock \
	-platform=linux_amd64 \
	-platform=linux_arm64 \
	-platform=darwin_amd64 \
	-platform=darwin_arm64 \
	-platform=windows_amd64

echo "Verifying with a readonly init, the same check CI runs:"
rm -rf .terraform
terraform init -backend=false -lockfile=readonly
rm -rf .terraform
