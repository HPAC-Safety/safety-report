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
# On success it prints six values on stdout, one per line, as NAME=value - all
# GitHub *variables* (never secrets). The first four are what H3 in issue #30
# copies into that account's GitHub *environment* (hpac-safety-staging or
# hpac-safety-production); the last two repeat AWS_PLAN_ROLE_ARN and
# TF_STATE_BUCKET's values under repository-scoped names, because
# terraform.yml's pull-request plan job cannot read an environment's
# variables - see ADR-0164 and docs/deployment.md "Required GitHub
# configuration":
#
#   AWS_DEPLOY_ROLE_ARN=...
#   AWS_PLAN_ROLE_ARN=...
#   TF_STATE_BUCKET=...
#   AWS_ACCOUNT_ID=...
#   AWS_PLAN_ROLE_ARN_STAGING=...    (or _PRODUCTION, matching the argument)
#   TF_STATE_BUCKET_STAGING=...      (or _PRODUCTION)
#
# Progress, warnings, and errors go to stderr, so the six lines above can be
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
  staging) GITHUB_ENVIRONMENT='hpac-safety-staging' ;;
  production) GITHUB_ENVIRONMENT='hpac-safety-production' ;;
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
# presents the subject `repo:ORG@ID/REPO@ID:environment:<name>` for such a job, never
# a `ref:` subject (see the OIDC trust below).
GITHUB_ORG='HPAC-Safety'
GITHUB_REPO='safety-report'

# This repository uses GitHub's immutable OIDC subject: every token names the
# organization and repository by name AND numeric ID,
# `repo:HPAC-Safety@307760008/safety-report@1341995834:<context>`
# (`gh api repos/HPAC-Safety/safety-report/actions/oidc/customization/sub`).
# The IDs are why the trust below cannot be met by an organization or
# repository re-created under the same name after a rename or deletion. A
# trust naming only `repo:HPAC-Safety/safety-report:...` matches no token this
# repository issues (#612, ADR-0167).
GITHUB_ORG_ID='307760008'
GITHUB_REPO_ID='1341995834'
OIDC_SUBJECT_PREFIX="repo:${GITHUB_ORG}@${GITHUB_ORG_ID}/${GITHUB_REPO}@${GITHUB_REPO_ID}"

OIDC_HOST='token.actions.githubusercontent.com'
OIDC_AUDIENCE='sts.amazonaws.com'

ROLE_NAME='hpac-safety-deploy'
POLICY_NAME='hpac-safety-deploy'
PLAN_ROLE_NAME='hpac-safety-plan'
PLAN_POLICY_NAME='hpac-safety-plan'
# The deploy role's policy is split across four managed policies -
# AWS limits a single managed policy to 6144 characters, and a policy this
# thorough about tag/name scoping does not fit in one. See the comment
# table above DEPLOY_POLICY_CORE below for what each one is for.
POLICY_NAME_IAM='hpac-safety-deploy-iam'
POLICY_NAME_SERVICES='hpac-safety-deploy-services'
POLICY_NAME_GUARDRAILS='hpac-safety-deploy-guardrails'
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

# converge_managed_policy NAME ARN DOCUMENT DESCRIPTION
#   Creates the customer-managed policy NAME if it does not exist, or - since a
#   managed policy holds at most five versions - deletes every non-default
#   version and publishes DOCUMENT as a new default version if it does. Used
#   for every policy below; there are five now instead of two, so this is no
#   longer worth inlining five times.
converge_managed_policy() {
  policy_name=$1
  policy_arn=$2
  policy_document=$3
  policy_description=$4
  if aws iam get-policy --policy-arn "$policy_arn" >/dev/null 2>&1; then
    # shellcheck disable=SC2016  # JMESPath literal backticks, not shell expansion.
    query='Versions[?IsDefaultVersion==`false`].VersionId'
    for version in $(aws iam list-policy-versions --policy-arn "$policy_arn" \
                       --query "$query" --output text); do
      aws iam delete-policy-version --policy-arn "$policy_arn" --version-id "$version" >/dev/null
    done
    aws iam create-policy-version \
      --policy-arn "$policy_arn" \
      --policy-document "$policy_document" \
      --set-as-default >/dev/null
    say "     ${policy_name}: already exists; new default version published"
  else
    aws iam create-policy \
      --policy-name "$policy_name" \
      --description "$policy_description" \
      --policy-document "$policy_document" \
      --tags "Key=${TAG_KEY},Value=${TAG_VALUE}" >/dev/null
    say "     ${policy_name}: created"
  fi
}

# attach_policy_if_missing ROLE_NAME POLICY_ARN
attach_policy_if_missing() {
  role_name=$1
  policy_arn=$2
  if aws iam list-attached-role-policies --role-name "$role_name" \
       --query 'AttachedPolicies[].PolicyArn' --output text | grep -qF "$policy_arn"; then
    return 0
  fi
  aws iam attach-role-policy --role-name "$role_name" --policy-arn "$policy_arn" >/dev/null
}

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
POLICY_ARN_IAM="arn:aws:iam::${ACCOUNT_ID}:policy/${POLICY_NAME_IAM}"
POLICY_ARN_SERVICES="arn:aws:iam::${ACCOUNT_ID}:policy/${POLICY_NAME_SERVICES}"
POLICY_ARN_GUARDRAILS="arn:aws:iam::${ACCOUNT_ID}:policy/${POLICY_NAME_GUARDRAILS}"
PLAN_ROLE_ARN="arn:aws:iam::${ACCOUNT_ID}:role/${PLAN_ROLE_NAME}"
PLAN_POLICY_ARN="arn:aws:iam::${ACCOUNT_ID}:policy/${PLAN_POLICY_NAME}"

say "Account ${ACCOUNT_ID}, region ${REGION}, environment ${ENVIRONMENT} (GitHub environment ${GITHUB_ENVIRONMENT}), as ${CALLER_ARN}"
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
# Trusts EXACTLY this repository's `environment: <GITHUB_ENVIRONMENT>` job,
# and nothing else - not a branch ref, not a tag, not a pull request, not
# another repository, not another environment. GITHUB_ENVIRONMENT
# (hpac-safety-staging / hpac-safety-production) is the GitHub environment's
# own name, distinct from ENVIRONMENT (staging / production), which names the AWS
# side: the Environment tag and the Terraform state key. `aud` and `sub` are
# both StringEquals, never StringLike: a wildcard here is exactly the gap
# that would let a workflow running under any OTHER GitHub environment (or
# none) assume this role.

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
          "${OIDC_HOST}:sub": "${OIDC_SUBJECT_PREFIX}:environment:${GITHUB_ENVIRONMENT}"
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
    --description "Assumed by GitHub Actions (environment: ${GITHUB_ENVIRONMENT}) via OIDC to run Terraform and deploy. Created by infra/bootstrap.sh." \
    --max-session-duration 3600 \
    --assume-role-policy-document "$TRUST_POLICY" \
    --tags "Key=${TAG_KEY},Value=${TAG_VALUE}" "Key=Environment,Value=${ENVIRONMENT}" >/dev/null
  say '     created'
