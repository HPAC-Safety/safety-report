#!/bin/sh
# One-time AWS bootstrap for HPAC Safety.
#
# A workflow cannot create the thing that lets it authenticate. This script
# creates the four resources that must exist before the first `terraform init`
# in CI can reach AWS at all, and nothing else. Everything else is Terraform.
#
#   1. the GitHub OIDC identity provider
#   2. the hpac-safety-deploy IAM role, and the policy it carries
#   3. the hpac-safety-plan role, which is how `terraform plan` runs on a pull
#      request without the deploy role ever trusting a branch other than main
#   4. the versioned, encrypted, private S3 bucket holding Terraform state,
#      which also holds the state lock - Terraform's S3 backend locks with a
#      conditional PutObject of a .tflock object, so there is no separate lock
#      table to create. See ADR-0031, which supersedes ADR-0010 on this.
#
# It is idempotent. Running it on an already-bootstrapped account converges the
# existing resources onto the definitions below and changes nothing else, which
# is what makes it safe to re-run after editing the trust policy.
#
# NO LONG-LIVED AWS CREDENTIAL IS CREATED, HERE OR ANYWHERE. It runs against an
# administrator's own SSO session:
#
#   aws sso login
#   ./infra/bootstrap.sh
#
# On success it prints the role ARN on stdout and nothing else, so it can be
# piped:
#
#   gh secret set AWS_DEPLOY_ROLE_ARN --body "$(./infra/bootstrap.sh)"
#
# Progress, warnings, and errors go to stderr.
#
# POSIX sh, not bash: this repository's shell scripts run under Git Bash on
# Windows too, and a bashism breaks the one platform nobody tests by hand.
# See ADR-0015 and ADR-0031.

set -eu

# --------------------------------------------------------------------------
# Constants. These are deliberately not parameters.
# --------------------------------------------------------------------------

# ca-central-1 is a data-protection decision, not an infrastructure preference.
# Reports describe real accidents and name real people. See docs/data-handling.md
# and ADR-0009. Do not make this an argument.
REGION='ca-central-1'

# The trust policy names THIS repository and THIS ref. A role trusting
# repo:HPAC-Safety/*:* would let any repository in the organisation deploy this
# system; a role trusting repo:HPAC-Safety/safety-report:* would let any branch,
# including one pushed to a fork, do the same.
GITHUB_ORG='HPAC-Safety'
GITHUB_REPO='safety-report'
GITHUB_REF='refs/heads/main'

OIDC_HOST='token.actions.githubusercontent.com'
OIDC_AUDIENCE='sts.amazonaws.com'

ROLE_NAME='hpac-safety-deploy'
POLICY_NAME='hpac-safety-deploy'
PLAN_ROLE_NAME='hpac-safety-plan'
PLAN_POLICY_NAME='hpac-safety-plan'
# The state bucket name gets the account id appended below: S3 bucket names are
# globally unique, so a fixed name would collide with any other account that ran
# this script.
STATE_BUCKET_PREFIX='hpac-safety-tfstate'

TAG_KEY='Project'
TAG_VALUE='hpac-safety'

# --------------------------------------------------------------------------
# Helpers
# --------------------------------------------------------------------------

say() { printf '%s\n' "$*" >&2; }
die() { printf 'error: %s\n' "$*" >&2; exit 1; }

# --------------------------------------------------------------------------
# Preconditions
# --------------------------------------------------------------------------

command -v aws >/dev/null 2>&1 || die 'the AWS CLI is not on PATH. See docs/deployment.md.'

ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text 2>/dev/null) \
  || die 'no usable AWS session. Run "aws sso login" (or export an admin session) first.'

CALLER_ARN=$(aws sts get-caller-identity --query Arn --output text)

case "$CALLER_ARN" in
  *':user/'*)
    say "warning: $CALLER_ARN is an IAM user, which implies a long-lived access key."
    say '         Nothing in this system should need one. Prefer an SSO session.'
    ;;
esac

STATE_BUCKET="${STATE_BUCKET_PREFIX}-${ACCOUNT_ID}"
OIDC_ARN="arn:aws:iam::${ACCOUNT_ID}:oidc-provider/${OIDC_HOST}"
ROLE_ARN="arn:aws:iam::${ACCOUNT_ID}:role/${ROLE_NAME}"
POLICY_ARN="arn:aws:iam::${ACCOUNT_ID}:policy/${POLICY_NAME}"
PLAN_ROLE_ARN="arn:aws:iam::${ACCOUNT_ID}:role/${PLAN_ROLE_NAME}"
PLAN_POLICY_ARN="arn:aws:iam::${ACCOUNT_ID}:policy/${PLAN_POLICY_NAME}"

say "Account ${ACCOUNT_ID}, region ${REGION}, as ${CALLER_ARN}"
say ''

