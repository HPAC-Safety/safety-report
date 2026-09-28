#!/bin/sh
# Per-account, per-environment AWS bootstrap for HPAC Safety.
#
# A workflow cannot create the thing that lets it authenticate. This script
# creates the four resources that must exist before the first `terraform init`
# in CI can reach AWS at all, and nothing else. Everything else is Terraform.
#
#   1. the GitHub OIDC identity provider (tolerating one that already exists -
#      the staging account also runs other applications, see below)
#   2. the hpac-safety-deploy IAM role, trusting only THIS environment, and the
#      policy it carries
#   3. the hpac-safety-plan role, which is how `terraform plan` runs on a pull
#      request without the deploy role ever trusting a branch other than main
#   4. the versioned, encrypted, private S3 bucket holding Terraform state,
#      which also holds the state lock - Terraform's S3 backend locks with a
#      conditional PutObject of a .tflock object, so there is no separate lock
#      table to create. See ADR-0031, which supersedes ADR-0010 on this.
#
# It is idempotent. Running it again on an already-bootstrapped account
# converges the existing resources onto the definitions below and changes
# nothing else, which is what makes it safe to re-run after editing a policy.
#
# ONE ACCOUNT, ONE ENVIRONMENT. Per #30/#464 (owner, 2026-09-25): the existing
# AWS account, which also runs other applications, becomes staging; a new,
# HPAC-Safety-only account becomes production. Run this script exactly once in
# each account, with that account's own environment name:
#
#   sh infra/bootstrap.sh staging      # in the existing account
#   sh infra/bootstrap.sh production   # in the new account
#
# NO LONG-LIVED AWS CREDENTIAL IS CREATED, HERE OR ANYWHERE. Run it in AWS
# CloudShell, signed in as an administrator of the target account - nothing
# to install, and CloudShell's own session already carries no long-lived key:
#
#   git clone https://github.com/HPAC-Safety/safety-report
#   sh safety-report/infra/bootstrap.sh staging
#
# On success it prints four values on stdout, one per line, as NAME=value -
# the GitHub *environment variables* (not secrets) that H3 in issue #30 copies
# into that same GitHub environment:
#
#   AWS_DEPLOY_ROLE_ARN=...
#   AWS_PLAN_ROLE_ARN=...
#   TF_STATE_BUCKET=...
#   AWS_ACCOUNT_ID=...
#
# Progress, warnings, and errors go to stderr, so the four lines above can be
# parsed on their own:
#
#   sh infra/bootstrap.sh staging 2>/tmp/bootstrap.log | tee /tmp/bootstrap.env
#
# POSIX sh, not bash: this repository's shell scripts run under Git Bash on
# Windows too, and a bashism breaks the one platform nobody tests by hand. It
# also happens to be exactly what CloudShell's default shell already is. See
# ADR-0015 and ADR-0031.

set -eu

# --------------------------------------------------------------------------
# Argument: the environment this account is
# --------------------------------------------------------------------------

ENVIRONMENT="${1:-}"
case "$ENVIRONMENT" in
  staging | production) ;;
  *)
    printf 'usage: %s <staging|production>\n' "$0" >&2
    printf '\n' >&2
    printf 'Run this once in each AWS account, in AWS CloudShell:\n' >&2
    printf '  sh infra/bootstrap.sh staging      # the existing, shared account\n' >&2
    printf '  sh infra/bootstrap.sh production   # the new, HPAC-Safety-only account\n' >&2
    exit 1
    ;;
esac

# --------------------------------------------------------------------------
# Constants. These are deliberately not parameters.
# --------------------------------------------------------------------------

# ca-central-1 is a data-protection decision, not an infrastructure preference.
# Reports describe real accidents and name real people. See docs/data-handling.md
# and ADR-0009. Do not make this an argument.
REGION='ca-central-1'

# The trust policy names THIS repository and THIS environment. A role trusting
# repo:HPAC-Safety/*:* would let any repository in the organisation deploy this
# system; a role trusting repo:HPAC-Safety/safety-report:* would let any branch
# or workflow, including one pushed to a fork, do the same. Naming the GitHub
# *environment* (rather than a ref) is what makes a job that declares
# `environment: staging` or `environment: production` assumable at all - GitHub
# presents the subject `repo:ORG/REPO:environment:<name>` for such a job, never
# a `ref:` subject (see the OIDC trust below).
GITHUB_ORG='HPAC-Safety'
GITHUB_REPO='safety-report'