fi

# --------------------------------------------------------------------------
# 2b. The policies the role carries
# --------------------------------------------------------------------------
#
# Terraform manages the whole environment, so most of this is service-scoped
# rather than resource-scoped: writing a resource-scoped policy for a role
# whose job is to CREATE those resources is circular. It is deliberately not
# AdministratorAccess - the role cannot touch IAM users, access keys,
# Organizations, or the account itself, which is the class of action that
# turns a leaked deploy path into a lost account.
#
# THE STAGING ACCOUNT IS THE OWNER'S OWN PERSONAL AWS ACCOUNT, AND ALSO RUNS
# OTHER, UNRELATED WORKLOADS. A tag condition that only checks
# StringEqualsIfExists on aws:ResourceTag/Project does NOT guard anything: it
# passes whenever the resource carries no Project tag at all, which is every
# resource belonging to those other workloads. Four independent guards close
# that gap instead:
#
#   1. NAME SCOPING wherever a service gives its resources predictable,
#      HPAC-Safety-only names: secrets under secret:hpac-safety-*, functions
#      under function:hpac-safety-* / layer:hpac-safety-*, ECR repositories
#      under repository/hpac-safety-*, SNS topics under hpac-safety-*, event
#      rules under rule/hpac-safety-*, schedules under
#      schedule/*/hpac-safety-*, our own log groups under
#      /aws/lambda/hpac-safety-* and /aws/rds/instance/hpac-safety*, and - now
#      that #591 renamed the AWS grouping to match this account's own GitHub
#      environment name - the Resource Group under
#      group/${GITHUB_ENVIRONMENT}. These services get their OWN Allow
#      statements below (DEPLOY_POLICY_CORE / DEPLOY_POLICY_SERVICES), scoped
#      to those ARN patterns instead of "*", so the tag condition is not the
#      only thing standing between this role and another app's
#      identically-typed resource.
#
#      THE APPREGISTRY APPLICATION IS NOT SIMILARLY SCOPED, even though #591
#      also renamed it to hpac-safety-<environment>: its ARN addresses the
#      application by an AWS-assigned opaque id
#      (arn:...:servicecatalog:.../applications/app-xxxxxxxxxxxx), never by
#      the human-chosen name, and that id does not exist until
#      CreateApplication has already run. There is no ARN pattern to write in
#      advance, so guard 3's tag condition remains the only enforceable scope
#      for it - see CreateOnlyAsOurProject below.
#   2. READ-ONLY METADATA everywhere else (Describe*/List*/most Get*) is
#      allowed broadly: configuration facts, not data, and Terraform needs
#      them to plan a diff against services that DON'T have a name pattern to
#      scope by (ACM, Auto Scaling, CloudFront, CloudWatch, EC2, RDS,
#      AppRegistry). Resource Groups' own Get*/List* moved to
#      ManageOurResourceGroupOnly below, alongside its CreateGroup, once the
#      group name itself became scopable; `resource-groups:SearchResources`
#      stays here because it queries by tag/type, not by an existing group's
#      name.
#   3. CREATE is allowed for those un-scopable services only with
#      aws:RequestTag/Project=HPAC-Safety - a real StringEquals, not
#      IfExists, so an untagged create request is refused, not merely
#      unchecked (DEPLOY_POLICY_SERVICES).
#   4. EVERY MUTATING VERB (Delete/Modify/Update/Put/Stop/Start/Reboot/
#      Terminate/Attach/Detach/Associate/Disassociate/Authorize/Revoke, and
#      removing an existing tag) is denied outright unless the resource is
#      ALREADY tagged Project=HPAC-Safety - StringNotEquals with no IfExists,
#      so a resource with no tag at all is denied exactly like a resource
#      tagged for someone else's project (DEPLOY_POLICY_GUARDRAILS). Create
#      verbs are deliberately absent from this list: a brand-new resource
#      cannot be tagged yet at the moment it is created, so gating creation
#      itself on aws:ResourceTag would be circular. Guard 3 covers creation;
#      this guards everything after it.
#
# Two narrow, documented exceptions to guard 4, because they touch a resource
# that legitimately has no HPAC-Safety tag and never will:
#   - `ec2:RunInstances` also names the `fck-nat` AMI (#465) as a resource in
#     the same call. That AMI is a public image someone else publishes and
#     tags however they like; RunInstances is a Create verb, so it is not in
#     the guardrails deny list at all, and is governed solely by guard 3
#     (allowed only with our own RequestTag on the instance/volume being
#     created).
#   - `ec2:CreateNetworkInterface`, and the handful of ATTACH/ASSOCIATE/
#     AUTHORIZE calls that wire a brand-new resource to the VPC/subnet it was
#     just created in (`EstablishNetworkAttachmentsOnResourcesWeJustCreated`
#     in DEPLOY_POLICY_SERVICES), are allowed unconditionally by that
#     statement - but that is not the last word: every one of those same
#     verbs (`Attach*`/`Associate*`/`Modify*`/`Authorize*`/`Revoke*`) is ALSO
#     in the guardrails deny list, so guard 4 still applies on top and
#     requires the resource on the other end (a subnet, a route table, a
#     security group) to already carry `Project=HPAC-Safety` - exactly the
#     tag THIS role's own `CreateOnlyAsOurProject`/`TagOnlyAtEc2CreationTime`
#     put there moments earlier. Nothing in this family is actually excluded
#     from guard 4; the two statements together are what let a legitimate
#     wiring call through while still refusing to wire up someone else's
#     untagged network resource. `ec2:CreateRoute` is the one action here
#     that is NOT guardrail-covered by a verb wildcard (`Create*` is
#     deliberately never a guardrails prefix, to keep guard 3/4 non-circular
#     for genuine creation) - it is named explicitly in the guardrails deny
#     list instead, because unlike every other `Create*` action it mutates an
#     EXISTING route table rather than creating a new resource.
#
# TAG-HIJACK CLOSED: creating a tag is not the same as creating a resource.
# `aws:RequestTag` alone, with Resource "*", would let this role attach
# `Project=HPAC-Safety` to ANY existing resource in the account - including
# another application's untagged one - after which guard 4 would treat it as
# ours and let it be modified or deleted. Three different fixes close this,
# chosen per service:
#   - `ec2:CreateTags` is allowed only when AWS's own `ec2:CreateAction`
#     context key names one of the specific EC2 create calls above
#     (`TagOnlyAtEc2CreationTime`) - that key is populated ONLY when tagging
#     is bundled into a genuine create request, never for a standalone
#     `CreateTags` call, so it cannot be pointed at an existing resource.
#   - `rds:AddTagsToResource` and `autoscaling:CreateOrUpdateTags`/
#     `DeleteTags` have no equivalent context key, so they are scoped by ARN
#     instead, to names only THIS role's own Terraform would ever choose
#     (`db:hpac-safety*` and siblings; `autoScalingGroupName/hpac-safety*`) -
#     `ManageOurDatabaseTagsOnly`, `ManageOurAutoScalingGroupTagsOnly`.
#   - `acm:AddTagsToCertificate`, `cloudfront:TagResource`,
#     `cloudwatch:TagResource`, `servicecatalog:TagResource`, and
#     `resource-groups:Tag`: AWS authorizes these on every create call that
#     carries tags, so the first real apply refused them (#626, ADR-0169).
#     They are allowed only when the tag being added is Project=HPAC-Safety
#     (`TagAtCreationAsOurProject`; alarms also by name in
#     `TagOurAlarmsOnly`; the group by name in `ManageOurResourceGroupOnly`),
#     and `NeverRetagAnotherProjectsResource` denies them on a resource
#     tagged for another project. ACCEPTED RESIDUAL RISK (ADR-0169): an
#     UNTAGGED ACM certificate, CloudFront distribution, or AppRegistry
#     application of another workload could be tagged ours and then changed
#     - their ARNs are opaque, so no name scope exists. The alarm and the
#     group are name-scoped, so they carry no such risk.
#
# UPDATES TO WHAT IS OURS. `ManageWhatIsAlreadyTaggedOurs` allows every
# ACM, Auto Scaling, CloudFront, CloudWatch, EC2, RDS, and AppRegistry action
# on a resource already tagged Project=HPAC-Safety - parameter-group
# parameters, a NAT ENI's source_dest_check, the release's
# StartInstanceRefresh and CreateInvalidation, and every later change. The
# four CloudFront configuration types that cannot carry a tag
# (`CreateCloudFrontConfigThatCannotBeTagged`) are created untagged; the
# guardrail still refuses updating or deleting them.
#
# DATA, NOT JUST RESOURCES, MUST STAY OUT OF REACH. Guard 1-4 stop this role
# from CHANGING another workload's resources, but several read-only "Get" and
# "list" style calls in these same services return the workload's DATA, not
# its configuration, and a Describe*/List*/Get* allow-everything statement
# would otherwise hand that over even though nothing was ever "mutated":
#   - `s3:GetObject*` returns object bytes - scoped to hpac-safety-* buckets
#     only, with an explicit deny on everything else
#     (NeverReadDataOutsideOurBuckets);
#   - `secretsmanager:GetSecretValue` returns a plaintext secret - denied
#     outright, everywhere, including our OWN secrets. This role WRITES
#     secret values (H4/#465 copies GitHub secrets into Secrets Manager); it
#     never needs to read one back;
#   - `lambda:GetFunction`/`GetFunctionConfiguration` return a function's
#     environment variables, and `InvokeFunction` runs it - scoped to our own
#     functions, denied for everything else;
#   - `ecr:GetDownloadUrlForLayer`/`BatchGetImage` return container image
#     layers - scoped to our own repositories, denied for everything else;
#   - `logs:GetLogEvents`/`FilterLogEvents`/`StartQuery`/`GetQueryResults`
#     return log line content, which for OUR OWN RDS log group can carry
#     report narrative text (`log_min_duration_statement`) - denied
#     outright, everywhere, ours included; this role manages log groups, it
#     never reads their content;
#   - `ssm:GetParameter*`, `dynamodb:GetItem`/`Query`/`Scan`,
#     `kinesis:GetRecords`, `sqs:ReceiveMessage` are services this role has no
#     legitimate reason to read at all - denied outright, everywhere, since
#     this account may hold other applications' parameters, tables, streams,
#     and queues.
#
# KMS IS THE SAME SHAPE OF PROBLEM AGAIN, ONE LEVEL DOWN: `kms:Decrypt` on
# Resource "*" would let this role decrypt anything encrypted under ANY
# customer-managed key in the account, including another application's,
# because a KMS key's OWN key policy - not this IAM policy - is usually what
# delegates decrypt permission to "any principal IAM allows," and this
# account's other applications' keys are outside this policy's sight
# entirely. Three conditions close it:
#   - `Decrypt`/`Encrypt`/`GenerateDataKey*` only with `kms:ViaService`
#     naming the services this system actually uses (RDS, Secrets Manager,
#     S3, Lambda, Logs, ECR) - so the call must be AWS calling KMS on this
#     role's behalf as part of one of those services' own API calls, never
#     this role calling KMS directly;
#   - `CreateGrant`/`ListGrants`/`RevokeGrant` only with
#     `kms:GrantIsForAWSResource: true` - a grant naming this role's own
#     credentials as the grantee, rather than a third party, is refused;
#   - `NeverUseKmsOutsideOurServices` denies all four of the above outright
#     whenever `kms:ViaService` is absent (a `Null` condition), so a direct
#     KMS API call - the one case the `ViaService` allow-condition above
#     does not need to consider, because it is not how AWS services request
#     it - is refused regardless of anything else the policy grants.
#     `DescribeKey`/`ListAliases` stay unconditional: metadata, not data.
#
# ONLY A REAL APPLY EXERCISES THIS. Nothing in this repository can call AWS;
# CI checks it by JSON validation and shellcheck. The first staging apply
# found the gaps #626 fixed; a further one is fixed the same way.
#
# Statement -> intent, at a glance (full detail is in the JSON below):
#
#   DEPLOY_POLICY_CORE (hpac-safety-deploy)
#     TerraformState                         read/write our own state object
#     ManageOurBucketsOnly                   manage hpac-safety-* buckets
#     NeverReadReportData                    deny: read an upload, even ours
#     NeverReadDataOutsideOurBuckets          deny: read any object elsewhere
#     ManageOurSecretsOnly                   manage hpac-safety-* / hpac-safety/* secrets
#     LetRdsCreateOurDatabasesMasterSecret   RDS-managed rds!db-* master secret
#     NeverReadASecretValueEvenOurOwn        deny: GetSecretValue, anywhere
#     ManageOurFunctionsOnly                 manage hpac-safety-* functions
#     ManageOurRepositoriesOnly              manage hpac-safety-* ECR repos
#     ManageOurTopicsOnly                    manage hpac-safety-* SNS topics
#     ManageOurEventRulesOnly                manage hpac-safety-* EventBridge
#     ManageOurSchedulesOnly                 manage hpac-safety-* schedules
#     ManageOurLogGroupsOnly                 manage our own log groups
#     NeverReadLogContent                    deny: read log/RDS-log content
#     NeverReadOtherFunctionsOrRepos          deny: Get/Invoke outside ours
#     NeverReadOtherApplicationsData          deny: ssm/dynamodb/kinesis/sqs
#     ManageOurDatabaseTagsOnly              tag only db/subgrp/pg/snapshot/cluster:hpac-safety*
#     ManageOurAutoScalingGroupTagsOnly      tag only autoScalingGroupName/hpac-safety*
#
#   DEPLOY_POLICY_IAM (hpac-safety-deploy-iam)
#     ManageOurRolesOnly/PoliciesOnly         IAM scoped to hpac-safety-*
#     ManageOurInstanceProfilesOnly           instance-profile/hpac-safety-* (NAT instance)
#     PassOnlyOurRolesToOurServices           PassRole to lambda/scheduler/ec2 only
#     ServiceLinkedRolesAwsRequiresByFixedName the two SLRs AWS names itself
#     ReadOnlyIdentityAndApplicationRegistration sts/OIDC-provider read
#     NeverMintACredentialOrChangeFederation  deny: users/keys/OIDC/org/account
#     NeverEditItsOwnPrivileges               deny: editing its own role
#
#   DEPLOY_POLICY_SERVICES (hpac-safety-deploy-services)
#     ReadOnlyMetadata                       Describe/List/Get for un-scopable services
#     ManageOurResourceGroupOnly             manage group/${GITHUB_ENVIRONMENT} only (name-scoped, not tag-only)
#     CreateOnlyAsOurProject                 Create* only with our RequestTag
#     CreateCloudFrontConfigThatCannotBeTagged  OAC/headers/cache policy/function, untagged
#     TagAtCreationAsOurProject              acm/cloudfront/servicecatalog tag-add, only our tag
#     TagOurAlarmsOnly                       cloudwatch tag-add on alarm:hpac-safety-*
#     ManageWhatIsAlreadyTaggedOurs          any action on a resource tagged ours
#     TagOnlyAtEc2CreationTime               ec2:CreateTags gated on ec2:CreateAction
#     EstablishNetworkAttachmentsOnResourcesWeJustCreated  wiring calls, still guardrail-gated (see above)
#     UseTheAwsManagedKeysViaOurServicesOnly  Decrypt/Encrypt/GenerateDataKey* via our services only
#     CreateGrantsForAwsResourcesOnly         CreateGrant/ListGrants/RevokeGrant for AWS-resource grants only
#     DescribeAndListAllKeys                 KMS metadata, unconditional
#     NeverUseKmsOutsideOurServices           deny: any of the above without kms:ViaService
#
#   DEPLOY_POLICY_GUARDRAILS (hpac-safety-deploy-guardrails)
#     NeverMutateAnUntaggedResource          deny every mutating verb unless the
#                                             resource already carries Project=HPAC-Safety
#     NeverRetagAnotherProjectsResource      deny the five tag-add actions on a
#                                             resource tagged for another project

