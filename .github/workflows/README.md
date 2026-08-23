# Workflows

- `ci.yml`: build, tests, coverage, web checks, bilingual-question validation,
  site-string translation parity, end-to-end placeholder, and agent
  configuration.
- `linked-issue.yml`: requires a pull request to close an issue.
- `i18n-translate.yml`: generates `locales/fr-CA.json` on a push to `main` and
  opens a pull request with it. Never runs on `pull_request` — see
  ADR-0021. Uses `DEEPL_API_KEY`, a GitHub Actions repository secret.
- `terraform.yml`: validates/plans infrastructure and applies approved main.
- `deploy-api.yml`: image, migration task, then API service update.
- `deploy-worker.yml`: Worker image and service update.
- `deploy-web.yml`: static public/admin subtrees to one site bucket.

Pull-request workflows never read runtime secrets — `i18n-translate.yml`'s
`DEEPL_API_KEY` is the one exception, and it runs only on a push to `main`,
never on a fork-authored `pull_request`. Deploy workflows authenticate to AWS
with GitHub OIDC and run only for successful `main` builds or a manual
dispatch. Runtime secrets live in AWS Secrets Manager, not GitHub.

Workflow job ids are required-check names; update the repository ruleset if a
job id changes. Local composite actions used only by deploy workflows must also
be exercised by CI so their manifests are validated before merge.
