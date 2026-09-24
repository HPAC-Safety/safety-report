# The Worker's container image: the host's `dotnet publish` output, on
# Microsoft's .NET runtime image, plus Ubuntu's ffmpeg (ADR-0118).
#
# Build it with tools/build-worker-image.sh, never by hand: the build context is
# the publish output, not the repository, so nothing but the compiled Worker
# reaches the image.
#
# ffmpeg parses files strangers upload, so it comes from Ubuntu's archive, whose
# security team patches it; rebuilding picks the fix up. It runs only as a child
# process doing a stream-copy remux (ADR-0094), never linked into the Worker.
# The base is pinned by digest; Renovate moves the pin within the .NET major.
# The major moves by hand, with global.json and the target framework (ADR-0120).
FROM mcr.microsoft.com/dotnet/runtime:10.0@sha256:ff17a18b639a0327e52c7c296fa2e1abe6e03eb61d8121a8ef67cc6aa430a27e

RUN apt-get update \
	&& apt-get install --yes --no-install-recommends ffmpeg \
	&& rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY . .

# The base image's non-root user; the Worker needs no privilege.
USER $APP_UID

ENTRYPOINT ["dotnet", "HpacSafety.Worker.dll"]