say '2/4  hpac-safety-deploy IAM role and policies'

DEPLOY_POLICY_CORE=$(cat <<JSON
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
      "Sid": "NeverReadDataOutsideOurBuckets",
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
      "NotResource": [
        "arn:aws:s3:::hpac-safety-*",
        "arn:aws:s3:::hpac-safety-*/*"
      ]
    },
    {
      "Sid": "ManageOurSecretsOnly",
      "Effect": "Allow",
      "Action": [
        "secretsmanager:Describe*",
        "secretsmanager:List*",
        "secretsmanager:CreateSecret",
        "secretsmanager:PutSecretValue",
        "secretsmanager:UpdateSecret",
        "secretsmanager:DeleteSecret",
        "secretsmanager:RestoreSecret",
        "secretsmanager:TagResource",
        "secretsmanager:UntagResource",
        "secretsmanager:PutResourcePolicy",
        "secretsmanager:DeleteResourcePolicy",
        "secretsmanager:GetResourcePolicy",
        "secretsmanager:RotateSecret",
        "secretsmanager:CancelRotateSecret"
      ],
      "Resource": [
        "arn:aws:secretsmanager:${REGION}:${ACCOUNT_ID}:secret:hpac-safety-*",
        "arn:aws:secretsmanager:${REGION}:${ACCOUNT_ID}:secret:hpac-safety/*"
      ]
    },
    {
      "Sid": "LetRdsCreateOurDatabasesMasterSecret",
      "Effect": "Allow",
      "Action": [
        "secretsmanager:CreateSecret",
        "secretsmanager:TagResource",
        "secretsmanager:DescribeSecret"
      ],
      "Resource": "arn:aws:secretsmanager:${REGION}:${ACCOUNT_ID}:secret:rds!db-*"
    },
    {
      "Sid": "NeverReadASecretValueEvenOurOwn",
      "Effect": "Deny",
      "Action": "secretsmanager:GetSecretValue",
      "Resource": "*"
    },
    {
      "Sid": "ManageOurFunctionsOnly",
      "Effect": "Allow",
      "Action": "lambda:*",
      "Resource": [
        "arn:aws:lambda:${REGION}:${ACCOUNT_ID}:function:hpac-safety-*",
        "arn:aws:lambda:${REGION}:${ACCOUNT_ID}:layer:hpac-safety-*"
      ]
    },
    {
      "Sid": "LambdaAccountLevelReadOnly",
      "Effect": "Allow",
      "Action": [
        "lambda:ListFunctions",
        "lambda:ListLayers",
        "lambda:GetAccountSettings"
      ],
      "Resource": "*"
    },
    {
      "Sid": "ManageOurRepositoriesOnly",
      "Effect": "Allow",
      "Action": "ecr:*",
      "Resource": "arn:aws:ecr:${REGION}:${ACCOUNT_ID}:repository/hpac-safety-*"
    },
    {
      "Sid": "EcrAuthTokenIsAccountLevelOnly",
      "Effect": "Allow",
      "Action": [
        "ecr:GetAuthorizationToken",
        "ecr:DescribeRegistry"
      ],
      "Resource": "*"
    },
    {
      "Sid": "ManageOurTopicsOnly",
      "Effect": "Allow",
      "Action": "sns:*",
      "Resource": "arn:aws:sns:${REGION}:${ACCOUNT_ID}:hpac-safety-*"
    },
    {
      "Sid": "ManageOurEventRulesOnly",
      "Effect": "Allow",
      "Action": "events:*",
      "Resource": [
        "arn:aws:events:${REGION}:${ACCOUNT_ID}:rule/hpac-safety-*",
        "arn:aws:events:${REGION}:${ACCOUNT_ID}:event-bus/default"
      ]
    },
    {
      "Sid": "ManageOurSchedulesOnly",
      "Effect": "Allow",
      "Action": "scheduler:*",
      "Resource": [
        "arn:aws:scheduler:${REGION}:${ACCOUNT_ID}:schedule/*/hpac-safety-*",
        "arn:aws:scheduler:${REGION}:${ACCOUNT_ID}:schedule-group/hpac-safety*"
      ]
    },
    {
      "Sid": "ManageOurLogGroupsOnly",
      "Effect": "Allow",
      "Action": [
        "logs:CreateLogGroup",
        "logs:DeleteLogGroup",
        "logs:PutRetentionPolicy",
        "logs:DeleteRetentionPolicy",
        "logs:PutSubscriptionFilter",
        "logs:DeleteSubscriptionFilter",
        "logs:AssociateKmsKey",
        "logs:DisassociateKmsKey",
        "logs:TagResource",
        "logs:UntagResource",
        "logs:TagLogGroup",
        "logs:UntagLogGroup",
        "logs:Describe*",
        "logs:List*",
        "logs:PutMetricFilter",
        "logs:DeleteMetricFilter"
      ],
      "Resource": [
        "arn:aws:logs:${REGION}:${ACCOUNT_ID}:log-group:/aws/lambda/hpac-safety-*",
        "arn:aws:logs:${REGION}:${ACCOUNT_ID}:log-group:/aws/lambda/hpac-safety-*:*",
        "arn:aws:logs:${REGION}:${ACCOUNT_ID}:log-group:/aws/rds/instance/hpac-safety*",
        "arn:aws:logs:${REGION}:${ACCOUNT_ID}:log-group:/aws/rds/instance/hpac-safety*:*"
      ]
    },
    {
      "Sid": "LogsAccountLevelReadOnly",
      "Effect": "Allow",
      "Action": [
        "logs:DescribeLogGroups",
        "logs:ListTagsForResource"
      ],
      "Resource": "*"
    },
    {
      "Sid": "NeverReadLogContent",
      "Effect": "Deny",
      "Action": [
        "logs:GetLogEvents",
        "logs:FilterLogEvents",
        "logs:GetLogRecord",
        "logs:StartQuery",
        "logs:GetQueryResults",
        "logs:StartLiveTail",
        "rds:DownloadDBLogFilePortion",
        "rds:DownloadCompleteDBLogFile"
      ],
      "Resource": "*"
    },
    {
      "Sid": "NeverReadOtherFunctionsOrRepos",
      "Effect": "Deny",
      "Action": [
        "lambda:GetFunction",
        "lambda:GetFunctionConfiguration",
        "lambda:GetFunctionUrlConfig",
        "lambda:GetLayerVersion",
        "lambda:GetLayerVersionByArn",
        "lambda:InvokeFunction",
        "lambda:InvokeAsync",
        "ecr:GetDownloadUrlForLayer",
        "ecr:BatchGetImage"
      ],
      "NotResource": [
        "arn:aws:lambda:${REGION}:${ACCOUNT_ID}:function:hpac-safety-*",
        "arn:aws:lambda:${REGION}:${ACCOUNT_ID}:layer:hpac-safety-*",
        "arn:aws:ecr:${REGION}:${ACCOUNT_ID}:repository/hpac-safety-*"
      ]
    },
    {
      "Sid": "NeverReadOtherApplicationsData",
      "Effect": "Deny",
      "Action": [
        "ssm:GetParameter",
        "ssm:GetParameters",
        "ssm:GetParametersByPath",
        "ssm:GetParameterHistory",
        "dynamodb:GetItem",
        "dynamodb:BatchGetItem",
        "dynamodb:Query",
        "dynamodb:Scan",
        "dynamodb:PartiQLSelect",
        "kinesis:GetRecords",
        "kinesis:GetShardIterator",
        "sqs:ReceiveMessage"
      ],
      "Resource": "*"
    },
    {
      "Sid": "ManageOurDatabaseTagsOnly",
      "Effect": "Allow",
      "Action": "rds:AddTagsToResource",
      "Resource": [
        "arn:aws:rds:${REGION}:${ACCOUNT_ID}:db:hpac-safety*",
        "arn:aws:rds:${REGION}:${ACCOUNT_ID}:subgrp:hpac-safety*",
        "arn:aws:rds:${REGION}:${ACCOUNT_ID}:pg:hpac-safety*",
        "arn:aws:rds:${REGION}:${ACCOUNT_ID}:snapshot:hpac-safety*",
        "arn:aws:rds:${REGION}:${ACCOUNT_ID}:cluster:hpac-safety*"
      ]
    },
    {
      "Sid": "ManageOurAutoScalingGroupTagsOnly",
      "Effect": "Allow",
      "Action": [
        "autoscaling:CreateOrUpdateTags",
        "autoscaling:DeleteTags"
      ],
      "Resource": "arn:aws:autoscaling:${REGION}:${ACCOUNT_ID}:autoScalingGroup:*:autoScalingGroupName/hpac-safety*"
    }
  ]
}
JSON
)