# --------------------------------------------------------------------------
# 1. The GitHub OIDC identity provider
# --------------------------------------------------------------------------
#
# No --thumbprint-list. Since June 2023 IAM validates the OIDC endpoint's
# certificate against its own trust store for well-known providers, and a
# pinned thumbprint is a copy of somebody else's rotation schedule sitting in
# our repository. Older CLI versions require the argument, so fall back once.

say '1/4  GitHub OIDC identity provider'
if aws iam get-open-id-connect-provider --open-id-connect-provider-arn "$OIDC_ARN" >/dev/null 2>&1; then
  say '     already exists'
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

# The audience must be present even on a provider somebody else created.
if ! aws iam get-open-id-connect-provider \
      --open-id-connect-provider-arn "$OIDC_ARN" \
      --query 'ClientIDList' --output text | grep -qw "$OIDC_AUDIENCE"; then
  aws iam add-client-id-to-open-id-connect-provider \
    --open-id-connect-provider-arn "$OIDC_ARN" \
    --client-id "$OIDC_AUDIENCE" >/dev/null
  say "     added audience ${OIDC_AUDIENCE}"
fi

# --------------------------------------------------------------------------
# 2. The deploy role
# --------------------------------------------------------------------------

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
          "${OIDC_HOST}:sub": "repo:${GITHUB_ORG}/${GITHUB_REPO}:ref:${GITHUB_REF}"
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
    --description 'Assumed by GitHub Actions via OIDC to run Terraform and deploy. Created by infra/bootstrap.sh.' \
    --max-session-duration 3600 \
    --assume-role-policy-document "$TRUST_POLICY" \
    --tags "Key=${TAG_KEY},Value=${TAG_VALUE}" >/dev/null
  say '     created'
fi