OIDC_HOST='token.actions.githubusercontent.com'
OIDC_AUDIENCE='sts.amazonaws.com'

ROLE_NAME='hpac-safety-deploy'
POLICY_NAME='hpac-safety-deploy'
PLAN_ROLE_NAME='hpac-safety-plan'
PLAN_POLICY_NAME='hpac-safety-plan'
# One bucket per account, not per environment: each account IS one environment
# (see above). S3 bucket names are globally unique, so the account id, not the
# environment name, makes this bucket name collision-free across accounts.
# infra/backend.tf reads the bucket at `terraform init` time and keys state
# under it per environment (hpac-safety/<environment>.tfstate), so a second
# environment sharing this account - which #30's design does not call for, but
# which this script does not forbid - still gets its own state file.
STATE_BUCKET_PREFIX='hpac-safety-tfstate'

# Every resource this script itself creates carries this tag. It is the exact
# value #465's Terraform tags every managed resource with; the two must never
# drift, because the deploy policy below also conditions on it.
TAG_KEY='Project'
TAG_VALUE='HPAC-Safety'

# --------------------------------------------------------------------------
# Helpers
# --------------------------------------------------------------------------

say() { printf '%s\n' "$*" >&2; }
die() { printf 'error: %s\n' "$*" >&2; exit 1; }

# --------------------------------------------------------------------------
# Preconditions
# --------------------------------------------------------------------------

command -v aws >/dev/null 2>&1 || die 'the AWS CLI is not on PATH. Run this from AWS CloudShell.'

ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text 2>/dev/null) \
  || die 'no usable AWS session. Sign in to the target AWS account and open CloudShell.'

CALLER_ARN=$(aws sts get-caller-identity --query Arn --output text)

case "$CALLER_ARN" in
  *':user/'*)
    say "warning: $CALLER_ARN is an IAM user, which implies a long-lived access key."
    say '         Nothing in this system should need one. Prefer CloudShell under your own session.'
    ;;
esac

STATE_BUCKET="${STATE_BUCKET_PREFIX}-${ACCOUNT_ID}"
OIDC_ARN="arn:aws:iam::${ACCOUNT_ID}:oidc-provider/${OIDC_HOST}"
ROLE_ARN="arn:aws:iam::${ACCOUNT_ID}:role/${ROLE_NAME}"
POLICY_ARN="arn:aws:iam::${ACCOUNT_ID}:policy/${POLICY_NAME}"
PLAN_ROLE_ARN="arn:aws:iam::${ACCOUNT_ID}:role/${PLAN_ROLE_NAME}"
PLAN_POLICY_ARN="arn:aws:iam::${ACCOUNT_ID}:policy/${PLAN_POLICY_NAME}"

say "Account ${ACCOUNT_ID}, region ${REGION}, environment ${ENVIRONMENT}, as ${CALLER_ARN}"
say ''

# --------------------------------------------------------------------------
# 1. The GitHub OIDC identity provider
# --------------------------------------------------------------------------
#
# TOLERATE AN EXISTING PROVIDER. The staging account also runs other
# applications, some of which may already have created this exact provider -
# there is at most one OIDC provider per URL per account, and it is a shared
# resource. This script never deletes it and never touches another
# application's client IDs or thumbprints; it only adds the one audience this
# system needs, and only if that audience is not already present.
#
# No --thumbprint-list on create. Since June 2023 IAM validates the OIDC
# endpoint's certificate against its own trust store for well-known
# providers, and a pinned thumbprint is a copy of somebody else's rotation
# schedule sitting in our repository. Older CLI versions require the
# argument, so fall back once.

say '1/4  GitHub OIDC identity provider'
if aws iam get-open-id-connect-provider --open-id-connect-provider-arn "$OIDC_ARN" >/dev/null 2>&1; then
  say '     already exists (left as-is; other client IDs and thumbprints are not touched)'
else
  if ! aws iam create-open-id-connect-provider \
        --url "https://${OIDC_HOST}" \
        --client-id-list "$OIDC_AUDIENCE" \
        --tags "Key=${TAG_KEY},Value=${TAG_VALUE}" >/dev/null 2>&1; then
    say '     retrying with an explicit thumbprint (older AWS CLI)'
    aws iam create-open-id-connect-provider \
      --url "https://${OIDC_HOST}" \
      --client-id-list "$OIDC_AUDIENCE" \
      --thumbprint-list '6938fd4d98bab03faadb97b34396831e3780aea1' \
      --tags "Key=${TAG_KEY},Value=${TAG_VALUE}" >/dev/null
  fi
  say '     created'