DEPLOY_POLICY_IAM=$(cat <<JSON
{
  "Version": "2012-10-17",
  "Statement": [
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
      "Sid": "ManageOurInstanceProfilesOnly",
      "Effect": "Allow",
      "Action": [
        "iam:CreateInstanceProfile",
        "iam:DeleteInstanceProfile",
        "iam:AddRoleToInstanceProfile",
        "iam:RemoveRoleFromInstanceProfile",
        "iam:GetInstanceProfile",
        "iam:TagInstanceProfile",
        "iam:UntagInstanceProfile",
        "iam:ListInstanceProfileTags"
      ],
      "Resource": "arn:aws:iam::${ACCOUNT_ID}:instance-profile/hpac-safety-*"
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
            "scheduler.amazonaws.com",
            "ec2.amazonaws.com"
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
            "autoscaling.amazonaws.com",
            "servicecatalog-appregistry.amazonaws.com"
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

DEPLOY_POLICY_SERVICES=$(cat <<JSON
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "ReadOnlyMetadata",
      "Effect": "Allow",
      "Action": [
        "acm:Describe*",
        "acm:List*",
        "acm:Get*",
        "autoscaling:Describe*",
        "cloudfront:Describe*",
        "cloudfront:List*",
        "cloudfront:Get*",
        "cloudwatch:Describe*",
        "cloudwatch:List*",
        "cloudwatch:GetMetricData",
        "cloudwatch:GetMetricStatistics",
        "cloudwatch:GetDashboard",
        "cloudwatch:GetInsightRuleReport",
        "ec2:Describe*",
        "rds:Describe*",
        "rds:List*",
        "resource-groups:SearchResources",
        "servicecatalog:Get*",
        "servicecatalog:List*",
        "servicecatalog:Search*",
        "tag:Get*"
      ],
      "Resource": "*"
    },
    {
      "Sid": "ManageOurResourceGroupOnly",
      "Effect": "Allow",
      "Action": [
        "resource-groups:CreateGroup",
        "resource-groups:GetGroup",
        "resource-groups:GetGroupConfiguration",
        "resource-groups:GetGroupQuery",
        "resource-groups:GetTags",
        "resource-groups:ListGroupResources",
        "resource-groups:PutGroupConfiguration",
        "resource-groups:Tag",
        "resource-groups:Untag",
        "resource-groups:UpdateGroup",
        "resource-groups:UpdateGroupQuery",
        "resource-groups:DeleteGroup"
      ],
      "Resource": "arn:aws:resource-groups:${REGION}:${ACCOUNT_ID}:group/${GITHUB_ENVIRONMENT}",
      "Condition": {
        "StringEqualsIfExists": {
          "aws:ResourceTag/${TAG_KEY}": "${TAG_VALUE}",
          "aws:RequestTag/${TAG_KEY}": "${TAG_VALUE}"
        }
      }
    },
    {
      "Sid": "CreateOnlyAsOurProject",
      "Effect": "Allow",
      "Action": [
        "acm:RequestCertificate",
        "autoscaling:CreateAutoScalingGroup",
        "autoscaling:CreateLaunchConfiguration",
        "cloudfront:CreateDistribution*",
        "cloudwatch:PutMetricAlarm",
        "cloudwatch:PutDashboard",
        "ec2:CreateVpc",
        "ec2:CreateSubnet",
        "ec2:CreateSecurityGroup",
        "ec2:CreateRouteTable",
        "ec2:CreateInternetGateway",
        "ec2:CreateNatGateway",
        "ec2:CreateLaunchTemplate",
        "ec2:CreateNetworkInterface",
        "ec2:AllocateAddress",
        "ec2:CreateVpcEndpoint",
        "ec2:CreateFlowLogs",
        "ec2:RunInstances",
        "rds:CreateDBInstance",
        "rds:CreateDBSubnetGroup",
        "rds:CreateDBParameterGroup",
        "servicecatalog:CreateApplication",
        "servicecatalog:CreateAttributeGroup",
        "servicecatalog:AssociateResource",
        "servicecatalog:AssociateAttributeGroup"
      ],
      "Resource": "*",
      "Condition": {
        "StringEquals": {
          "aws:RequestTag/${TAG_KEY}": "${TAG_VALUE}"
        }
      }
    },
    {
      "Sid": "CreateCloudFrontConfigThatCannotBeTagged",
      "Effect": "Allow",
      "Action": [
        "cloudfront:CreateOriginAccessControl",
        "cloudfront:CreateResponseHeadersPolicy",
        "cloudfront:CreateCachePolicy",
        "cloudfront:CreateFunction"
      ],
      "Resource": "*"
    },
    {
      "Sid": "TagAtCreationAsOurProject",
      "Effect": "Allow",
      "Action": [
        "acm:AddTagsToCertificate",
        "cloudfront:TagResource",
        "servicecatalog:TagResource"
      ],
      "Resource": "*",
      "Condition": {
        "StringEquals": {
          "aws:RequestTag/${TAG_KEY}": "${TAG_VALUE}"
        }
      }
    },
    {
      "Sid": "TagOurAlarmsOnly",
      "Effect": "Allow",
      "Action": "cloudwatch:TagResource",
      "Resource": "arn:aws:cloudwatch:${REGION}:${ACCOUNT_ID}:alarm:hpac-safety-*",
      "Condition": {
        "StringEquals": {
          "aws:RequestTag/${TAG_KEY}": "${TAG_VALUE}"
        }
      }
    },
    {
      "Sid": "ManageWhatIsAlreadyTaggedOurs",
      "Effect": "Allow",
      "Action": [
        "acm:*",
        "autoscaling:*",
        "cloudfront:*",
        "cloudwatch:*",
        "ec2:*",
        "rds:*",
        "servicecatalog:*"
      ],
      "Resource": "*",
      "Condition": {
        "StringEquals": {
          "aws:ResourceTag/${TAG_KEY}": "${TAG_VALUE}"
        }
      }
    },
    {
      "Sid": "TagOnlyAtEc2CreationTime",
      "Effect": "Allow",
      "Action": "ec2:CreateTags",
      "Resource": "*",
      "Condition": {
        "StringEquals": {
          "aws:RequestTag/${TAG_KEY}": "${TAG_VALUE}",
          "ec2:CreateAction": [
            "RunInstances",
            "CreateVpc",
            "CreateSubnet",
            "CreateSecurityGroup",
            "CreateRouteTable",
            "CreateInternetGateway",
            "CreateLaunchTemplate",
            "CreateNetworkInterface",
            "AllocateAddress",
            "CreateVpcEndpoint",
            "CreateFlowLogs",
            "AuthorizeSecurityGroupIngress",
            "AuthorizeSecurityGroupEgress"
          ]
        }
      }
    },
    {
      "Sid": "EstablishNetworkAttachmentsOnResourcesWeJustCreated",
      "Effect": "Allow",
      "Action": [
        "ec2:AttachInternetGateway",
        "ec2:AssociateRouteTable",
        "ec2:AssociateSubnetCidrBlock",
        "ec2:AssociateAddress",
        "ec2:ModifySubnetAttribute",
        "ec2:ModifyVpcAttribute",
        "ec2:CreateRoute",
        "ec2:AuthorizeSecurityGroupIngress",
        "ec2:AuthorizeSecurityGroupEgress",
        "ec2:RevokeSecurityGroupIngress",
        "ec2:RevokeSecurityGroupEgress"
      ],
      "Resource": "*"
    },
    {
      "Sid": "UseTheAwsManagedKeysViaOurServicesOnly",
      "Effect": "Allow",
      "Action": [
        "kms:Decrypt",
        "kms:Encrypt",
        "kms:GenerateDataKey",
        "kms:GenerateDataKeyWithoutPlaintext"
      ],
      "Resource": "*",
      "Condition": {
        "StringEquals": {
          "kms:ViaService": [
            "rds.${REGION}.amazonaws.com",
            "secretsmanager.${REGION}.amazonaws.com",
            "s3.${REGION}.amazonaws.com",
            "lambda.${REGION}.amazonaws.com",
            "logs.${REGION}.amazonaws.com",
            "ecr.${REGION}.amazonaws.com"
          ]
        }
      }
    },
    {
      "Sid": "CreateGrantsForAwsResourcesOnly",
      "Effect": "Allow",
      "Action": [
        "kms:CreateGrant",
        "kms:ListGrants",
        "kms:RevokeGrant"
      ],
      "Resource": "*",
      "Condition": {
        "Bool": {
          "kms:GrantIsForAWSResource": "true"
        }
      }
    },
    {
      "Sid": "DescribeAndListAllKeys",
      "Effect": "Allow",
      "Action": [
        "kms:DescribeKey",
        "kms:ListAliases"
      ],
      "Resource": "*"
    },
    {
      "Sid": "NeverUseKmsOutsideOurServices",
      "Effect": "Deny",
      "Action": [
        "kms:Decrypt",
        "kms:Encrypt",
        "kms:GenerateDataKey",
        "kms:GenerateDataKeyWithoutPlaintext",
        "kms:CreateGrant"
      ],
      "Resource": "*",
      "Condition": {
        "Null": {
          "kms:ViaService": "true"
        }
      }
    }
  ]
}
JSON
)

