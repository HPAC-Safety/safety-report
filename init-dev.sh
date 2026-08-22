#!/usr/bin/env sh
#
# init-dev.sh — make this machine able to build and test safety-report.
#
# One file, POSIX sh, invoked the same way on every platform:
#
#     ./init-dev.sh            install whatever is missing
#     ./init-dev.sh --check    report only; install nothing
#     ./init-dev.sh --help
#
# macOS and Linux run it natively. Windows runs it under Git Bash, which every
# contributor here already has — CONTRIBUTING.md requires Git for Windows with
# `core.symlinks true`, so the shell is not a new dependency. See ADR-0015.
#
# Two properties are requirements, not conveniences:
#
#   Deterministic — every version this script enforces is read at run time from
#   the file that already pins it: global.json for the .NET SDK,
#   .github/workflows/ci.yml for the Node major. There is no second copy of a
#   version number in here, so there is nothing to drift.
#
#   Idempotent — every step probes before it acts. A second consecutive run
#   installs nothing, changes nothing, and exits 0.
#
# Anything that cannot be completed unattended — starting Docker Desktop,
# picking up a new PATH entry, joining the `docker` group — is reported as a
# numbered manual step rather than silently skipped or falsely claimed.

set -eu

REPO_ROOT=$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)
cd "$REPO_ROOT"

# ---------------------------------------------------------------- reporting --

if [ -t 1 ] && [ -z "${NO_COLOR-}" ]; then
	C_OFF=$(printf '\033[0m')
	C_OK=$(printf '\033[32m')
	C_ADD=$(printf '\033[33m')
	C_BAD=$(printf '\033[31m')
	C_DIM=$(printf '\033[2m')
else
	C_OFF='' C_OK='' C_ADD='' C_BAD='' C_DIM=''
fi

# Manual steps accumulate here, one per line, and are printed as a numbered list
# at the end. A step lands here only when the script genuinely cannot do it.
MANUAL=''
FAILED=0

say()    { printf '%s\n' "$*"; }
heading(){ printf '\n%s%s%s\n' "$C_DIM" "$*" "$C_OFF"; }
ok()     { printf '  %s✓%s %s\n' "$C_OK"  "$C_OFF" "$*"; }
added()  { printf '  %s+%s %s\n' "$C_ADD" "$C_OFF" "$*"; }
missing(){ printf '  %s✗%s %s\n' "$C_BAD" "$C_OFF" "$*"; FAILED=$((FAILED + 1)); }
note()   { printf '  %s·%s %s\n' "$C_DIM" "$C_OFF" "$*"; }

manual() {
	# Appended with a literal newline rather than through a command substitution,
	# which strips trailing newlines and would run every entry together into one.
	MANUAL="${MANUAL}${1}
"
	printf '  %s!%s %s\n' "$C_ADD" "$C_OFF" "$1"
}

die() { printf '%serror:%s %s\n' "$C_BAD" "$C_OFF" "$*" >&2; exit 1; }

have() { command -v "$1" >/dev/null 2>&1; }

usage() {
	sed -n '3,27p' "$0" | sed 's/^#\{0,1\} \{0,1\}//'
	exit 0
}

# ------------------------------------------------------------------- options --

CHECK_ONLY=0
for arg in "$@"; do
	case "$arg" in
		--check) CHECK_ONLY=1 ;;
		-h|--help) usage ;;
		*) die "unknown option: $arg (try --help)" ;;
	esac
done

# ------------------------------------------------- required versions, parsed --
#
# Read from the pinning file, never restated. `dotnet` itself is the authority on
# whether an installed SDK satisfies global.json — including the rollForward
# semantics — so the version below is used only when installing.

[ -f global.json ] || die "global.json not found. Run this from a clone of the repository."

SDK_VERSION=$(
	sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([0-9][0-9.]*\)".*/\1/p' global.json | head -n 1
)
[ -n "$SDK_VERSION" ] || die "could not read the SDK version from global.json"
SDK_MAJOR=${SDK_VERSION%%.*}

CI_WORKFLOW=.github/workflows/ci.yml
[ -f "$CI_WORKFLOW" ] || die "$CI_WORKFLOW not found"

NODE_MAJOR=$(
	sed -n 's/.*node-version:[[:space:]]*["'"'"']\{0,1\}\([0-9][0-9]*\).*/\1/p' "$CI_WORKFLOW" | head -n 1
)
[ -n "$NODE_MAJOR" ] || die "could not read node-version from $CI_WORKFLOW"