fi

# The audience must be present even on a provider somebody else created. This
# is additive only: it never removes another application's audience.
if ! aws iam get-open-id-connect-provider \
      --open-id-connect-provider-arn "$OIDC_ARN" \
      --query 'ClientIDList' --output text | grep -qw "$OIDC_AUDIENCE"; then
  aws iam add-client-id-to-open-id-connect-provider \
    --open-id-connect-provider-arn "$OIDC_ARN" \
    --client-id "$OIDC_AUDIENCE" >/dev/null
  say "     added audience ${OIDC_AUDIENCE} (existing audiences left as-is)"
fi

# --------------------------------------------------------------------------
# 2. The deploy role
# --------------------------------------------------------------------------
#
# Trusts EXACTLY this repository's `environment: <ENVIRONMENT>` job, and
# nothing else - not a branch ref, not a tag, not a pull request, not another
# repository, not another environment. `aud` and `sub` are both StringEquals,
# never StringLike: a wildcard here is exactly the gap that would let a
# workflow running under any OTHER GitHub environment (or none) assume this
# role.

say '2/4  hpac-safety-deploy IAM role'

TRUST_POLICY=$(cat <<JSON
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": { "Federated": "${OIDC_ARN}" },
      "Action": "sts:AssumeRoleWithWebIdentity",
      "Condition": {
        "StringEquals": {
          "${OIDC_HOST}:aud": "${OIDC_AUDIENCE}",
          "${OIDC_HOST}:sub": "repo:${GITHUB_ORG}/${GITHUB_REPO}:environment:${ENVIRONMENT}"
        }
      }
    }
  ]
}
JSON
)

if aws iam get-role --role-name "$ROLE_NAME" >/dev/null 2>&1; then
  # Converge, do not skip. Re-running after editing the trust policy above is
  # the supported way to change it.
  aws iam update-assume-role-policy \
    --role-name "$ROLE_NAME" \
    --policy-document "$TRUST_POLICY" >/dev/null
  say '     already exists; trust policy updated'
else
  aws iam create-role \
    --role-name "$ROLE_NAME" \
    --description "Assumed by GitHub Actions (environment: ${ENVIRONMENT}) via OIDC to run Terraform and deploy. Created by infra/bootstrap.sh." \
    --max-session-duration 3600 \
    --assume-role-policy-document "$TRUST_POLICY" \
    --tags "Key=${TAG_KEY},Value=${TAG_VALUE}" "Key=Environment,Value=${ENVIRONMENT}" >/dev/null
  say '     created'
fi