DEPLOY_POLICY_GUARDRAILS=$(cat <<JSON
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "NeverMutateAnUntaggedResource",
      "Effect": "Deny",
      "Action": [
        "acm:Delete*",
        "acm:Import*",
        "acm:Renew*",
        "acm:Resend*",
        "acm:Update*",
        "acm:RemoveTagsFromCertificate",
        "autoscaling:Delete*",
        "autoscaling:Update*",
        "autoscaling:Suspend*",
        "autoscaling:Resume*",
        "autoscaling:Set*",
        "autoscaling:Terminate*",
        "autoscaling:Attach*",
        "autoscaling:Detach*",
        "autoscaling:Enter*",
        "autoscaling:Exit*",
        "autoscaling:Execute*",
        "autoscaling:Put*",
        "cloudfront:Delete*",
        "cloudfront:Update*",
        "cloudfront:UntagResource",
        "cloudwatch:Delete*",
        "cloudwatch:Set*",
        "cloudwatch:Disable*",
        "cloudwatch:Enable*",
        "cloudwatch:UntagResource",
        "ec2:Delete*",
        "ec2:Modify*",
        "ec2:Terminate*",
        "ec2:Stop*",
        "ec2:Start*",
        "ec2:Reboot*",
        "ec2:Attach*",
        "ec2:Detach*",
        "ec2:Associate*",
        "ec2:Disassociate*",
        "ec2:Authorize*",
        "ec2:Revoke*",
        "ec2:ReplaceRoute*",
        "ec2:ResetInstanceAttribute",
        "ec2:DeleteTags",
        "ec2:CreateRoute",
        "rds:Delete*",
        "rds:Modify*",
        "rds:Stop*",
        "rds:Start*",
        "rds:Reboot*",
        "rds:Restore*",
        "rds:CreateDBSnapshot",
        "rds:CopyDBSnapshot",
        "rds:ShareDBSnapshot",
        "rds:PromoteReadReplica*",
        "rds:Failover*",
        "rds:RemoveFromGlobalCluster",
        "rds:RemoveTagsFromResource",
        "rds:PurchaseReservedDBInstancesOffering",
        "resource-groups:Delete*",
        "resource-groups:Update*",
        "resource-groups:Untag",
        "servicecatalog:Delete*",
        "servicecatalog:Update*",
        "servicecatalog:Disassociate*",
        "servicecatalog:UntagResource",
        "secretsmanager:Delete*",
        "secretsmanager:Update*",
        "secretsmanager:Restore*",
        "secretsmanager:UntagResource",
        "secretsmanager:PutResourcePolicy",
        "secretsmanager:DeleteResourcePolicy",
        "lambda:Delete*",
        "lambda:Update*",
        "lambda:Put*",
        "lambda:Remove*",
        "lambda:UntagResource",
        "ecr:Delete*",
        "ecr:Put*",
        "ecr:BatchDeleteImage",
        "ecr:SetRepositoryPolicy",
        "ecr:UntagResource",
        "sns:Delete*",
        "sns:Set*",
        "sns:Unsubscribe",
        "sns:UntagResource",
        "events:Delete*",
        "events:Put*",
        "events:Remove*",
        "events:Disable*",
        "events:Enable*",
        "events:Deactivate*",
        "events:Activate*",
        "events:UntagResource",
        "scheduler:Delete*",
        "scheduler:Update*",
        "scheduler:UntagResource",
        "logs:Delete*",
        "logs:Put*",
        "logs:UntagResource",
        "logs:UntagLogGroup",
        "logs:DisassociateKmsKey"
      ],
      "Resource": "*",
      "Condition": {
        "StringNotEquals": {
          "aws:ResourceTag/${TAG_KEY}": "${TAG_VALUE}"
        }
      }
    },
    {
      "Sid": "NeverRetagAnotherProjectsResource",
      "Effect": "Deny",
      "Action": [
        "acm:AddTagsToCertificate",
        "cloudfront:TagResource",
        "cloudwatch:TagResource",
        "resource-groups:Tag",
        "servicecatalog:TagResource"
      ],
      "Resource": "*",
      "Condition": {
        "Null": {
          "aws:ResourceTag/${TAG_KEY}": "false"
        },
        "StringNotEquals": {
          "aws:ResourceTag/${TAG_KEY}": "${TAG_VALUE}"
        }
      }
    }
  ]
}
JSON
)