# ---------------------------------------------------------------- platform ----

case "$(uname -s)" in
	Darwin)                    PLATFORM=macos ;;
	Linux)                     PLATFORM=linux ;;
	MINGW*|MSYS*|CYGWIN*)      PLATFORM=windows ;;
	*) die "unsupported platform: $(uname -s). Install the prerequisites listed in CONTRIBUTING.md by hand." ;;
esac

# The package manager used for everything except the .NET SDK. `none` is a
# supported state: the script then reports what to install and where from,
# rather than failing with a command-not-found.
PKG=none
case "$PLATFORM" in
	macos)
		if have brew; then PKG=brew; fi
		;;
	linux)
		if   have apt-get; then PKG=apt
		elif have dnf;     then PKG=dnf
		elif have pacman;  then PKG=pacman
		fi
		;;
	windows)
		if   have winget; then PKG=winget
		elif have choco;  then PKG=choco
		fi
		;;
esac

# Elevation, only where it is actually needed (Linux package managers).
SUDO=''
if [ "$PLATFORM" = linux ] && [ "$(id -u)" != 0 ] && have sudo; then
	SUDO=sudo
fi

# A scratch directory for downloaded installers. They are written to a file and
# then executed, never piped straight into a shell — the file is inspectable if
# something goes wrong, and a truncated download cannot half-execute.
#
# Created here at the top level rather than lazily inside a helper. A helper
# invoked through command substitution runs in a subshell, so its EXIT trap
# fires the moment the substitution closes — deleting the directory out from
# under the very download that asked for it.
SCRATCH=$(mktemp -d 2>/dev/null || mktemp -d -t init-dev)
trap 'rm -rf "$SCRATCH"' EXIT
trap 'rm -rf "$SCRATCH"; exit 130' INT
trap 'rm -rf "$SCRATCH"; exit 143' TERM

fetch() {
	# fetch <url> <destination>
	if   have curl; then curl -fsSL "$1" -o "$2"
	elif have wget; then wget -qO "$2" "$1"
	else die "neither curl nor wget is available; cannot download $1"
	fi
}

# install_pkg <brew> <apt> <dnf> <pacman> <winget> <choco>
#
# A '-' means "this manager has no package for it". Returns non-zero when the
# platform has no usable manager, without reporting: whether that is fatal
# depends on whether the caller is installing a required tool or an optional
# one, and only the caller knows.
install_pkg() {
	p_brew=$1 p_apt=$2 p_dnf=$3 p_pacman=$4 p_winget=$5 p_choco=$6
	# The package arguments are deliberately unquoted: several entries are more
	# than one word ("nodejs npm", "--cask docker") and must split. So is $SUDO,
	# which is empty when elevation is not needed and must then vanish entirely.
	# shellcheck disable=SC2086
	case "$PKG" in
		brew)   [ "$p_brew"   = - ] || { brew install $p_brew;                           return $?; } ;;
		apt)    [ "$p_apt"    = - ] || { $SUDO apt-get update -qq \
		                                 && $SUDO apt-get install -y $p_apt;             return $?; } ;;
		dnf)    [ "$p_dnf"    = - ] || { $SUDO dnf install -y $p_dnf;                    return $?; } ;;
		pacman) [ "$p_pacman" = - ] || { $SUDO pacman -S --needed --noconfirm $p_pacman; return $?; } ;;
		winget) [ "$p_winget" = - ] || { winget install --exact --id "$p_winget" \
		                                 --accept-source-agreements --accept-package-agreements; return $?; } ;;
		choco)  [ "$p_choco"  = - ] || { choco install -y "$p_choco";                    return $?; } ;;
	esac
	return 1
}

say "safety-report — development environment"
note "platform: $PLATFORM, package manager: $PKG"
note "required: .NET SDK $SDK_VERSION (global.json), Node $NODE_MAJOR ($CI_WORKFLOW)"
if [ "$CHECK_ONLY" -eq 1 ]; then
	note "--check: reporting only, nothing will be installed"
fi