# --------------------------------------------------------------------------
# 2b. The policy the role carries
# --------------------------------------------------------------------------
#
# Terraform manages the whole environment, so most of this is service-scoped
# rather than resource-scoped: writing a resource-scoped policy for a role
# whose job is to CREATE those resources is circular. It is deliberately not
# AdministratorAccess - the role cannot touch IAM users, access keys,
# Organizations, or the account itself, which is the class of action that
# turns a leaked deploy path into a lost account.
#
# THE STAGING ACCOUNT ALSO RUNS OTHER APPLICATIONS. Two independent guards
# keep this role off them:
#   - IAM and S3 are restricted BY NAME to hpac-safety-* roles/policies and
#     hpac-safety-* buckets: the only IAM/S3 resources this role can name at
#     all belong to this system.
#   - Everywhere IAM condition keys support it, a StringEqualsIfExists on
#     aws:ResourceTag/Project and aws:RequestTag/Project pins this role to
#     resources already tagged HPAC-Safety, and to newly created resources
#     being tagged HPAC-Safety. "IfExists" is deliberate: a bare
#     StringEquals would also have to match on actions that carry no tag
#     context at all (many read/list calls, and some create calls before the
#     resource exists to tag), which would silently break Terraform rather
#     than protect anything. The name-based S3/IAM restriction is the hard
#     boundary; the tag condition is defence in depth on top of it, not a
#     substitute for it.
#
# It also cannot read an upload. `s3:*` would otherwise let it fetch a crash
# photograph, and it has no reason to: it CONFIGURES that bucket - policy,
# versioning, encryption, lifecycle - and never reads an object out of it. The
# `NeverReadReportData` denial below covers the versioned actions too, because
# `s3:GetObjectVersion` is a DISTINCT IAM action from `s3:GetObject` and denying
# only the latter leaves every noncurrent version readable. That distinction
# matters more since the quarantine lifecycle rule: between its two hops an
# unverified upload exists precisely as a noncurrent version.
#
# SAME BUG CLASS, SECOND SERVICE: `rds:*` below lets this role manage the
# database, and RDS mirrors its logs into CloudWatch Logs
# (`enabled_cloudwatch_logs_exports` in database.tf) - the postgresql export can
# carry report narrative text, because `log_min_duration_statement` logs the
# statement TEXT for anything slower than a second. A Deny on CloudWatch Logs'
# read API (`logs:GetLogEvents` etc.) is not enough on its own either: RDS's OWN
# native log-download API (`rds:DownloadDBLogFilePortion`,
# `rds:DownloadCompleteDBLogFile`) reaches the very same log content through a
# completely different action namespace. `NeverReadDatabaseLogs` denies both.
#
# THE NAT INSTANCE (#30, #465): every release deletes and recreates it, in a
# one-instance Auto Scaling group (`fck-nat`, a t4g.nano). That is
# `autoscaling:*` - the EC2 Auto Scaling service, a distinct action namespace
# from both `ec2:*` (which covers the instance, its launch template, and the
# network interface/EIP the group attaches to) and the now-dropped
# `application-autoscaling:*` (which scales ECS/DynamoDB/etc. capacity, not EC2
# instances, and this system has none of those left to scale).

