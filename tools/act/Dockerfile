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
# gh and shellcheck come from Ubuntu 24.04's own archive, the one GitHub's
# runner image installs ShellCheck from. Built locally by tools/ci-local.sh;
# never pushed.
FROM --platform=linux/amd64 ubuntu:24.04@sha256:008173c23f95b170204355c12626cb5a965d779a7e1283b09e9cffbb1bf33ca3 AS amd64

FROM ghcr.io/catthehacker/ubuntu:act-24.04@sha256:62d572b92f9f32d3427b6d220ad1f9dca9c7b6ffad37d295425037dbff78abaf

RUN apt-get update \
	&& apt-get install -y --no-install-recommends gh shellcheck \
	&& rm -rf /var/lib/apt/lists/*

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
