# The API's DEPLOYED container image: the host's `dotnet publish` output, on
# the same .NET runtime image `dev-up.sh`'s PublishContainer build uses, plus
# the AWS Lambda Web Adapter (ADR-0042).
#
# Build it with tools/build-api-image.sh, never by hand — same convention as
# the Worker's Dockerfile (ADR-0118): the build context is the publish output,
# not the repository. Local development keeps using `dotnet publish
# /t:PublishContainer` (dev-up.sh) — a plain Kestrel container needs no
# adapter, because nothing local puts a Lambda Function URL event in front of
# it. This Dockerfile is release.yml's, for the image that actually runs on
# Lambda.
#
# The adapter translates each Function URL event into a loopback HTTP request
# against Kestrel — the ASP.NET Core application has no Lambda SDK dependency
# and no "am I running in Lambda" branch. AWS_LWA_PORT and
# AWS_LWA_READINESS_CHECK_PATH (infra/lambda.tf) tell it where to send that
# request and how to know the app is ready.
#
# The adapter comes from this organization's mirror of
# public.ecr.aws/awsguru/aws-lambda-adapter, never from public.ecr.aws itself:
# that registry limits anonymous pulls per source IP, and CI runners share
# their IPs with everyone else's jobs (#629). The tag and digest are
# upstream's, unchanged by the copy; tools/mirror-lambda-adapter.sh copies
# this exact line's image before the release builds, and Renovate bumps it
# against upstream.
FROM ghcr.io/hpac-safety/aws-lambda-adapter:1.1.0@sha256:17cfd08eff1dfea3f6a9a1e9c65fdac80aa4919b6085e746615530f43f57d2f1 AS lambda-adapter

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:2d584d8147faddb0d678c5748d47953e5b8e18621ed4fb7049a91381d9d7746f

COPY --from=lambda-adapter /lambda-adapter /opt/extensions/lambda-adapter

WORKDIR /app
COPY . .

# The base image's non-root user; the API needs no privilege.
USER $APP_UID

ENTRYPOINT ["dotnet", "HpacSafety.Api.dll"]