DEPLOY_POLICY=$(cat <<JSON
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "TerraformState",
      "Effect": "Allow",
      "Action": [
        "s3:ListBucket",
        "s3:GetBucketVersioning",
        "s3:GetObject",
        "s3:PutObject",
        "s3:DeleteObject"
      ],
      "Resource": [
        "arn:aws:s3:::${STATE_BUCKET}",
        "arn:aws:s3:::${STATE_BUCKET}/*"
      ]
    },
    {
      "Sid": "ManageTheEnvironment",
      "Effect": "Allow",
      "Action": [
        "acm:*",
        "autoscaling:*",
        "cloudfront:*",
        "cloudwatch:*",
        "ec2:*",
        "ecr:*",
        "events:*",
        "lambda:*",
        "logs:*",
        "rds:*",
        "resource-groups:*",
        "scheduler:*",
        "secretsmanager:*",
        "servicecatalog:*",
        "sns:*",
        "tag:*"
      ],
      "Resource": "*",
      "Condition": {
        "StringEqualsIfExists": {
          "aws:ResourceTag/${TAG_KEY}": "${TAG_VALUE}",
          "aws:RequestTag/${TAG_KEY}": "${TAG_VALUE}"
        }
      }
    },
    {
      "Sid": "UseTheAwsManagedKeys",
      "Effect": "Allow",
      "Action": [
        "kms:Decrypt",
        "kms:Encrypt",
        "kms:GenerateDataKey",
        "kms:GenerateDataKeyWithoutPlaintext",
        "kms:DescribeKey",
        "kms:ListAliases",
        "kms:CreateGrant",
        "kms:ListGrants",
        "kms:RevokeGrant"
      ],
      "Resource": "*"
    },
    {
      "Sid": "ManageOurBucketsOnly",
      "Effect": "Allow",
      "Action": "s3:*",
      "Resource": [
        "arn:aws:s3:::hpac-safety-*",
        "arn:aws:s3:::hpac-safety-*/*"
      ],
      "Condition": {
        "StringEqualsIfExists": {
          "aws:ResourceTag/${TAG_KEY}": "${TAG_VALUE}",
          "aws:RequestTag/${TAG_KEY}": "${TAG_VALUE}"
        }
      }
    },
    {
      "Sid": "ManageOurRolesOnly",
      "Effect": "Allow",
      "Action": [
        "iam:CreateRole",
        "iam:DeleteRole",
        "iam:GetRole",
        "iam:UpdateRole",
        "iam:TagRole",
        "iam:UntagRole",
        "iam:ListRoleTags",
        "iam:AttachRolePolicy",
        "iam:DetachRolePolicy",
        "iam:ListAttachedRolePolicies",
        "iam:PutRolePolicy",
        "iam:DeleteRolePolicy",
        "iam:GetRolePolicy",
        "iam:ListRolePolicies",
        "iam:ListInstanceProfilesForRole",
        "iam:UpdateAssumeRolePolicy"
      ],
      "Resource": "arn:aws:iam::${ACCOUNT_ID}:role/hpac-safety-*"
    },
    {
      "Sid": "ManageOurPoliciesOnly",
      "Effect": "Allow",
      "Action": [
        "iam:CreatePolicy",
        "iam:DeletePolicy",
        "iam:GetPolicy",
        "iam:GetPolicyVersion",
        "iam:CreatePolicyVersion",
        "iam:DeletePolicyVersion",
        "iam:ListPolicyVersions",
        "iam:TagPolicy",
        "iam:UntagPolicy",
        "iam:ListPolicyTags"
      ],
      "Resource": "arn:aws:iam::${ACCOUNT_ID}:policy/hpac-safety-*"
    },
    {
      "Sid": "PassOnlyOurRolesToOurServices",
      "Effect": "Allow",
      "Action": "iam:PassRole",
      "Resource": "arn:aws:iam::${ACCOUNT_ID}:role/hpac-safety-*",
      "Condition": {
        "StringEquals": {
          "iam:PassedToService": [
            "lambda.amazonaws.com",
            "scheduler.amazonaws.com"
          ]
        }
      }
    },
    {
      "Sid": "ServiceLinkedRolesAwsRequiresByFixedName",
      "Effect": "Allow",
      "Action": "iam:CreateServiceLinkedRole",
      "Resource": "*",
      "Condition": {
        "StringEquals": {
          "iam:AWSServiceName": [
            "rds.amazonaws.com",
            "autoscaling.amazonaws.com"
          ]
        }
      }
    },
    {
      "Sid": "ReadOnlyIdentityAndApplicationRegistration",
      "Effect": "Allow",
      "Action": [
        "sts:GetCallerIdentity",
        "iam:ListOpenIDConnectProviders",
        "iam:GetOpenIDConnectProvider"
      ],
      "Resource": "*"
    },
    {
      "Sid": "NeverReadReportData",
      "Effect": "Deny",
      "Action": [
        "s3:GetObject",
        "s3:GetObjectAcl",
        "s3:GetObjectAttributes",
        "s3:GetObjectTorrent",
        "s3:GetObjectVersion",
        "s3:GetObjectVersionAcl",
        "s3:GetObjectVersionAttributes",
        "s3:GetObjectVersionTorrent"
      ],
      "Resource": "arn:aws:s3:::hpac-safety-uploads-*/*"
    },
    {
      "Sid": "NeverReadDatabaseLogs",
      "Effect": "Deny",
      "Action": [
        "logs:GetLogEvents",
        "logs:FilterLogEvents",
        "logs:GetLogRecord",
        "logs:StartQuery",
        "logs:GetQueryResults",
        "rds:DownloadDBLogFilePortion",
        "rds:DownloadCompleteDBLogFile"
      ],
      "Resource": "*"
    },
    {
      "Sid": "NeverMintACredentialOrChangeFederation",
      "Effect": "Deny",
      "Action": [
        "iam:CreateUser",
        "iam:CreateAccessKey",
        "iam:CreateLoginProfile",
        "iam:UpdateAccessKey",
        "iam:UpdateLoginProfile",
        "iam:CreateSAMLProvider",
        "iam:UpdateSAMLProvider",
        "iam:DeleteSAMLProvider",
        "iam:CreateOpenIDConnectProvider",
        "iam:DeleteOpenIDConnectProvider",
        "iam:UpdateOpenIDConnectProviderThumbprint",
        "iam:AddClientIDToOpenIDConnectProvider",
        "iam:RemoveClientIDFromOpenIDConnectProvider",
        "organizations:*",
        "account:*"
      ],
      "Resource": "*"
    },
    {
      "Sid": "NeverEditItsOwnPrivileges",
      "Effect": "Deny",
      "Action": [
        "iam:DeleteRole",
        "iam:PutRolePolicy",
        "iam:DeleteRolePolicy",
        "iam:AttachRolePolicy",
        "iam:DetachRolePolicy",
        "iam:UpdateAssumeRolePolicy"
      ],
      "Resource": "arn:aws:iam::${ACCOUNT_ID}:role/${ROLE_NAME}"
    }
  ]
}
JSON
)

