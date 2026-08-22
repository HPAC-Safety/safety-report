# SES: domain identity, DKIM, and a configuration set.
#
# The Worker sends one kind of mail: a notification to the safety committee that
# a report is waiting for review. Those mails carry a LINK, never the report —
# an inbox is outside this system's access controls (docs/data-handling.md).
#
# Two things here need a human and cannot be Terraform:
#
#   1. SES starts in SANDBOX. Until production access is granted, mail only
#      reaches individually verified addresses, so safety@hpac.ca receives
#      nothing. The request is reviewed by a person and takes a day or more.
#   2. The verification, DKIM, SPF, and DMARC records go on `hpac.ca`, a zone
#      administered by HPAC and not by this account. The values are in the
#      `dns_records_to_publish` output.
#
# Neither is a gap in this file. Both are listed in docs/deployment.md.

resource "aws_sesv2_email_identity" "main" {
  email_identity         = var.ses_domain
  configuration_set_name = aws_sesv2_configuration_set.main.configuration_set_name

  dkim_signing_attributes {
    # Easy DKIM: AWS holds and rotates the key material and publishes three
    # CNAMEs to follow. BYODKIM would mean a private key, and a private key would
    # mean a value in Terraform state — the thing ADR-0010 exists to prevent.
    next_signing_key_length = "RSA_2048_BIT"
  }

  tags = { Name = local.name }
}

# A custom MAIL FROM subdomain so the envelope sender is on hpac.ca and SPF
# aligns with the visible From header. Without it SPF authenticates
# amazonses.com, DMARC alignment fails, and an alert about a fatality lands in
# a spam folder.
resource "aws_sesv2_email_identity_mail_from_attributes" "main" {
  email_identity = aws_sesv2_email_identity.main.email_identity

  mail_from_domain = "${var.ses_mail_from_subdomain}.${var.ses_domain}"

  # If the MX record is missing, fall back to amazonses.com rather than
  # refusing to send. A missed notification is worse than an unaligned one.
  behavior_on_mx_failure = "USE_DEFAULT_VALUE"
}

resource "aws_sesv2_configuration_set" "main" {
  configuration_set_name = local.name

  delivery_options {
    # Refuse to send unencrypted. There is no fallback to plaintext SMTP.
    tls_policy = "REQUIRE"
  }

  reputation_options {
    reputation_metrics_enabled = true
  }

  sending_options {
    sending_enabled = true
  }

  suppression_options {
    # A hard bounce or a complaint suppresses the address account-wide. The
    # committee mailbox is a small, known list; sending at a dead address is how
    # a domain's reputation goes.
    suppressed_reasons = ["BOUNCE", "COMPLAINT"]
  }

  tags = { Name = local.name }
}

# Bounces and complaints go to CloudWatch, where the alarm in observability.tf
# can see them. Not to an SNS topic nobody reads.
resource "aws_sesv2_configuration_set_event_destination" "cloudwatch" {
  configuration_set_name = aws_sesv2_configuration_set.main.configuration_set_name
  event_destination_name = "cloudwatch"

  event_destination {
    enabled              = true
    matching_event_types = ["SEND", "DELIVERY", "BOUNCE", "COMPLAINT", "REJECT", "RENDERING_FAILURE"]

    cloud_watch_destination {
      dimension_configuration {
        dimension_name          = "ses:configuration-set"
        dimension_value_source  = "MESSAGE_TAG"
        default_dimension_value = local.name
      }
    }
  }
}
