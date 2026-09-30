---
title: Environment setup
description: Every human step to configure GitHub and AWS for the staging and production environments, as one checklist.
type: guide
---

# Environment setup

Every step a person has to take by hand, in order. Everything else runs from
GitHub. The why lives in [`docs/deployment.md`](../docs/deployment.md); this
page is only the checklist.

- Do **Part 1** once for the repository.
- Do **Part 2** for staging, then **Part 3** for production. They are separate
  accounts with separate settings; never copy a value from one to the other.
- No step shares a password or an AWS access key. GitHub reaches AWS by OIDC
  only ([ADR-0158](../docs/decisions/ADR-0158-two-aws-accounts-staged-and-promoted-by-approval.md)).

## Staging vs production

| | Staging | Production |
|---|---|---|
| AWS account | The owner's existing account, shared with unrelated apps | HPAC's own account, unrelated to staging (not an AWS Organizations member of it) |
| GitHub environment | `hpac-safety-staging` | `hpac-safety-production` |
| Bootstrap argument | `staging` | `production` |
| Deploys when | A release is published, with no approval | A maintainer promotes a staged tag with `promote.yml`, and the `hpac-safety-admins` team approves |
| Web address | CloudFront default `dxxxx.cloudfront.net` only | `safety.hpac.ca` (English), `securite.acvl.ca` (French) |
| DNS and certificate | None | ACM validation and CNAME records at the hpac.ca and acvl.ca DNS hosts |
| Alarm email | None (the topic has no subscriber) | `safety@hpac.ca` |
| Database backup retention | 1 day | 7 days |
| Data | Synthetic only | Real reports |
| Settings file | [`staging.tfvars`](staging.tfvars) | [`production.tfvars`](production.tfvars) |

`diff infra/staging.tfvars infra/production.tfvars` is the complete list of
infrastructure differences.

## Part 1 — Repository (once, for every environment)

### Prerequisites

