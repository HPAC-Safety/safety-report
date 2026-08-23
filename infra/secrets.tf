# Secrets Manager entries only; Terraform never stores their values.
# A human sets each value before the first deployment.

locals {
  secret_entries = {
    model_api_key = {
      name        = "${local.name}/model-api-key"
      description = "Model API key. Read by the Worker for summarize and translate."
    }
    turnstile_secret_key = {
      name        = "${local.name}/turnstile-secret-key"
      description = "Cloudflare Turnstile secret key. Read by the API for server-side siteverify. Never reaches a static bundle."
    }
    connection_string = {
      name        = "${local.name}/connection-string"
      description = "ConnectionStrings__HpacSafety for the API, Worker, and migration task."
    }
    notifications_to = {
      name        = "${local.name}/notifications-to"
      description = "Notifications__To. Where the Worker sends review alerts, safety@hpac.ca in production. A mailbox address, held here rather than in a variable so changing it is not a deploy."
    }
  }
}

resource "aws_secretsmanager_secret" "this" {
  for_each = local.secret_entries

  name                    = each.value.name
  description             = each.value.description
  recovery_window_in_days = 7

  tags = { Name = each.value.name }
}
