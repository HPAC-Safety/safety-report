# Log groups and alarms.
#
# THE ONE APPLICATION METRIC THIS SYSTEM PUBLISHES — everything else this
# file's alarms watch is a signal AWS already measures on its own.
#
#   OutboxOldestAgeSeconds  a gauge, published by the Worker on each drain
#                           pass: how old the oldest unclaimed, unpoisoned
#                           outbox row is. It rises when the Worker is wedged
#                           and stays flat when it is merely busy, which is
#                           why it is the age and not the depth.
#
# It goes to the namespace below, which the Worker is given through
# Metrics__Namespace (infra/lambda.tf) so it can never drift from what the
# alarm reads. No dimension is ever derived from report content (AGENTS.md
# invariant 8) — this metric has none at all.
#
# The owner scaled operations back for a lightly used system (#467,
# 2026-09-28): four alarms total, production email only, each a short,
# self-contained description naming what is wrong and what it affects — no
# runbook link. The thresholds are the owner's, kept tunable per environment
# in tfvars.

locals {
  metric_namespace = "HpacSafety"
}

resource "aws_cloudwatch_log_group" "this" {
  for_each = local.log_groups

  name              = each.value
  retention_in_days = var.log_retention_days

  tags = { Name = each.value }

  lifecycle {
    # Audit history. Never replaced, in both environments (CON-INF-013) — a
    # plan that would drop and recreate one loses everything logged under it.
    prevent_destroy = true
  }
}

resource "aws_sns_topic" "alarms" {
  name = "${local.name}-alarms"

  tags = { Name = "${local.name}-alarms" }
}

# PENDING CONFIRMATION until a human clicks the link AWS emails to the address.
# Terraform creates the subscription and reports success either way; it cannot
# complete the handshake, and there is no attribute to wait on. Until someone
# clicks, every alarm below fires, is visible in CloudWatch, and emails nobody.
#
# The `alarm_subscriptions_pending_confirmation` output and the manual-steps
# table in docs/deployment.md both exist so that gap is stated rather than
# discovered.
#
# Alarm mail comes from Amazon SNS — an operator alert, not an application
# email flow. It does still need safety@hpac.ca to be a mailbox somebody reads.
# ADR-0158: this subscription exists in every environment, but only
# production's list of addresses is non-empty — staging holds synthetic data
# and has nobody on call for it.
resource "aws_sns_topic_subscription" "alarms_email" {
  for_each = toset(var.alarm_email_addresses)

  topic_arn = aws_sns_topic.alarms.arn
  protocol  = "email"
  endpoint  = each.value
}

resource "aws_cloudwatch_metric_alarm" "outbox_age" {
  alarm_name        = "${local.name}-outbox-age"
  alarm_description = "Outbox stalled: reports have been waiting over 15 minutes for processing."

  namespace   = local.metric_namespace
  metric_name = "OutboxOldestAgeSeconds"
  statistic   = "Maximum"

  comparison_operator = "GreaterThanThreshold"
  threshold           = var.outbox_age_alarm_seconds
  period              = 300
  evaluation_periods  = 2
  datapoints_to_alarm = 2

  # A Worker that has stopped publishing entirely is the failure this alarm is
  # for, so missing data is breaching — not "no news is good news".
  treat_missing_data = "breaching"

  alarm_actions = [aws_sns_topic.alarms.arn]
  ok_actions    = [aws_sns_topic.alarms.arn]

  tags = { Name = "${local.name}-outbox-age" }
}

resource "aws_cloudwatch_metric_alarm" "api_errors" {
  # Was the ALB's HTTPCode_Target_5XX_Count; there is no ALB in front of the
  # API any more (ADR-0042, ADR-0159, #443). A Function URL has no per-request
  # status-code metric of its own, so this watches the function's own
  # unhandled-exception count instead — a superset of "5xx", since an
  # unhandled exception is exactly what an ASP.NET Core process turns into one.
  alarm_name        = "${local.name}-api-errors"
  alarm_description = "API failing: reporters cannot submit or view reports."

  namespace   = "AWS/Lambda"
  metric_name = "Errors"
  statistic   = "Sum"

  dimensions = {
    FunctionName = aws_lambda_function.api.function_name
  }

  comparison_operator = "GreaterThanOrEqualToThreshold"
  threshold           = var.lambda_error_alarm_threshold
  period              = var.lambda_error_alarm_period_seconds
  evaluation_periods  = 1
  treat_missing_data  = "notBreaching"

  alarm_actions = [aws_sns_topic.alarms.arn]

  tags = { Name = "${local.name}-api-errors" }
}

resource "aws_cloudwatch_metric_alarm" "worker_errors" {
  alarm_name        = "${local.name}-worker-errors"
  alarm_description = "Worker failing: summaries and translations have stopped."

  namespace   = "AWS/Lambda"
  metric_name = "Errors"
  statistic   = "Sum"

  dimensions = {
    FunctionName = aws_lambda_function.worker.function_name
  }

  comparison_operator = "GreaterThanOrEqualToThreshold"
  threshold           = var.lambda_error_alarm_threshold
  period              = var.lambda_error_alarm_period_seconds
  evaluation_periods  = 1
  treat_missing_data  = "notBreaching"

  alarm_actions = [aws_sns_topic.alarms.arn]

  tags = { Name = "${local.name}-worker-errors" }
}

# The NAT instance (#465/#588, ADR-0158) is the one resource this system ever
# deletes and recreates — a one-instance Auto Scaling group heals it on its
# own between releases. GroupInServiceInstances < 1 covers both conditions the
# owner named: the ASG's own EC2 health check already folds a failed status
# check into a lower in-service count, so a single alarm on the in-service
# count is "0 healthy instances, or a failed status check" — not two alarms
# for the same underlying signal. Approved as is (#467, 2026-09-28).
resource "aws_cloudwatch_metric_alarm" "nat_unhealthy" {
  alarm_name        = "${local.name}-nat-unhealthy"
  alarm_description = "NAT instance down: outbound calls to the identity provider, Gemini, and DeepL are failing."

  namespace   = "AWS/AutoScaling"
  metric_name = "GroupInServiceInstances"
  statistic   = "Minimum"

  dimensions = {
    # The module exposes only the ARN (autoscaling_group_arn); the name is its
    # last "/"-separated segment
    # (arn:aws:autoscaling:<region>:<account>:autoScalingGroup:<id>:autoScalingGroupName/<name>).
    AutoScalingGroupName = element(split("/", module.fck_nat.autoscaling_group_arn), 1)
  }

  comparison_operator = "LessThanThreshold"
  threshold           = 1
  period              = var.nat_unhealthy_alarm_period_seconds
  evaluation_periods  = 1

  # No data at all from the ASG is exactly the failure this alarm watches for.
  treat_missing_data = "breaching"

  alarm_actions = [aws_sns_topic.alarms.arn]
  ok_actions    = [aws_sns_topic.alarms.arn]

  tags = { Name = "${local.name}-nat-unhealthy" }
}