converge_managed_policy "$POLICY_NAME" "$POLICY_ARN" "$DEPLOY_POLICY_CORE" \
  'What hpac-safety-deploy may manage by name/ARN. Created by infra/bootstrap.sh.'
converge_managed_policy "$POLICY_NAME_IAM" "$POLICY_ARN_IAM" "$DEPLOY_POLICY_IAM" \
  'IAM/identity portion of what hpac-safety-deploy may do. Created by infra/bootstrap.sh.'
converge_managed_policy "$POLICY_NAME_SERVICES" "$POLICY_ARN_SERVICES" "$DEPLOY_POLICY_SERVICES" \
  'Read-only and create-with-tag grants for services with no HPAC-Safety-only name pattern. Created by infra/bootstrap.sh.'
converge_managed_policy "$POLICY_NAME_GUARDRAILS" "$POLICY_ARN_GUARDRAILS" "$DEPLOY_POLICY_GUARDRAILS" \
  'Denies every mutating action on a resource not tagged Project=HPAC-Safety. Created by infra/bootstrap.sh.'

for arn in "$POLICY_ARN" "$POLICY_ARN_IAM" "$POLICY_ARN_SERVICES" "$POLICY_ARN_GUARDRAILS"; do
  attach_policy_if_missing "$ROLE_NAME" "$arn"