- Owner of the [`HPAC-Safety`](https://github.com/HPAC-Safety) GitHub
  organization.
- Admin on [`HPAC-Safety/safety-report`](https://github.com/HPAC-Safety/safety-report).
- The [GitHub CLI](https://cli.github.com/) signed in as that person
  (`gh auth status`).
- A paid, billing-enabled Gemini API key, for the interface translation
  workflow (the same development key summaries use,
  [ADR-0179](../docs/decisions/ADR-0179-gemini-translates-everything-between-canadian-english-and-canadian-french.md)).

### 1.1 Merge settings

- Open [Settings → General](https://github.com/HPAC-Safety/safety-report/settings)
  → **Pull Requests**:
  - allow **squash merging** only; untick merge commits and rebase merging;
  - squash default message: **Pull request title and description**;
  - tick **Allow auto-merge**;
  - tick **Automatically delete head branches**.
- Check it worked:
  `gh api repos/HPAC-Safety/safety-report --jq '{allow_squash_merge,allow_merge_commit,allow_rebase_merge,allow_auto_merge,delete_branch_on_merge}'`
  shows `true, false, false, true, true`.

### 1.2 The `main` ruleset and merge queue

- Apply only after `main` carries the workflows that report the required checks
  ([ADR-0147](../docs/decisions/ADR-0147-pull-requests-merge-through-a-merge-queue.md)).
- New repository:
  `gh api -X POST repos/HPAC-Safety/safety-report/rulesets --input docs/github-ruleset.json`.
- Existing ruleset: find its ID with
  `gh api repos/HPAC-Safety/safety-report/rulesets --jq '.[]|"\(.id) \(.name)"'`,
  then `gh api -X PUT repos/HPAC-Safety/safety-report/rulesets/<id> --input docs/github-ruleset.json`.
- Source of truth: [`docs/github-ruleset.json`](../docs/github-ruleset.json).
  Change the file, then re-apply; never edit the rule only in the UI.
- Check it worked: [Settings → Rules → Rulesets](https://github.com/HPAC-Safety/safety-report/settings/rules)
  shows `main` as **Active**, and the next auto-merged pull request enters the
  merge queue.

### 1.3 Renovate

- Install the [Renovate GitHub App](https://github.com/apps/renovate) on
  `HPAC-Safety/safety-report` only.
- Its settings are [`renovate.json`](../renovate.json); nothing else to
  configure.
- Check it worked: a **Dependency Dashboard** issue appears, and Renovate pull
  requests carry the `renovate` label.

### 1.4 Translation secrets and variables

Repository-level (not environment) settings, read by
[`i18n-translate.yml`](../.github/workflows/i18n-translate.yml),
[`traceability.yml`](../.github/workflows/traceability.yml), and
[`terraform-relock.yml`](../.github/workflows/terraform-relock.yml)
([ADR-0021](../docs/decisions/ADR-0021-ci-translation-opens-a-pull-request.md)).

- [Settings → Secrets and variables → Actions → **Secrets**](https://github.com/HPAC-Safety/safety-report/settings/secrets/actions):
  - `GEMINI_API_KEY_DEV` — the development Gemini key, which the translation
    workflow also reads ([ADR-0179](../docs/decisions/ADR-0179-gemini-translates-everything-between-canadian-english-and-canadian-french.md)). Without it, translation reports what is
    waiting and changes nothing.
  - `TRANSLATION_PR_TOKEN` — a
    [fine-grained personal access token](https://github.com/settings/personal-access-tokens/new)
    for this repository only, with **Contents: Read and write** and
    **Pull requests: Read and write**, nothing else. Without it, bot commits
    land but checks need a manual re-run. Note its expiry date.
- **Variables** tab — optional, all unset by default; set only to tune
  translation:
  - `TRANSLATION_PROVIDER` (default `gemini`), `TRANSLATION_MODEL` (default
    `gemini-3.7-flash`), `TRANSLATION_REASONING_EFFORT` (default `low`),
    `TRANSLATION_ENDPOINT`.
- Check it worked: `gh secret list --repo HPAC-Safety/safety-report` lists
  `GEMINI_API_KEY_DEV` and `TRANSLATION_PR_TOKEN`.

### 1.5 The approvers team

- [Organization → Teams → New team](https://github.com/orgs/HPAC-Safety/new-team):
  name `hpac-safety-admins`; add everyone who may approve a production deploy.
- Give it access to the repository:
  [Settings → Collaborators and teams](https://github.com/HPAC-Safety/safety-report/settings/access)
  → **Add teams** → `hpac-safety-admins` → **Write** or higher.
- Check it worked: `gh api orgs/HPAC-Safety/teams/hpac-safety-admins/members --jq '.[].login'`.

### 1.6 Project board

- Link the [HPAC Safety project](https://github.com/orgs/HPAC-Safety/projects)
  to the repository: repository **Projects** tab → **Link a project**.
- Check it worked: a new issue can be added to it with
  `gh project item-add <number> --owner HPAC-Safety --url <issue-url>`.

## Part 2 — Staging

### Prerequisites (staging)

- Part 1 done.
- Administrator sign-in to the **existing** AWS account (the owner's).
- Admin on the GitHub repository.
- The Gemini API key (paid,
  [ADR-0104](../docs/decisions/ADR-0104-summaries-are-generated-by-gemini-through-a-paid-key.md))
  and a DeepL API key. Staging uses its own copies; for now they hold the same
  values as production's.
- Nothing else: staging needs no DNS, no certificate, and no alarm inbox.

### 2.1 GitHub environment

- [Settings → Environments → New environment](https://github.com/HPAC-Safety/safety-report/settings/environments/new):
  `hpac-safety-staging`.
- **Deployment branches and tags** → **Selected branches and tags** → add a
  **branch** rule `main` (Release runs on `main`) and a **tag** rule `20*`
  (release tags are `YYYY.MM.DD-N`).
- No required reviewers.
- Check it worked: the environment lists the `main` branch rule, the `20*`
  tag rule, and no reviewers.

### 2.2 Bootstrap the staging AWS account

- Sign in to the existing account: <https://console.aws.amazon.com>.
- Region, top right: **Canada (Central) ca-central-1**.
- Open **CloudShell** (the `>_` icon in the top bar).
- Paste:
  ```sh
  git clone https://github.com/HPAC-Safety/safety-report && sh safety-report/infra/bootstrap.sh staging
  ```
- It creates (or reuses) the GitHub OIDC provider, the `hpac-safety-deploy`
  and `hpac-safety-plan` roles, and the state bucket
  `hpac-safety-tfstate-<account-id>`. It touches nothing else in the shared
  account, and is safe to re-run. Script: [`bootstrap.sh`](bootstrap.sh).
- Keep the printed `gh variable set …` lines for the next step.
- Check it worked: [IAM → Roles](https://console.aws.amazon.com/iam/home#/roles)
  shows `hpac-safety-deploy` and `hpac-safety-plan`; S3 shows the state bucket.

### 2.3 Staging variables

- Environment variables, under
  [Environments → `hpac-safety-staging` → Environment variables](https://github.com/HPAC-Safety/safety-report/settings/environments),
  or paste the printed lines:
  ```sh
  gh variable set AWS_DEPLOY_ROLE_ARN --repo HPAC-Safety/safety-report --env hpac-safety-staging --body <printed>
  gh variable set AWS_PLAN_ROLE_ARN   --repo HPAC-Safety/safety-report --env hpac-safety-staging --body <printed>
  gh variable set TF_STATE_BUCKET     --repo HPAC-Safety/safety-report --env hpac-safety-staging --body <printed>
  gh variable set AWS_ACCOUNT_ID      --repo HPAC-Safety/safety-report --env hpac-safety-staging --body <printed>
  ```
- Repository variables, read by pull-request `terraform plan`
  ([ADR-0164](../docs/decisions/ADR-0164-release-workflow-build-once-deploy-and-promote.md)):
  ```sh
  gh variable set AWS_PLAN_ROLE_ARN_STAGING --repo HPAC-Safety/safety-report --body <printed>
  gh variable set TF_STATE_BUCKET_STAGING   --repo HPAC-Safety/safety-report --body <printed>
  ```
- These are identifiers, not secrets.
- Check it worked:
  `gh variable list --repo HPAC-Safety/safety-report --env hpac-safety-staging`
  lists four; `gh variable list --repo HPAC-Safety/safety-report` lists the two
  `_STAGING` values.

### 2.4 Staging secrets

- [Environments → `hpac-safety-staging` → Environment secrets](https://github.com/HPAC-Safety/safety-report/settings/environments):
  ```sh
  gh secret set GEMINI_API_KEY --repo HPAC-Safety/safety-report --env hpac-safety-staging
  gh secret set DEEPL_API_KEY  --repo HPAC-Safety/safety-report --env hpac-safety-staging
  ```
- Each release copies them into the account's Secrets Manager; never enter
  them in AWS by hand.
- Check it worked:
  `gh secret list --repo HPAC-Safety/safety-report --env hpac-safety-staging`
  lists both.

### 2.5 First staging release

- [Actions → Release](https://github.com/HPAC-Safety/safety-report/actions/workflows/release.yml)
  → **Run workflow** (from `main`) → **Run workflow**. Or
  `gh workflow run release.yml --repo HPAC-Safety/safety-report --ref main`.
- One run creates today's next tag (`YYYY.MM.DD-N`) and a GitHub Release whose
  notes list every pull request merged since the previous release, builds
  once, and deploys staging. Nothing to type; no tag or release is made by
  hand.
- The `staging` job runs on its own, and the release ends there. It never
  deploys to or waits on production; create as many as you like.
- Check it worked: the `staging` job is green; the `cloudfront.net` address in
  its summary opens the site, and `<address>/api/health` answers.
- Also check: open any pull request touching `infra/`; the `plan (staging)` leg
  of [`terraform.yml`](../.github/workflows/terraform.yml) posts a plan.

## Part 3 — Production

### Prerequisites (production)

- Parts 1 and 2 done, with a green staging release.
- HPAC's own AWS account, created and owned by HPAC, with its root email under
  HPAC's control. It is **not** created from the staging account.
- Administrator sign-in to that account.
- At least one member in `hpac-safety-admins` (step 1.5).
- Someone who can edit DNS at the **hpac.ca** host, and someone who can at the
  **acvl.ca** host. They may be different organisations; allow days.
- Access to the `safety@hpac.ca` inbox.
- The Gemini and DeepL keys for production.

### 3.1 GitHub environment

- [Settings → Environments → New environment](https://github.com/HPAC-Safety/safety-report/settings/environments/new):
  `hpac-safety-production`.
- **Required reviewers**: the `hpac-safety-admins` team.
- Optionally tick **Prevent self-review**. With a sole maintainer, leave it off.
- Untick **Allow administrators to bypass configured protection rules**, so a
  repository admin cannot deploy to production without the approval.
- **Deployment branches and tags** → **Selected branches and tags** → add a
  **tag** rule `20*` only; no branch rule. Promote runs on the tag.
- Check it worked: the environment lists the reviewers and the `20*` tag rule.

### 3.2 Bootstrap the production AWS account

- Sign in to **HPAC's** account (not staging's): <https://console.aws.amazon.com>.
- Region: **Canada (Central) ca-central-1**.
- Open **CloudShell** and paste:
  ```sh
  git clone https://github.com/HPAC-Safety/safety-report && sh safety-report/infra/bootstrap.sh production
  ```
- Check it worked: as in 2.2, in this account; the printed account ID is
  HPAC's, not staging's.

### 3.3 Production variables

- Environment variables, from this account's printed lines:
  ```sh
  gh variable set AWS_DEPLOY_ROLE_ARN --repo HPAC-Safety/safety-report --env hpac-safety-production --body <printed>
  gh variable set AWS_PLAN_ROLE_ARN   --repo HPAC-Safety/safety-report --env hpac-safety-production --body <printed>
  gh variable set TF_STATE_BUCKET     --repo HPAC-Safety/safety-report --env hpac-safety-production --body <printed>
  gh variable set AWS_ACCOUNT_ID      --repo HPAC-Safety/safety-report --env hpac-safety-production --body <printed>
  ```
- Repository variables:
  ```sh
  gh variable set AWS_PLAN_ROLE_ARN_PRODUCTION --repo HPAC-Safety/safety-report --body <printed>
  gh variable set TF_STATE_BUCKET_PRODUCTION   --repo HPAC-Safety/safety-report --body <printed>
  ```
- Check it worked: the `hpac-safety-production` list has four, with HPAC's
  account ID; the repository list has both `_PRODUCTION` values.

### 3.4 Production secrets

- ```sh
  gh secret set GEMINI_API_KEY --repo HPAC-Safety/safety-report --env hpac-safety-production
  gh secret set DEEPL_API_KEY  --repo HPAC-Safety/safety-report --env hpac-safety-production
  ```
- Check it worked:
  `gh secret list --repo HPAC-Safety/safety-report --env hpac-safety-production`
  lists both.

### 3.5 First production deploy

- Pick a release whose `staging` job is green (2.5).
- [Actions → Promote → **Run workflow**](https://github.com/HPAC-Safety/safety-report/actions/workflows/promote.yml)
  → **Use workflow from** → **Tags** → that tag → **Run workflow**. Or
  `gh workflow run promote.yml --repo HPAC-Safety/safety-report --ref <tag>`.
- A member of `hpac-safety-admins` opens the run → **Review deployments** →
  tick `hpac-safety-production` → **Approve and deploy**.
- It deploys the same images and web build staging ran; nothing is rebuilt.
  A tag never green on staging, or older than the 90-day artifact retention,
  is refused before any AWS call.
- Check it worked: the job reaches the certificate step and prints
  `dns_records_to_publish` in its summary.

### 3.6 Production DNS

- Copy the records from the job summary; they are grouped by zone.
- At the **hpac.ca** DNS host:
  - the ACM validation CNAME for `safety.hpac.ca`;
  - `safety` CNAME → the `dxxxx.cloudfront.net` name shown.
- At the **acvl.ca** DNS host:
  - the ACM validation CNAME for `securite.acvl.ca`;
  - `securite` CNAME → the **same** `dxxxx.cloudfront.net`.
- Validation can take up to two hours. If the job timed out waiting, re-run it
  after the records resolve.
- Check it worked: `dig +short CNAME safety.hpac.ca` and
  `dig +short CNAME securite.acvl.ca` both return the CloudFront name;
  <https://safety.hpac.ca> opens in English and <https://securite.acvl.ca> in
  French.

### 3.7 Alarm email

- Open **AWS Notification - Subscription Confirmation** at `safety@hpac.ca`
  and click **Confirm subscription**.
- Check it worked: [SNS → Subscriptions](https://ca-central-1.console.aws.amazon.com/sns/v3/home?region=ca-central-1#/subscriptions)
  in the production account shows the address as **Confirmed**.

## Part 4 — After setup

- **Every release**: run Release (2.5); it reaches staging only.
- **Promote to production**: when a staged tag is ready, promote it and
  approve (3.5). Nothing else.
- **Rollback**: production, promote an earlier tag; staging, re-run an
  earlier release's jobs. See
  [`docs/deployment.md`](../docs/deployment.md) "Release and promotion".
- **Rotating a key**: replace the environment secret (2.4 or 3.4), then
  publish a release.
- **Token expiry**: renew `TRANSLATION_PR_TOKEN` (1.4) before it expires.

### Known gaps

- **Sign-in**: the identity provider is deferred
  ([ADR-0064](../docs/decisions/ADR-0064-jwt-bearer-authentication-with-three-roles.md)),
  and no workflow or Terraform sets its authority yet. Until it is wired,
  public pages and submission work, but sign-in, review, and administration do
  not, in either environment.
- **Restore drill**: not planned (#594 closed), and no restore runbook exists.

## Further reading

- [`docs/deployment.md`](../docs/deployment.md) — release, promotion,
  rollback, and what the deploy and plan roles may do.
- [`docs/infrastructure-and-operations.md`](../docs/infrastructure-and-operations.md)
  — topology, configuration, alarms, backups.
- [`infra/README.md`](README.md) — what each Terraform file creates.
- [`.github/workflows/README.md`](../.github/workflows/README.md) — what each
  workflow does.
- [ADR-0159](../docs/decisions/ADR-0159-cloudfront-routes-api-to-a-function-url-no-alb.md)
  — CloudFront routes `/api/*` to the API; no ALB.
