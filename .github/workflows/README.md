# GitHub workflows

| Workflow | Responsibility |
|---|---|
| `ci.yml` | Build, tests, coverage, web, localization, skill/agent validation |
| `linked-issue.yml` | Require a closing issue reference in each PR |
| `i18n-translate.yml` | Prepare French application-catalogue changes only |
| `terraform.yml` | Validate/plan/apply infrastructure |
| `deploy-api.yml` | Publish API image and run explicit migrations |
| `deploy-worker.yml` | Publish Worker image |
| `deploy-web.yml` | Publish static sites |

Pull-request workflows must be safe for forks: use `pull_request`, never
`pull_request_target`, do not expose secrets, and never make live AI or
translation calls. UI catalogue generation runs after merge and opens a human-
reviewed PR; it does not translate database questions or summaries.

Deployments run only from successful tested `main` commits or explicit manual
dispatch, use GitHub OIDC rather than AWS access keys, and are protected by the
production environment. A migration completes before new API traffic.

The current deployment workflows still reflect legacy combined-site and email
infrastructure. Align them with issue #30 and
[`../../docs/infrastructure-and-operations.md`](../../docs/infrastructure-and-operations.md)
before production use.

Run `actionlint` after workflow edits. If a required job ID changes, update the
repository ruleset in the same PR. Every PR body must contain an actual closing
keyword such as `Closes #78`.