if [ "$PKG" = none ]; then
	case "$PLATFORM" in
		macos)   note "no Homebrew found. Install it first — https://brew.sh — or install the tools below by hand." ;;
		linux)   note "no apt, dnf, or pacman found. Install the tools below with your distribution's package manager." ;;
		windows) note "no winget or Chocolatey found. winget ships with App Installer — https://aka.ms/getwinget" ;;
	esac
fi

# ------------------------------------------------------------------- git ------

heading "git"
if have git; then
	ok "git $(git --version | awk '{print $3}')"
	if [ "$PLATFORM" = windows ] && [ "$(git config --get core.symlinks || echo false)" != true ]; then
		manual "Enable symlinks: git config core.symlinks true, turn on Windows Developer Mode, then re-clone. Without it CLAUDE.md and the other agent instruction files arrive as text containing a path."
	fi
else
	missing "git is not installed, yet this script is running from a clone — install Git for your platform and start again"
fi

# ----------------------------------------------------------------- .NET SDK ---
#
# `dotnet --version` run inside the repository is the authoritative check: it
# resolves global.json, applies rollForward, and fails when no installed SDK
# satisfies it. That is strictly better than comparing version strings here,
# because the resolution rules live in one place and it is not this script.

heading ".NET SDK"
dotnet_satisfied() { have dotnet && dotnet --version >/dev/null 2>&1; }

# An install into ~/.dotnet is invisible until PATH is updated, and PATH is the
# one thing this script cannot change in the caller's shell. Detecting it keeps
# the run idempotent: without this probe every subsequent run re-invokes the
# installer to be told the SDK is already there.
DOTNET_HOME_INSTALL="$HOME/.dotnet/dotnet"
dotnet_home_satisfied() {
	[ -x "$DOTNET_HOME_INSTALL" ] && "$DOTNET_HOME_INSTALL" --version >/dev/null 2>&1
}

# The $HOME and $PATH below are literal: this string is instructions to paste,
# not something to expand here.
# shellcheck disable=SC2016
PATH_HINT='Add the SDK to your PATH, then run ./init-dev.sh again: export PATH="$HOME/.dotnet:$PATH" — put that line in ~/.zshrc or ~/.bashrc to make it stick.'

if dotnet_satisfied; then
	ok ".NET SDK $(dotnet --version) satisfies global.json"
elif dotnet_home_satisfied; then
	ok ".NET SDK $("$DOTNET_HOME_INSTALL" --version) is installed in $HOME/.dotnet but is not on PATH"
	manual "$PATH_HINT"
elif [ "$CHECK_ONLY" -eq 1 ]; then
	if have dotnet; then
		missing ".NET SDK is installed, but no installed SDK satisfies global.json ($SDK_VERSION)"
	else
		missing ".NET SDK $SDK_VERSION is not installed"
	fi
else
	if [ "$PLATFORM" = windows ]; then
		# winget's SDK package tracks the newest feature band of the major
		# version, which global.json's rollForward: latestFeature accepts.
		install_pkg - - - - "Microsoft.DotNet.SDK.$SDK_MAJOR" "dotnet-sdk" || true
	else
		# The official installer is the only mechanism that takes an exact SDK
		# version, which is what makes this step deterministic — no package
		# manager will pin 10.0.100. It writes to ~/.dotnet and touches nothing
		# outside the home directory. Downloaded to a file and then run, never
		# piped into a shell.
		say "  downloading the official .NET installer"
		fetch https://dot.net/v1/dotnet-install.sh "$SCRATCH/dotnet-install.sh"
		sh "$SCRATCH/dotnet-install.sh" --version "$SDK_VERSION" --install-dir "$HOME/.dotnet" --no-path
	fi

	if dotnet_satisfied; then
		added ".NET SDK $(dotnet --version)"
	elif dotnet_home_satisfied; then
		added ".NET SDK $SDK_VERSION into $HOME/.dotnet"
		manual "$PATH_HINT"
	else
		missing ".NET SDK $SDK_VERSION could not be installed — see https://dotnet.microsoft.com/download"
	fi
fi

# -------------------------------------------------------------------- Docker --
#
# Installed and running are separate states and are reported separately. Docker
# Desktop cannot be started unattended on macOS or Windows, and a fresh Linux
# install leaves the invoking user outside the `docker` group until they log in
# again — so a green tick here would be a lie in three common situations.

heading "Docker"
docker_running() { have docker && docker info >/dev/null 2>&1; }

