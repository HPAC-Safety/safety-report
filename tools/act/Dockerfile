# The image tools/ci-local.sh runs every `ubuntu-latest` job in under act
# (ADR-0145). It is catthehacker's act-24.04 image, pinned by digest and native
# on arm64 and amd64, plus what our workflows call that it lacks:
#
#   gh          the coverage job's baseline download, agent-config's skillfile
#               download
#   shellcheck  the build job and terraform `infra`
#   the x86-64 loader, libc, and libgcc_s, on arm64 only
#               the workflows download linux-amd64 builds (terraform, tflint,
#               skillfile). Docker Desktop runs them through Rosetta; the static
#               ones need nothing more, and skillfile needs these three.
#
# gh is GitHub's own release, pinned by version and SHA-256: Ubuntu's gh
# 2.45.0 lacks the `gh run download` path-traversal fix (CVE-2024-54132, fixed
# in 2.63.1), and its -updates pocket keeps only the newest build, so an exact
# apt pin stops resolving after the next update. shellcheck 0.9.0-1 comes from
# noble's release pocket, which never changes, and is the version GitHub's
# runner image ships.
#
# Built locally by tools/ci-local.sh, tagged with this file's hash so a change
# rebuilds it; never pushed.
FROM --platform=linux/amd64 ubuntu:24.04@sha256:008173c23f95b170204355c12626cb5a965d779a7e1283b09e9cffbb1bf33ca3 AS amd64

FROM ghcr.io/catthehacker/ubuntu:act-24.04@sha256:62d572b92f9f32d3427b6d220ad1f9dca9c7b6ffad37d295425037dbff78abaf

ARG GH_VERSION=2.101.0
ARG GH_SHA256_AMD64=9bca2d1c16825f109907a23307628a2f0698fbf99662b73a5cf0b020293072b8
ARG GH_SHA256_ARM64=b57e8063f18862647c9d22727c32e9da1b963f8bf9db648fe123a6975695640f

RUN apt-get update \
	&& apt-get install -y --no-install-recommends shellcheck=0.9.0-1 \
	&& rm -rf /var/lib/apt/lists/*

RUN set -eu; \
	arch=$(dpkg --print-architecture); \
	case "$arch" in \
		amd64) sum=$GH_SHA256_AMD64 ;; \
		arm64) sum=$GH_SHA256_ARM64 ;; \
		*) echo "no pinned gh build for $arch" >&2; exit 1 ;; \
	esac; \
	curl -fsSL -o /tmp/gh.tar.gz \
		"https://github.com/cli/cli/releases/download/v${GH_VERSION}/gh_${GH_VERSION}_linux_${arch}.tar.gz"; \
	echo "$sum  /tmp/gh.tar.gz" | sha256sum -c -; \
	tar -xzf /tmp/gh.tar.gz -C /tmp; \
	install -m 0755 "/tmp/gh_${GH_VERSION}_linux_${arch}/bin/gh" /usr/local/bin/gh; \
	rm -rf /tmp/gh.tar.gz "/tmp/gh_${GH_VERSION}_linux_${arch}"; \
	gh --version

# Staged, then installed only where the image is not already x86-64, so an
# amd64 host's own libc is never replaced.
COPY --from=amd64 /usr/lib64/ld-linux-x86-64.so.2 /opt/amd64/lib64/
COPY --from=amd64 /usr/lib/x86_64-linux-gnu/libc.so.6 /usr/lib/x86_64-linux-gnu/libgcc_s.so.1 /opt/amd64/lib/
RUN if [ "$(dpkg --print-architecture)" != amd64 ]; then \
		mkdir -p /usr/lib64 /usr/lib/x86_64-linux-gnu \
		&& cp /opt/amd64/lib64/* /usr/lib64/ \
		&& cp /opt/amd64/lib/* /usr/lib/x86_64-linux-gnu/ \
		&& { [ -e /lib64 ] || ln -s usr/lib64 /lib64; }; \
	fi \
	&& rm -rf /opt/amd64
