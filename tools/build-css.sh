#!/usr/bin/env sh
#
# build-css.sh — compile src/web/styles/tailwind.css into src/web/styles/site.css.
#
#     ./tools/build-css.sh              build once, minified
#     ./tools/build-css.sh --watch      rebuild on change
#     ./tools/build-css.sh --check      report what it would use; build nothing
#     ./tools/build-css.sh --help
#
# The only build step the web layer has. There is no npm, no node, and no
# node_modules here: Tailwind v4 ships a standalone binary and that is what this
# downloads. See docs/decisions/ADR-0006-theme-engine.md.
#
# Three properties are requirements, not conveniences:
#
#   Pinned — the version lives in tools/tailwind.pin and nowhere else, and is
#   read from there at run time. "latest" is never fetched, so a stylesheet
#   built today and one built next year are the same stylesheet.
#
#   Verified — the downloaded binary's SHA-256 must match the checksum recorded
#   for that release in the same pin file. A binary that does not match is
#   deleted, not run.
#
#   Idempotent — a second consecutive run re-uses the verified binary already in
#   tools/ and downloads nothing.
#
# POSIX sh, like init-dev.sh: Windows contributors run it under Git Bash.

set -eu

REPO_ROOT=$(CDPATH='' cd -- "$(dirname -- "$0")/.." && pwd)
cd "$REPO_ROOT"

PIN_FILE=tools/tailwind.pin
BIN=./tools/tailwindcss
INPUT=src/web/styles/tailwind.css
OUTPUT=src/web/styles/site.css

die() { printf 'build-css: %s\n' "$*" >&2; exit 1; }
say() { printf '%s\n' "$*"; }

usage() {
	sed -n '3,10p' "$0" | sed 's/^#\{0,1\} \{0,1\}//'
	exit 0
}

MODE=build
for arg in "$@"; do
	case "$arg" in
		--watch) MODE=watch ;;
		--check) MODE=check ;;
		-h|--help) usage ;;
		*) die "unknown option: $arg (try --help)" ;;
	esac
done

# ------------------------------------------------------------------- the pin --

[ -f "$PIN_FILE" ] || die "$PIN_FILE not found. Run this from a clone of the repository."

VERSION=$(sed -n 's/^version[[:space:]]\{1,\}\([0-9][0-9A-Za-z.-]*\)[[:space:]]*$/\1/p' "$PIN_FILE" | head -n 1)
[ -n "$VERSION" ] || die "no 'version' line in $PIN_FILE"

# --------------------------------------------------------- platform and asset --
#
# The asset name is the release asset published by tailwindlabs/tailwindcss, and
# it is also the key the checksum is recorded under, so the two cannot disagree.

case "$(uname -s)" in
	Darwin) os=macos ;;
	Linux)  os=linux ;;
	MINGW*|MSYS*|CYGWIN*) os=windows ;;
	*) die "unsupported platform: $(uname -s). Set TAILWIND_BIN to a Tailwind $VERSION binary and run again." ;;
esac

case "$(uname -m)" in
	arm64|aarch64) arch=arm64 ;;
	x86_64|amd64)  arch=x64 ;;
	*) die "unsupported architecture: $(uname -m). Set TAILWIND_BIN to a Tailwind $VERSION binary and run again." ;;
esac

case "$os" in
	windows)
		[ "$arch" = x64 ] || die "no Tailwind standalone build for windows-$arch"
		ASSET="tailwindcss-windows-x64.exe"
		;;
	macos)
		ASSET="tailwindcss-macos-$arch"
		;;
	linux)
		# musl and glibc are separate builds and the glibc one will not start on
		# Alpine. `ldd --version` writes its banner to stderr on musl and exits
		# non-zero, hence the redirect and the `|| true`.
		if (ldd --version 2>&1 || true) | grep -qi musl; then
			ASSET="tailwindcss-linux-$arch-musl"
		else
			ASSET="tailwindcss-linux-$arch"
		fi
		;;
esac

EXPECTED=$(
	sed -n "s|^sha256[[:space:]]\{1,\}${ASSET}[[:space:]]\{1,\}\([0-9a-f]\{64\}\)[[:space:]]*\$|\1|p" "$PIN_FILE" | head -n 1
)
[ -n "$EXPECTED" ] || die "no sha256 recorded for $ASSET in $PIN_FILE"

URL="https://github.com/tailwindlabs/tailwindcss/releases/download/v$VERSION/$ASSET"

# ------------------------------------------------------------------ checksums --

sha256_of() {
	if   command -v shasum >/dev/null 2>&1;   then shasum -a 256 "$1" | awk '{print $1}'
	elif command -v sha256sum >/dev/null 2>&1; then sha256sum "$1" | awk '{print $1}'
	else die "neither shasum nor sha256sum is available; cannot verify the download"
	fi
}

# ------------------------------------------------------------------ the binary --
#
# TAILWIND_BIN is the escape hatch for an air-gapped build or a distribution
# package. It is used as given and is NOT checksummed - the caller has taken
# responsibility for it, and saying so here is more honest than a check that
# would always fail.

if [ -n "${TAILWIND_BIN-}" ]; then
	[ -x "$TAILWIND_BIN" ] || die "TAILWIND_BIN=$TAILWIND_BIN is not an executable file"
	BIN=$TAILWIND_BIN
	case "$BIN" in */*) : ;; *) BIN="./$BIN" ;; esac
	say "build-css: using TAILWIND_BIN=$BIN (not checksum-verified; set by the caller)"
else
	if [ -f "$BIN" ] && [ "$(sha256_of "$BIN")" = "$EXPECTED" ]; then
		say "build-css: Tailwind $VERSION already present and verified ($BIN)"
	else
		[ "$MODE" = check ] || {
			if [ -f "$BIN" ]; then
				say "build-css: $BIN is not Tailwind $VERSION; replacing it"
			fi
			say "build-css: downloading $ASSET from the v$VERSION release"

			tmp="$BIN.download.$$"
			# Downloaded to a file and checksummed before it is ever made
			# executable, so a truncated or substituted binary cannot run.
			if   command -v curl >/dev/null 2>&1; then curl -fsSL "$URL" -o "$tmp"
			elif command -v wget >/dev/null 2>&1; then wget -qO "$tmp" "$URL"
			else die "neither curl nor wget is available; cannot download $URL"
			fi

			actual=$(sha256_of "$tmp")
			if [ "$actual" != "$EXPECTED" ]; then
				rm -f "$tmp"
				die "checksum mismatch for $ASSET
  expected $EXPECTED (from $PIN_FILE)
  actual   $actual
The download was discarded. Do not run it."
			fi

			chmod +x "$tmp"
			mv "$tmp" "$BIN"
			say "build-css: verified Tailwind $VERSION"
		}
	fi
fi

# -------------------------------------------------------------------- the build --

[ -f "$INPUT" ] || die "$INPUT not found"

if [ "$MODE" = check ]; then
	say "build-css: version $VERSION (from $PIN_FILE)"
	say "build-css: asset   $ASSET"
	say "build-css: binary  $BIN"
	say "build-css: $INPUT -> $OUTPUT"
	say "build-css: --check: nothing was built"
	exit 0
fi

if [ "$MODE" = watch ]; then
	exec "$BIN" --input "$INPUT" --output "$OUTPUT" --watch
fi

"$BIN" --input "$INPUT" --output "$OUTPUT" --minify

[ -s "$OUTPUT" ] || die "$OUTPUT was not produced, or is empty"
say "build-css: wrote $OUTPUT"
