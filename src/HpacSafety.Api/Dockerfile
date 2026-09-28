# The API's DEPLOYED container image: the host's `dotnet publish` output, on
# the same .NET runtime image `dev-up.sh`'s PublishContainer build uses, plus
# the AWS Lambda Web Adapter (ADR-0042).
#
# Build it with tools/build-api-image.sh, never by hand — same convention as
# the Worker's Dockerfile (ADR-0118): the build context is the publish output,
# not the repository. Local development keeps using `dotnet publish
# /t:PublishContainer` (dev-up.sh) — a plain Kestrel container needs no
# adapter, because nothing local puts a Lambda Function URL event in front of
# it. This Dockerfile is deploy-api.yml's, for the image that actually runs on
# Lambda.
#
# The adapter translates each Function URL event into a loopback HTTP request
# against Kestrel — the ASP.NET Core application has no Lambda SDK dependency
# and no "am I running in Lambda" branch. AWS_LWA_PORT and
# AWS_LWA_READINESS_CHECK_PATH (infra/lambda.tf) tell it where to send that
# request and how to know the app is ready.
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:2d584d8147faddb0d678c5748d47953e5b8e18621ed4fb7049a91381d9d7746f

COPY --from=public.ecr.aws/awsguru/aws-lambda-adapter:1.1.0 /lambda-adapter /opt/extensions/lambda-adapter

WORKDIR /app
COPY . .

# The base image's non-root user; the API needs no privilege.
USER $APP_UID

ENTRYPOINT ["dotnet", "HpacSafety.Api.dll"]
