# RDS PostgreSQL.
#
# This holds the raw reports: names, phone numbers, injuries, and occasionally
# fatalities. Encrypted at rest, private subnets only, no public accessibility,
# automated backups on, deletion protection on.

resource "aws_db_subnet_group" "main" {
  name        = local.name
  description = "Private subnets. The database has no route to or from the internet."
  subnet_ids  = [for s in aws_subnet.private : s.id]

  tags = { Name = local.name }
}

# A parameter group exists so there is somewhere to put a setting, rather than
# discovering later that the default group cannot be edited and every change is
# a replacement.
resource "aws_db_parameter_group" "main" {
  name        = "${local.name}-postgres${var.db_engine_version}"
  family      = "postgres${var.db_engine_version}"
  description = "PostgreSQL parameters for ${local.name}."

  # Log statements that take longer than a second. Postgres logs the statement
  # TEXT, and a slow INSERT of a report would put narrative into the log — so
  # this is intentionally a duration threshold rather than log_statement = 'all'.
  # See docs/data-handling.md: log identifiers, never content.
  #
  # This is why the two log groups these logs export to
  # (enabled_cloudwatch_logs_exports, below) are named explicitly in the
  # NeverReadDatabaseLogs denial in infra/bootstrap.sh — reading them through
  # CloudWatch Logs' API is a different action namespace from RDS's own
  # log-download API, and denying only the latter would leave this text
  # readable through the former.
  parameter {
    name  = "log_min_duration_statement"
    value = "1000"
  }

  parameter {
    name  = "log_connections"
    value = "1"
  }

  parameter {
    name  = "log_disconnections"
    value = "1"
  }

  # Force TLS. The application connects from inside the VPC, but "inside the
  # VPC" is not encryption.
  parameter {
    name  = "rds.force_ssl"
    value = "1"
  }

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_db_instance" "main" {
  identifier     = local.name
  engine         = "postgres"
  engine_version = var.db_engine_version

  # Minor versions are applied in the maintenance window rather than tracked in
  # this file, which is why engine_version is a major only.
  auto_minor_version_upgrade = true
  apply_immediately          = false

  instance_class = var.db_instance_class

  allocated_storage     = var.db_allocated_storage
  max_allocated_storage = var.db_max_allocated_storage
  storage_type          = "gp3"
  storage_encrypted     = true

  db_name  = "hpacsafety"
  username = "hpacsafety"

  # The master password is generated and rotated by RDS into its own Secrets
  # Manager secret. Terraform never sees it, so it is never written to state —
  # which is the same rule ADR-0010 states for application secrets, applied to
  # the one credential Terraform would otherwise be forced to hold.
  manage_master_user_password = true

  db_subnet_group_name   = aws_db_subnet_group.main.name
  parameter_group_name   = aws_db_parameter_group.main.name
  vpc_security_group_ids = [aws_security_group.database.id]
  publicly_accessible    = false
  multi_az               = var.db_multi_az

  backup_retention_period = var.db_backup_retention_days
  backup_window           = "07:00-08:00" # 02:00-03:00 America/Toronto
  maintenance_window      = "sun:08:00-sun:09:00"
  copy_tags_to_snapshot   = true

  # A snapshot is taken before the instance goes, and deletion_protection means
  # a `terraform destroy` cannot take it by accident. Destroying and recreating
  # the environment — an acceptance criterion on #32 — therefore requires
  # clearing this deliberately, which is the intended amount of friction for the
  # one resource holding data about real accidents.
  deletion_protection       = true
  skip_final_snapshot       = false
  final_snapshot_identifier = "${local.name}-final-${formatdate("YYYYMMDDhhmmss", timestamp())}"

  performance_insights_enabled    = false
  enabled_cloudwatch_logs_exports = ["postgresql", "upgrade"]

  tags = { Name = local.name }

  lifecycle {
    # timestamp() changes on every plan. Without this, an unchanged repository
    # would produce a diff, and ADR-0010 requires apply on an unchanged
    # repository to be a no-op.
    ignore_changes = [final_snapshot_identifier]
  }
}