if aws iam get-policy --policy-arn "$POLICY_ARN" >/dev/null 2>&1; then
  # A managed policy holds at most five versions. Delete every non-default one
  # before adding another, or the fifth re-run of this script fails.
  # shellcheck disable=SC2016  # JMESPath literal backticks, not shell expansion.
  query='Versions[?IsDefaultVersion==`false`].VersionId'
  for version in $(aws iam list-policy-versions --policy-arn "$POLICY_ARN" \
                     --query "$query" --output text); do
    aws iam delete-policy-version --policy-arn "$POLICY_ARN" --version-id "$version" >/dev/null
  done
  aws iam create-policy-version \
    --policy-arn "$POLICY_ARN" \
    --policy-document "$DEPLOY_POLICY" \
    --set-as-default >/dev/null
  say '     policy already exists; new default version published'
else
  aws iam create-policy \
    --policy-name "$POLICY_NAME" \
    --description 'What hpac-safety-deploy may do. Created by infra/bootstrap.sh.' \
    --policy-document "$DEPLOY_POLICY" \
    --tags "Key=${TAG_KEY},Value=${TAG_VALUE}" >/dev/null
  say '     policy created'
fi

if aws iam list-attached-role-policies --role-name "$ROLE_NAME" \
     --query 'AttachedPolicies[].PolicyArn' --output text | grep -qF "$POLICY_ARN"; then
  say '     policy already attached'
else
  aws iam attach-role-policy --role-name "$ROLE_NAME" --policy-arn "$POLICY_ARN" >/dev/null
  say '     policy attached'
fi

# --------------------------------------------------------------------------
# 3. The plan role
# --------------------------------------------------------------------------
#
# ADR-0010 wants `terraform plan` posted on every pull request - it is the
# single most useful review artifact in the deployment story. But the deploy
# role above trusts ONLY this repository's `environment:` jobs, and a
# pull_request-triggered workflow presents the subject
# "repo:ORG/REPO:pull_request", not an environment subject. It cannot assume
# that role, and widening the deploy role so it could is exactly the thing
# the issue says not to do.
#
# So there are two roles. This one is assumable from any pull request against
# this repository and can read but not write, which is all a plan needs. See
# ADR-0032.
#
# Fork pull requests never reach it at all: GitHub withholds secrets and
# issues a read-only token for them, so the workflow has no role ARN to pass
# and skips.

say '3/4  hpac-safety-plan IAM role (read-only, pull requests)'

PLAN_TRUST_POLICY=$(cat <<JSON
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": { "Federated": "${OIDC_ARN}" },
      "Action": "sts:AssumeRoleWithWebIdentity",
      "Condition": {
        "StringEquals": {
          "${OIDC_HOST}:aud": "${OIDC_AUDIENCE}",
          "${OIDC_HOST}:sub": "repo:${GITHUB_ORG}/${GITHUB_REPO}:pull_request"
        }
      }
    }
  ]
}
JSON
)

if aws iam get-role --role-name "$PLAN_ROLE_NAME" >/dev/null 2>&1; then
  aws iam update-assume-role-policy \
    --role-name "$PLAN_ROLE_NAME" \
    --policy-document "$PLAN_TRUST_POLICY" >/dev/null
  say '     already exists; trust policy updated'
else
  aws iam create-role \
    --role-name "$PLAN_ROLE_NAME" \
    --description 'Assumed by pull-request workflows via OIDC to run terraform plan. Read-only. Created by infra/bootstrap.sh.' \
    --max-session-duration 3600 \
    --assume-role-policy-document "$PLAN_TRUST_POLICY" \
    --tags "Key=${TAG_KEY},Value=${TAG_VALUE}" >/dev/null
  say '     created'
fi