# --------------------------------------------------------------------------
# 2b. The policy the role carries
# --------------------------------------------------------------------------
#
# Terraform manages the whole environment, so this is service-scoped rather than
# resource-scoped: writing a resource-scoped policy for a role whose job is to
# CREATE those resources is circular. It is deliberately not AdministratorAccess
# — the role cannot touch IAM users, access keys, Organizations, or the account
# itself, which is the class of action that turns a leaked deploy path into a
# lost account.
#
# It also cannot read an upload. `s3:*` would otherwise let it fetch a crash
# photograph, and it has no reason to: it CONFIGURES that bucket — policy,
# versioning, and encryption — and never reads an object out of it. The
# `NeverReadReportData` denial below covers the versioned actions too, because
# `s3:GetObjectVersion` is a DISTINCT IAM action from `s3:GetObject` and denying
# only the latter leaves every noncurrent version readable.
#
# SAME BUG CLASS, SECOND SERVICE: `rds:*` above lets this role manage the
# database, and RDS mirrors its logs into CloudWatch Logs
# (`enabled_cloudwatch_logs_exports` in database.tf) — the postgresql export can
# carry report narrative text, because `log_min_duration_statement` logs the
# statement TEXT for anything slower than a second. Denying `rds:*`'s own
# download actions is not enough: reading those SAME log groups through
# CloudWatch Logs' API (`logs:GetLogEvents`, `logs:FilterLogEvents`, and Logs
# Insights) is a completely different action namespace that `rds:*` never
# touched and `logs:*` above would otherwise grant. `NeverReadDatabaseLogs`
# closes it the same way the S3 fix did: name the other surface, not just the
# one this role happens to use on purpose.

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
        "application-autoscaling:*",
        "cloudfront:*",
        "cloudwatch:*",
        "ec2:*",
        "ecr:*",
        "ecs:*",
        "elasticloadbalancing:*",
        "events:*",
        "kms:*",
        "logs:*",
        "rds:*",
        "s3:*",
        "secretsmanager:*",
        "sns:*",
        "sqs:*",
        "tag:*"
      ],
      "Resource": "*"
    },
    {
      "Sid": "ManageServiceRolesOnly",
      "Effect": "Allow",
      "Action": [
        "iam:CreateRole",
        "iam:DeleteRole",
        "iam:GetRole",
        "iam:UpdateRole",
        "iam:PassRole",
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
        "iam:CreateServiceLinkedRole",
        "iam:CreatePolicy",
        "iam:DeletePolicy",
        "iam:GetPolicy",
        "iam:GetPolicyVersion",
        "iam:CreatePolicyVersion",
        "iam:DeletePolicyVersion",
        "iam:ListPolicyVersions",
        "iam:ListInstanceProfilesForRole",
        "iam:UpdateAssumeRolePolicy"
      ],
      "Resource": "*"
    },
    {
      "Sid": "ReadOnlyIdentity",
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
        "logs:GetQueryResults"
      ],
      "Resource": [
        "arn:aws:logs:${REGION}:${ACCOUNT_ID}:log-group:/aws/rds/instance/hpac-safety/postgresql:*",
        "arn:aws:logs:${REGION}:${ACCOUNT_ID}:log-group:/aws/rds/instance/hpac-safety/upgrade:*"
      ]
    },
    {
      "Sid": "NeverMintACredential",
      "Effect": "Deny",
      "Action": [
        "iam:CreateUser",
        "iam:CreateAccessKey",
        "iam:CreateLoginProfile",
        "iam:UpdateAccessKey",
        "iam:UpdateLoginProfile",
        "iam:CreateSAMLProvider",
        "iam:CreateOpenIDConnectProvider",
        "iam:DeleteOpenIDConnectProvider",
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
# ADR-0010 wants `terraform plan` posted on every pull request — it is the single
# most useful review artifact in the deployment story. But the deploy role above
# trusts ONLY ref:refs/heads/main, and a pull_request-triggered workflow presents
# the subject "repo:ORG/REPO:pull_request", not a branch ref. It cannot assume
# that role, and widening the deploy role so it could is exactly the thing the
# issue says not to do.
#
# So there are two roles. This one is assumable from a pull request and can read
# but not write, which is all a plan needs. See ADR-0032.
#
# Fork pull requests never reach it at all: GitHub withholds secrets and issues a
# read-only token for them, so the workflow has no role ARN to pass and skips.

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

# ReadOnlyAccess plus three explicit denials.
#
# ReadOnlyAccess on its own would let a plan running on ANY pull request — from
# any contributor with write access to a branch — read every object in the
# uploads bucket. Those objects are photographs of crash sites.
#
# The denial names the VERSIONED actions as well. `s3:GetObjectVersion` is a
# distinct IAM action from `s3:GetObject`, so denying only the latter would leave
# every noncurrent version readable by version id.
#
# SAME BUG CLASS, SECOND SERVICE: `ReadOnlyAccess` grants `logs:GetLogEvents` and
# `logs:FilterLogEvents`, which is normally exactly what a read-only role should
# have — except RDS mirrors its own logs into two of those log groups
# (`enabled_cloudwatch_logs_exports` in database.tf), and the postgresql export
# can carry report narrative text: `log_min_duration_statement` logs the
# statement TEXT for anything slower than a second. `NeverReadASecretValue`
# already denies RDS's *native* log-download API
# (`rds:DownloadDBLogFilePortion`); it does not touch CloudWatch Logs' API,
# which is a different action namespace reading the same data. Same shape as the
# `s3:GetObjectVersion` gap above: a Deny naming one API surface does not cover a
# different API surface that reaches the same underlying data. It would also
# grant secretsmanager:GetSecretValue on nothing (that action is not in
# ReadOnlyAccess), but the denial is written out anyway so the guarantee does not
# depend on AWS never widening a managed policy.
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
        "logs:GetQueryResults"
      ],
      "Resource": [
        "arn:aws:logs:${REGION}:${ACCOUNT_ID}:log-group:/aws/rds/instance/hpac-safety/postgresql:*",
        "arn:aws:logs:${REGION}:${ACCOUNT_ID}:log-group:/aws/rds/instance/hpac-safety/upgrade:*"
      ]
    },
    {
      "Sid": "NeverReadASecretValue",
      "Effect": "Deny",
      "Action": [
        "secretsmanager:GetSecretValue",
        "rds-data:*",
        "rds:DownloadDBLogFilePortion",
        "rds:DownloadCompleteDBLogFile"
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
# Encrypted and fully private because state contains deployment topology and
# access controls worth protecting.

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
  --tagging "TagSet=[{Key=${TAG_KEY},Value=${TAG_VALUE}}]" >/dev/null

say '     versioning, encryption, public-access block, TLS-only policy applied'

# --------------------------------------------------------------------------
# Done
# --------------------------------------------------------------------------

say ''
say 'Bootstrap complete. Next:'
say ''
say "  gh secret set AWS_DEPLOY_ROLE_ARN --repo ${GITHUB_ORG}/${GITHUB_REPO} --body <the ARN printed below>"
say "  gh secret set AWS_PLAN_ROLE_ARN   --repo ${GITHUB_ORG}/${GITHUB_REPO} --body ${PLAN_ROLE_ARN}"
say "  gh variable set AWS_REGION --repo ${GITHUB_ORG}/${GITHUB_REPO} --body ${REGION}"
say "  gh variable set TF_STATE_BUCKET --repo ${GITHUB_ORG}/${GITHUB_REPO} --body ${STATE_BUCKET}"
say ''
say "infra/backend.tf is a partial configuration: the bucket name carries the account"
say "id, so it is supplied at init time from TF_STATE_BUCKET rather than committed."
say 'The key and region are fixed in that file, and the state lock is an object in'
say 'this same bucket rather than a table in another service.'
say ''

# The one thing on stdout: the deploy role ARN, so the script stays pipeable.
# The plan role ARN is in the instructions above, on stderr.
printf '%s\n' "$ROLE_ARN"
