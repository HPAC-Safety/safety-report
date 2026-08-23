# Secrets Manager ENTRIES. Never values.
#
# A value in a .tfvars file is a value in Terraform state, and state is a file in
# S3 that more people can read than should see an API key. So Terraform creates
# the entry, the task definitions reference it by ARN, and a human puts the value
# in once:
#
#   aws secretsmanager put-secret-value \
#     --secret-id hpac-safety/anthropic-api-key \
#     --secret-string '…'
#
# There is deliberately no `aws_secretsmanager_secret_version` resource anywhere
# in this directory. If you find yourself adding one, that is the defect.
#
# Consequence, stated plainly because it looks like a bug the first time: a task
# whose secret has no version FAILS TO START, with a ResourceNotFoundException in
# the ECS event log. That is the correct behaviour — an API booting without its
# Turnstile key would accept unverified submissions — but it means the
# put-secret-value calls are part of first deploy, not a later tidy-up. They are
# listed in docs/deployment.md.

locals {
  # Description is the whole documentation surface an operator sees in the
  # console, so it says what the value is and who reads it.
  secret_entries = {
    anthropic_api_key = {
      name        = "${local.name}/anthropic-api-key"
      description = "Anthropic API key. Read by the Worker for summarize, PII audit, and translate."
    }
    turnstile_secret_key = {
      name        = "${local.name}/turnstile-secret-key"
      description = "Cloudflare Turnstile secret key. Read by the API for server-side siteverify. Never reaches a static bundle."
    }
    connection_string = {
      name        = "${local.name}/connection-string"
      description = "ConnectionStrings__Default. Built from the RDS endpoint and the RDS-managed master password secret; not derivable by Terraform without putting the password in state."
    }
    notifications_to = {
      name        = "${local.name}/notifications-to"
      description = "Notifications__To. Where the Worker sends review alerts, safety@hpac.ca in production. A mailbox address, held here rather than in a variable so changing it is not a deploy."
    }
  }
}

resource "aws_secretsmanager_secret" "this" {
  for_each = local.secret_entries

  name        = each.value.name
  description = each.value.description

  # Long enough to undo a mistaken delete, short enough that the name can be
  # reused within a sprint.
  recovery_window_in_days = 7

  tags = { Name = each.value.name }
}

# The RDS-managed master password lives in its own secret, created by RDS rather
# than by this file. The task execution role needs to read it only if the
# connection string is ever assembled from it; today it is not, and the entry
# above holds the assembled string. Exposed as an output so an operator can find
# it without hunting through the console.