# ReadOnlyAccess plus reading Terraform state, minus three explicit denials.
#
# ReadOnlyAccess on its own would let a plan running on ANY pull request - from
# any contributor with write access to a branch - read every object in the
# uploads bucket. Those objects are photographs of crash sites.
#
# The denial names the VERSIONED actions as well. `s3:GetObjectVersion` is a
# distinct IAM action from `s3:GetObject`, so denying only the latter would leave
# every noncurrent version readable by version id - including, between the two
# hops of the quarantine lifecycle rule, an upload that failed validation.
#
# SAME BUG CLASS, SECOND SERVICE: `ReadOnlyAccess` grants `logs:GetLogEvents` and
# `logs:FilterLogEvents`, which is normally exactly what a read-only role should
# have - except RDS mirrors its own logs into two of those log groups
# (`enabled_cloudwatch_logs_exports` in database.tf), and the postgresql export
# can carry report narrative text: `log_min_duration_statement` logs the
# statement TEXT for anything slower than a second. `NeverReadASecretValue`
# already denies RDS's *native* log-download API
# (`rds:DownloadDBLogFilePortion`, `rds:DownloadCompleteDBLogFile`); it does not
# touch CloudWatch Logs' API, which is a different action namespace reading the
# same data. Same shape as the `s3:GetObjectVersion` gap above: a Deny naming
# one API surface does not cover a different API surface that reaches the same
# underlying data. It would also grant secretsmanager:GetSecretValue and
# rds-data:* on nothing today (ReadOnlyAccess does not include either), but the
# denial is written out anyway so the guarantee does not depend on what AWS
# widens a managed policy to tomorrow.
#
# Deny beats Allow unconditionally, so these hold regardless of what the managed
# policy contains today or gains tomorrow.

PLAN_POLICY=$(cat <<JSON
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "ReadTerraformState",
      "Effect": "Allow",
      "Action": [
        "s3:ListBucket",
        "s3:GetObject"
      ],
      "Resource": [
        "arn:aws:s3:::${STATE_BUCKET}",
        "arn:aws:s3:::${STATE_BUCKET}/*"
      ]
    },
    {
      "Sid": "NeverReadReportData",
      "Effect": "Deny",
      "Action": [
        "s3:GetObject",
        "s3:GetObjectAcl",
        "s3:GetObjectAttributes",
        "s3:GetObjectTorrent",
        "s3:GetObjectVersion",
        "s3:GetObjectVersionAcl",
        "s3:GetObjectVersionAttributes",
        "s3:GetObjectVersionTorrent"
      ],
      "Resource": "arn:aws:s3:::hpac-safety-uploads-*/*"
    },
    {
      "Sid": "NeverReadDatabaseLogs",
      "Effect": "Deny",
      "Action": [
        "logs:GetLogEvents",
        "logs:FilterLogEvents",
        "logs:GetLogRecord",
        "logs:StartQuery",
        "logs:GetQueryResults",
        "rds:DownloadDBLogFilePortion",
        "rds:DownloadCompleteDBLogFile"
      ],
      "Resource": "*"
    },
    {
      "Sid": "NeverReadASecretValue",
      "Effect": "Deny",
      "Action": [
        "secretsmanager:GetSecretValue",
        "rds-data:*"
      ],
      "Resource": "*"
    }
  ]
}
JSON
)

if aws iam get-policy --policy-arn "$PLAN_POLICY_ARN" >/dev/null 2>&1; then
  # shellcheck disable=SC2016  # JMESPath literal backticks, not shell expansion.
  query='Versions[?IsDefaultVersion==`false`].VersionId'
  for version in $(aws iam list-policy-versions --policy-arn "$PLAN_POLICY_ARN" \
                     --query "$query" --output text); do
    aws iam delete-policy-version --policy-arn "$PLAN_POLICY_ARN" --version-id "$version" >/dev/null
  done
  aws iam create-policy-version \
    --policy-arn "$PLAN_POLICY_ARN" \
    --policy-document "$PLAN_POLICY" \
    --set-as-default >/dev/null
  say '     policy already exists; new default version published'
else
  aws iam create-policy \
    --policy-name "$PLAN_POLICY_NAME" \
    --description 'What hpac-safety-plan may do. Created by infra/bootstrap.sh.' \
    --policy-document "$PLAN_POLICY" \
    --tags "Key=${TAG_KEY},Value=${TAG_VALUE}" >/dev/null
  say '     policy created'
fi

for arn in 'arn:aws:iam::aws:policy/ReadOnlyAccess' "$PLAN_POLICY_ARN"; do
  if aws iam list-attached-role-policies --role-name "$PLAN_ROLE_NAME" \
       --query 'AttachedPolicies[].PolicyArn' --output text | grep -qF "$arn"; then
    continue
  fi
  aws iam attach-role-policy --role-name "$PLAN_ROLE_NAME" --policy-arn "$arn" >/dev/null
done
say '     ReadOnlyAccess and the denial policy attached'

