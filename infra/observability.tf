# Log groups and alarms.
#
# THE TWO ALARMS THE ISSUE NAMES WATCH METRICS THE APPLICATION HAS TO PUBLISH.
# They are not derived from anything AWS knows on its own:
#
#   SummaryFailed           a count, incremented by the Worker each time a
#                           summarization attempt fails. One failure means a real
#                           report is sitting unprocessed.
#   OutboxOldestAgeSeconds  a gauge, published by the Worker on each poll: how
#                           old the oldest unclaimed outbox row is. It rises when
#                           the Worker is wedged and stays flat when it is merely
#                           busy, which is why it is the age and not the depth.
#
# Both go to the namespace below. Until the Worker exists (#33) the metrics never
# report, and `treat_missing_data` on each alarm says what that means. This
# contract is written down in infra/README.md and docs/deployment.md so the
# Worker author does not have to read Terraform to find the metric names.

locals {
  metric_namespace = "HpacSafety"
}

resource "aws_cloudwatch_log_group" "this" {
  for_each = local.log_groups

  name              = each.value
  retention_in_days = var.log_retention_days

  tags = { Name = each.value }
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
# Alert delivery is an SNS subscription to the configured role mailbox.
resource "aws_sns_topic_subscription" "alarms_email" {
  for_each = toset(var.alarm_email_addresses)

  topic_arn = aws_sns_topic.alarms.arn
  protocol  = "email"
  endpoint  = each.value
}

resource "aws_cloudwatch_metric_alarm" "summary_failed" {
  alarm_name        = "${local.name}-summary-failed"
  alarm_description = "The Worker failed to summarize a report. A real occurrence report is sitting unprocessed. Runbook: docs/deployment.md."

  namespace   = local.metric_namespace
  metric_name = "SummaryFailed"
  statistic   = "Sum"

  comparison_operator = "GreaterThanOrEqualToThreshold"
  threshold           = var.summary_failed_alarm_threshold
  period              = var.summary_failed_alarm_period_seconds
  evaluation_periods  = 1

  # No failures published is the healthy state, not an unknown one.
  treat_missing_data = "notBreaching"

  alarm_actions = [aws_sns_topic.alarms.arn]
  ok_actions    = [aws_sns_topic.alarms.arn]

  tags = { Name = "${local.name}-summary-failed" }
}

resource "aws_cloudwatch_metric_alarm" "outbox_age" {
  alarm_name        = "${local.name}-outbox-age"
  alarm_description = "The oldest unprocessed outbox row is older than the threshold. The Worker is not draining. Runbook: docs/deployment.md."

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
