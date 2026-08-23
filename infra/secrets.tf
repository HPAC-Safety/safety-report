# Secrets Manager entries only; Terraform never stores their values.
# A human sets each value before the first deployment.

locals {
  secret_entries = {
    model_api_key = {
      name        = "${local.name}/model-api-key"
      description = "Model API key. Read by the Worker for its one anonymizing summary call."
    }
    connection_string = {
      name        = "${local.name}/connection-string"
      description = "ConnectionStrings__HpacSafety for the API, Worker, and migration task."
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