# --------------------------------------------------------------------------
# 4. The Terraform state bucket
# --------------------------------------------------------------------------
#
# Versioned because state is the only record of what exists; a corrupt write
# with no previous version is an environment you can no longer manage.
# Encrypted and fully private because Terraform state can hold sensitive
# values, which makes this bucket's access controls something to verify
# rather than assume. One bucket per account; infra/backend.tf keys state
# under it per environment (hpac-safety/<environment>.tfstate).

say "4/4  Terraform state bucket ${STATE_BUCKET}"

if aws s3api head-bucket --bucket "$STATE_BUCKET" >/dev/null 2>&1; then
  say '     already exists'
else
  aws s3api create-bucket \
    --bucket "$STATE_BUCKET" \
    --region "$REGION" \
    --create-bucket-configuration "LocationConstraint=${REGION}" >/dev/null
  say '     created'
fi

aws s3api put-bucket-versioning \
  --bucket "$STATE_BUCKET" \
  --versioning-configuration Status=Enabled >/dev/null

aws s3api put-bucket-encryption \
  --bucket "$STATE_BUCKET" \
  --server-side-encryption-configuration \
  '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"},"BucketKeyEnabled":true}]}' >/dev/null

aws s3api put-public-access-block \
  --bucket "$STATE_BUCKET" \
  --public-access-block-configuration \
  'BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true' >/dev/null

aws s3api put-bucket-policy --bucket "$STATE_BUCKET" --policy "$(cat <<JSON
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "DenyPlaintextTransport",
      "Effect": "Deny",
      "Principal": "*",
      "Action": "s3:*",
      "Resource": [
        "arn:aws:s3:::${STATE_BUCKET}",
        "arn:aws:s3:::${STATE_BUCKET}/*"
      ],
      "Condition": { "Bool": { "aws:SecureTransport": "false" } }
    }
  ]
}
JSON
)" >/dev/null

aws s3api put-bucket-tagging \
  --bucket "$STATE_BUCKET" \
  --tagging "TagSet=[{Key=${TAG_KEY},Value=${TAG_VALUE}},{Key=Environment,Value=${ENVIRONMENT}}]" >/dev/null

say '     versioning, encryption, public-access block, TLS-only policy applied'

# --------------------------------------------------------------------------
# Done
# --------------------------------------------------------------------------

say ''
say "Bootstrap complete for ${ENVIRONMENT}. Next (see docs/deployment.md H3):"
say ''
say "  In GitHub: Settings -> Environments -> ${ENVIRONMENT} -> Environment variables,"
say '  add each of the four NAME=value lines printed below (they are identifiers,'
# shellcheck disable=SC2016  # backticks are markdown code formatting, not shell expansion.
say '  not secrets - no `gh secret set` here, only `gh variable set` if scripting it):'
say ''
say "    gh variable set AWS_DEPLOY_ROLE_ARN --repo ${GITHUB_ORG}/${GITHUB_REPO} --env ${ENVIRONMENT} --body ${ROLE_ARN}"
say "    gh variable set AWS_PLAN_ROLE_ARN   --repo ${GITHUB_ORG}/${GITHUB_REPO} --env ${ENVIRONMENT} --body ${PLAN_ROLE_ARN}"
say "    gh variable set TF_STATE_BUCKET     --repo ${GITHUB_ORG}/${GITHUB_REPO} --env ${ENVIRONMENT} --body ${STATE_BUCKET}"
say "    gh variable set AWS_ACCOUNT_ID      --repo ${GITHUB_ORG}/${GITHUB_REPO} --env ${ENVIRONMENT} --body ${ACCOUNT_ID}"
say ''
say "infra/backend.tf is a partial configuration: the bucket name carries the account"
say "id, so it is supplied at init time from TF_STATE_BUCKET rather than committed,"
say "and the state key is namespaced per environment: hpac-safety/${ENVIRONMENT}.tfstate."
say ''

# The four values on stdout, NAME=value, so the output stays parseable even
# though there is more than one of them now.
printf 'AWS_DEPLOY_ROLE_ARN=%s\n' "$ROLE_ARN"
printf 'AWS_PLAN_ROLE_ARN=%s\n' "$PLAN_ROLE_ARN"
printf 'TF_STATE_BUCKET=%s\n' "$STATE_BUCKET"
printf 'AWS_ACCOUNT_ID=%s\n' "$ACCOUNT_ID"