if docker_running; then
	ok "Docker $(docker version --format '{{.Server.Version}}' 2>/dev/null || echo '') is running"
	if docker compose version >/dev/null 2>&1; then
		ok "docker compose plugin"
	else
		manual "Install the Docker Compose plugin: https://docs.docker.com/compose/install/"
	fi
elif have docker; then
	manual "Docker is installed but the daemon is not reachable. Start Docker Desktop (macOS, Windows), or run: sudo systemctl start docker (Linux). Testcontainers integration tests cannot run without it."
elif [ "$CHECK_ONLY" -eq 1 ]; then
	missing "Docker is not installed"
else
	case "$PLATFORM" in
		macos)
			install_pkg "--cask docker" - - - - - && {
				added "Docker Desktop"
				manual "Start Docker Desktop once and complete its first-run setup."
			}
			;;
		windows)
			install_pkg - - - - "Docker.DockerDesktop" "docker-desktop" && {
				added "Docker Desktop"
				manual "Start Docker Desktop, enable the WSL 2 backend, and complete its first-run setup."
			}
			;;
		linux)
			install_pkg - "docker.io" "docker" "docker" - - && {
				added "Docker engine"
				if have systemctl; then
					$SUDO systemctl enable --now docker || true
				fi
				if ! docker info >/dev/null 2>&1; then
					$SUDO usermod -aG docker "$(id -un)" 2>/dev/null || true
					manual "Log out and back in so your shell picks up the 'docker' group, then run this script again."
				fi
			}
			;;
	esac
	have docker || missing "Docker could not be installed — see https://docs.docker.com/get-started/get-docker/"
fi

# ---------------------------------------------------------------------- Node --
#
# CI pins the major; anything newer is fine locally, anything older is not — the
# coverage gate and the node:test suites are written against it.

heading "Node.js"
node_major() { node -p 'process.versions.node.split(".")[0]' 2>/dev/null || echo 0; }

if have node && [ "$(node_major)" -ge "$NODE_MAJOR" ]; then
	ok "Node $(node --version) (>= $NODE_MAJOR)"
elif [ "$CHECK_ONLY" -eq 1 ]; then
	if have node; then
		missing "Node $(node --version) is older than the required major $NODE_MAJOR"
	else
		missing "Node.js $NODE_MAJOR or newer is not installed"
	fi
else
	case "$PLATFORM" in
		macos)   install_pkg "node" - - - - - || true ;;
		windows) install_pkg - - - - "OpenJS.NodeJS.LTS" "nodejs-lts" || true ;;
		linux)
			install_pkg - "nodejs npm" "nodejs npm" "nodejs npm" - - || true
			# Several long-term distributions ship a major well behind CI. The
			# NodeSource repository is the vendor's own supported route to a
			# specific major, and is added only when the distribution's own
			# package turned out to be too old.
			if [ "$(node_major)" -lt "$NODE_MAJOR" ] && [ "$PKG" != pacman ]; then
				say "  distribution Node is older than $NODE_MAJOR; adding the NodeSource repository"
				fetch "https://deb.nodesource.com/setup_${NODE_MAJOR}.x" "$SCRATCH/nodesource.sh" 2>/dev/null \
					|| fetch "https://rpm.nodesource.com/setup_${NODE_MAJOR}.x" "$SCRATCH/nodesource.sh"
				$SUDO sh "$SCRATCH/nodesource.sh"
				install_pkg - "nodejs" "nodejs" - - - || true
			fi
			;;
	esac

	if have node && [ "$(node_major)" -ge "$NODE_MAJOR" ]; then
		added "Node $(node --version)"
	else
		missing "Node.js $NODE_MAJOR or newer could not be installed — see https://nodejs.org/en/download"
	fi
fi

# -------------------------------------------------------------------- Python --
#
# Not needed to build or test. `tools/extract-typeform.py` regenerates
# docs/form-spec.md, and src/web/README.md serves the static site with
# `python3 -m http.server`.

heading "Python 3 (optional)"
if have python3; then
	ok "$(python3 --version 2>&1)"
elif [ "$CHECK_ONLY" -eq 1 ]; then
	note "python3 is not installed — tools/extract-typeform.py will not run"
else
	if install_pkg "python" "python3" "python3" "python" "Python.Python.3" "python3" && have python3; then
		added "$(python3 --version 2>&1)"
	else
		note "python3 could not be installed; it is optional — see https://www.python.org/downloads/"
	fi