done
say '     four policies converged and attached'

# --------------------------------------------------------------------------
# 3. The plan role
# --------------------------------------------------------------------------
#
# ADR-0010 wants `terraform plan` posted on every pull request - it is the
# single most useful review artifact in the deployment story. But the deploy
# role above trusts ONLY this repository's `environment:` jobs, and a
# pull_request-triggered workflow presents the subject
# "<OIDC_SUBJECT_PREFIX>:pull_request", not an environment subject. It cannot assume
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
          "${OIDC_HOST}:sub": "${OIDC_SUBJECT_PREFIX}:pull_request"
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

# ReadOnlyAccess plus reading Terraform state, minus explicit denials.
#
# ReadOnlyAccess is the AWS managed policy for "can read almost everything in
# the account" - which is exactly the problem in a shared, unrelated-workloads
# staging account. It grants, among many other things:
#   - `s3:GetObject` on every bucket, not just the state bucket - denied for
#     everything except the state bucket (NeverReadDataOutsideOurState) and
#     the uploads bucket is denied explicitly too (NeverReadReportData),
#     since `s3:GetObjectVersion` is a DISTINCT action from `s3:GetObject` and
#     denying only the latter would leave every noncurrent version readable -
#     including, between the two hops of the quarantine lifecycle rule, an
#     upload that failed validation;
#   - `lambda:GetFunction`/`GetFunctionConfiguration` (environment variables)
#     and `ecr:GetDownloadUrlForLayer`/`BatchGetImage` (image layers) on every
#     function/repository in the account - denied outside our own
#     (NeverReadOtherFunctionsOrRepos);
#   - `logs:GetLogEvents`/`FilterLogEvents` - which for OUR OWN RDS log group
#     can carry report narrative text (`log_min_duration_statement`) - denied
#     outright (NeverReadLogContentOrASecretValue), alongside RDS's own
#     native log-download API, a distinct action namespace reaching the same
#     data, and `secretsmanager:GetSecretValue`/`rds-data:*`, which
#     ReadOnlyAccess does not currently grant but the denial is written out
#     anyway so the guarantee does not depend on what AWS widens the managed
#     policy to;
#   - `ssm:GetParameter*`, `dynamodb:GetItem`/`Query`/`Scan`,
#     `kinesis:GetRecords`, `sqs:ReceiveMessage` on every parameter,
#     table, stream, and queue in the account - this role has no legitimate
#     reason to read any of them, denied outright
#     (NeverReadOtherApplicationsData).
#
# Deny beats Allow unconditionally, so these hold regardless of what the
# managed policy contains today or gains tomorrow.

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
      "Sid": "NeverReadDataOutsideOurState",
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
      "NotResource": [
        "arn:aws:s3:::${STATE_BUCKET}",
        "arn:aws:s3:::${STATE_BUCKET}/*"
      ]
    },
    {
      "Sid": "NeverReadOtherFunctionsOrRepos",
      "Effect": "Deny",
      "Action": [
        "lambda:GetFunction",
        "lambda:GetFunctionConfiguration",
        "lambda:GetFunctionUrlConfig",
        "lambda:GetLayerVersion",
        "lambda:GetLayerVersionByArn",
        "lambda:InvokeFunction",
        "lambda:InvokeAsync",
        "ecr:GetDownloadUrlForLayer",
        "ecr:BatchGetImage"
      ],
      "NotResource": [
        "arn:aws:lambda:${REGION}:${ACCOUNT_ID}:function:hpac-safety-*",
        "arn:aws:lambda:${REGION}:${ACCOUNT_ID}:layer:hpac-safety-*",
        "arn:aws:ecr:${REGION}:${ACCOUNT_ID}:repository/hpac-safety-*"
      ]
    },
    {
      "Sid": "NeverReadLogContentOrASecretValue",
      "Effect": "Deny",
      "Action": [
        "logs:GetLogEvents",
        "logs:FilterLogEvents",
        "logs:GetLogRecord",
        "logs:StartQuery",
        "logs:GetQueryResults",
        "logs:StartLiveTail",
        "rds:DownloadDBLogFilePortion",
        "rds:DownloadCompleteDBLogFile",
        "secretsmanager:GetSecretValue",
        "rds-data:*"
      ],
      "Resource": "*"
    },
    {
      "Sid": "NeverReadOtherApplicationsData",
      "Effect": "Deny",
      "Action": [
        "ssm:GetParameter",
        "ssm:GetParameters",
        "ssm:GetParametersByPath",
        "ssm:GetParameterHistory",
        "dynamodb:GetItem",
        "dynamodb:BatchGetItem",
        "dynamodb:Query",
        "dynamodb:Scan",
        "dynamodb:PartiQLSelect",
        "kinesis:GetRecords",
        "kinesis:GetShardIterator",
        "sqs:ReceiveMessage"
      ],
      "Resource": "*"
    }
  ]
}
JSON
)

