# Security groups.
#
# Rules are separate `aws_vpc_security_group_*_rule` resources rather than inline
# blocks: inline rules are authoritative for the whole group, so two people
# editing the same group silently delete each other's rules, and the plan reads
# as a replacement rather than an addition.

resource "aws_security_group" "api" {
  name        = "${local.name}-api"
  description = "The API's Lambda function, in the private subnets (ADR-0042). No ingress rule at all — a Function URL's traffic never traverses this ENI; only the function's own outbound calls (RDS, Secrets Manager, the identity provider, DeepL) do."
  vpc_id      = aws_vpc.main.id

  tags = merge(local.app_tags, { Name = "${local.name}-api" })
}

resource "aws_vpc_security_group_egress_rule" "api_all" {
  security_group_id = aws_security_group.api.id
  description       = "Outbound to RDS, Secrets Manager, ECR, CloudWatch, the identity provider, DeepL."
  cidr_ipv4         = "0.0.0.0/0"
  ip_protocol       = "-1"
}

resource "aws_security_group" "worker" {
  name        = "${local.name}-worker"
  description = "The Worker's Lambda function, in the private subnets. No ingress at all — it is invoked by the API and by EventBridge, neither of which reaches it over this ENI."
  vpc_id      = aws_vpc.main.id

  tags = merge(local.app_tags, { Name = "${local.name}-worker" })
}

resource "aws_vpc_security_group_egress_rule" "worker_all" {
  security_group_id = aws_security_group.worker.id
  description       = "Outbound to RDS, Secrets Manager, ECR, CloudWatch, and the Gemini API."
  cidr_ipv4         = "0.0.0.0/0"
  ip_protocol       = "-1"
}

resource "aws_security_group" "database" {
  name        = "${local.name}-database"
  description = "PostgreSQL. Reachable from the two task groups and nothing else."
  vpc_id      = aws_vpc.main.id

  tags = merge(local.app_tags, { Name = "${local.name}-database" })
}

resource "aws_vpc_security_group_ingress_rule" "database_from_api" {
  security_group_id            = aws_security_group.database.id
  description                  = "From the API tasks."
  referenced_security_group_id = aws_security_group.api.id
  from_port                    = 5432
  to_port                      = 5432
  ip_protocol                  = "tcp"
}

resource "aws_vpc_security_group_ingress_rule" "database_from_worker" {
  security_group_id            = aws_security_group.database.id
  description                  = "From the Worker tasks."
  referenced_security_group_id = aws_security_group.worker.id
  from_port                    = 5432
  to_port                      = 5432
  ip_protocol                  = "tcp"
}

# No egress rule on the database group. PostgreSQL answers on an established
# connection; it never opens one.