fi

# ----------------------------------------------------------------- skillfile --
#
# Optional, and only for agent tooling: it materialises skills/ and agents/ into
# .claude/, which is gitignored. A contributor who does not use an AI agent does
# not need it, so a failure here is a note rather than a failure.

heading "skillfile (optional)"

# Same shape as the .NET step: the installer drops a binary into ~/.local/bin,
# which a fresh shell may not have on PATH. Probe for it before downloading, or
# every run re-downloads a binary that is already there.
SKILLFILE_HOME_INSTALL="$HOME/.local/bin/skillfile"

if have skillfile; then
	ok "skillfile $(skillfile --version 2>/dev/null | awk '{print $NF}')"
elif [ -x "$SKILLFILE_HOME_INSTALL" ]; then
	ok "skillfile is installed in $HOME/.local/bin but is not on PATH"
	# shellcheck disable=SC2016
	manual 'Add skillfile to your PATH, then run ./init-dev.sh again: export PATH="$HOME/.local/bin:$PATH"'
elif [ "$CHECK_ONLY" -eq 1 ]; then
	note "skillfile is not installed — 'skillfile install' will not run"
elif have cargo; then
	cargo install --locked skillfile && added "skillfile"
else
	say "  downloading the official skillfile installer"
	if fetch https://github.com/eljulians/skillfile/releases/latest/download/install.sh "$SCRATCH/skillfile.sh" \
		&& sh "$SCRATCH/skillfile.sh" >/dev/null 2>&1; then
		if have skillfile; then
			added "skillfile"
		elif [ -x "$SKILLFILE_HOME_INSTALL" ]; then
			added "skillfile into $HOME/.local/bin"
			# shellcheck disable=SC2016
			manual 'Add skillfile to your PATH, then run ./init-dev.sh again: export PATH="$HOME/.local/bin:$PATH"'
		else
			note "skillfile installed outside PATH — see https://github.com/eljulians/skillfile"
		fi
	else
		note "skillfile could not be installed; it is optional — see https://github.com/eljulians/skillfile"
	fi
fi

# ------------------------------------------------------- repository restore ---
#
# Everything below is repository state rather than machine state, and every
# command is a no-op when it is already satisfied.

heading "repository"

if [ "$CHECK_ONLY" -eq 1 ]; then
	note "skipped: --check reports machine state and does not touch the repository"
else
	if dotnet_satisfied; then
		if [ ! -f .config/dotnet-tools.json ]; then
			note "no .config/dotnet-tools.json yet; nothing to restore"
		elif dotnet tool restore >/dev/null 2>&1; then
			ok "dotnet local tools (.config/dotnet-tools.json)"
		else
			missing "dotnet tool restore failed — run it directly to see why"
		fi

		if dotnet restore HpacSafety.slnx >/dev/null 2>&1; then
			ok "NuGet packages restored"
		else
			missing "dotnet restore failed — run it directly to see why"
		fi
	else
		note "skipped: the .NET SDK is not usable yet"
	fi

	if ! have skillfile; then
		note "skipped: skillfile is not installed"
	elif skillfile install >/dev/null 2>&1; then
		ok "skills installed into .claude/"
	else
		note "skillfile install failed — skills are optional; run it directly to see why"
	fi
fi

# ------------------------------------------------------------------ summary ---

heading "summary"

if [ -n "$MANUAL" ]; then
	say "  Steps left for you:"
	printf '%s' "$MANUAL" | grep -v '^$' | nl -w4 -s'. ' -ba
	say ""
fi

if [ "$FAILED" -gt 0 ]; then
	printf '  %s%s prerequisite(s) missing.%s Fix the items marked ✗ and run ./init-dev.sh again.\n' \
		"$C_BAD" "$FAILED" "$C_OFF"
	exit 1
fi

if [ -n "$MANUAL" ]; then
	printf '  %sEverything installable is installed.%s Complete the steps above, then run ./init-dev.sh --check.\n' \
		"$C_ADD" "$C_OFF"
	exit 0
fi

printf '  %sReady.%s Build and test with:\n' "$C_OK" "$C_OFF"
say "    dotnet build HpacSafety.slnx"
say "    dotnet test  HpacSafety.slnx"
say ""
say "  Then read AGENTS.md and pick an issue from the Foundation milestone."