converge_managed_policy "$PLAN_POLICY_NAME" "$PLAN_POLICY_ARN" "$PLAN_POLICY" \
  'What hpac-safety-plan may do. Created by infra/bootstrap.sh.'

for arn in 'arn:aws:iam::aws:policy/ReadOnlyAccess' "$PLAN_POLICY_ARN"; do
  attach_policy_if_missing "$PLAN_ROLE_NAME" "$arn"
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

# Upper-cased for the two repository-level variable names below
# (AWS_PLAN_ROLE_ARN_STAGING / _PRODUCTION, TF_STATE_BUCKET_STAGING /
# _PRODUCTION) - $ENVIRONMENT itself is already lower-case staging/production.
ENVIRONMENT_UPPER=$(printf '%s' "$ENVIRONMENT" | tr '[:lower:]' '[:upper:]')

say ''
say "Bootstrap complete for ${ENVIRONMENT} (GitHub environment ${GITHUB_ENVIRONMENT}). Next (see docs/deployment.md H3):"
say ''
say "  Environment variables - In GitHub: Settings -> Environments -> ${GITHUB_ENVIRONMENT} ->"
say '  Environment variables, add each of these four NAME=value lines (they are'
# shellcheck disable=SC2016  # backticks are markdown code formatting, not shell expansion.
say '  identifiers, not secrets - no `gh secret set` here, only `gh variable set` if'
say '  scripting it). release.yml reads these, scoped to this one environment:'
say ''
say "    gh variable set AWS_DEPLOY_ROLE_ARN --repo ${GITHUB_ORG}/${GITHUB_REPO} --env ${GITHUB_ENVIRONMENT} --body ${ROLE_ARN}"
say "    gh variable set AWS_PLAN_ROLE_ARN   --repo ${GITHUB_ORG}/${GITHUB_REPO} --env ${GITHUB_ENVIRONMENT} --body ${PLAN_ROLE_ARN}"
say "    gh variable set TF_STATE_BUCKET     --repo ${GITHUB_ORG}/${GITHUB_REPO} --env ${GITHUB_ENVIRONMENT} --body ${STATE_BUCKET}"
say "    gh variable set AWS_ACCOUNT_ID      --repo ${GITHUB_ORG}/${GITHUB_REPO} --env ${GITHUB_ENVIRONMENT} --body ${ACCOUNT_ID}"
say ''
say "  Repository variables - hpac-safety-plan's OIDC trust matches a pull request's"
say "  own subject (${OIDC_SUBJECT_PREFIX}:pull_request), which a job presents"
say "  only when it does NOT declare environment: - so terraform.yml's pull-request"
say "  plan job cannot read the ${GITHUB_ENVIRONMENT} copy above. It reads this second,"
say "  repository-scoped copy of the same two values instead (ADR-0164):"
say ''
say "    gh variable set AWS_PLAN_ROLE_ARN_${ENVIRONMENT_UPPER} --repo ${GITHUB_ORG}/${GITHUB_REPO} --body ${PLAN_ROLE_ARN}"
say "    gh variable set TF_STATE_BUCKET_${ENVIRONMENT_UPPER}   --repo ${GITHUB_ORG}/${GITHUB_REPO} --body ${STATE_BUCKET}"
say ''
say "infra/backend.tf is a partial configuration: the bucket name carries the account"
say "id, so it is supplied at init time from TF_STATE_BUCKET rather than committed,"
say "and the state key is namespaced per environment: hpac-safety/${ENVIRONMENT}.tfstate."
say ''

# The six values on stdout, NAME=value, so the output stays parseable even
# though there is more than one of them now. The last two duplicate
# AWS_PLAN_ROLE_ARN/TF_STATE_BUCKET's values under their repository-scoped
# names, so a caller scripting this needs only one parse pass.
printf 'AWS_DEPLOY_ROLE_ARN=%s\n' "$ROLE_ARN"
printf 'AWS_PLAN_ROLE_ARN=%s\n' "$PLAN_ROLE_ARN"
printf 'TF_STATE_BUCKET=%s\n' "$STATE_BUCKET"
printf 'AWS_ACCOUNT_ID=%s\n' "$ACCOUNT_ID"
printf 'AWS_PLAN_ROLE_ARN_%s=%s\n' "$ENVIRONMENT_UPPER" "$PLAN_ROLE_ARN"
printf 'TF_STATE_BUCKET_%s=%s\n' "$ENVIRONMENT_UPPER" "$STATE_BUCKET"
