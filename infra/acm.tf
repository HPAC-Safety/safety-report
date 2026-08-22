# Certificates.
#
# Two, because CloudFront only accepts a certificate issued in us-east-1 while
# the ALB needs one in the region it lives in. Neither certificate holds report
# data; both are DNS-validated.
#
# THE VALIDATION RECORDS ARE PUBLISHED BY SOMEBODY ELSE. `hpac.ca` is HPAC's
# zone, administered outside this account, so Terraform cannot create the
# validation CNAMEs. The `aws_acm_certificate_validation` resources below WAIT —
# for up to two hours — until those records exist. That is deliberate: the
# alternative is a certificate attached to a listener that silently serves an
# untrusted chain.
#
# The records to publish are `domain_validation_records` in the outputs. Ask for
# them early; DNS on another organisation's zone takes days, not minutes.

resource "aws_acm_certificate" "api" {
  count = local.has_api_domain ? 1 : 0

  domain_name       = var.api_domain
  validation_method = "DNS"

  tags = { Name = "${local.name}-api" }

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_acm_certificate_validation" "api" {
  count = local.has_api_domain ? 1 : 0

  certificate_arn = aws_acm_certificate.api[0].arn

  timeouts {
    create = "2h"
  }
}

resource "aws_acm_certificate" "site" {
  count    = local.has_site_domain ? 1 : 0
  provider = aws.us_east_1

  domain_name               = local.site_domains[0]
  subject_alternative_names = slice(local.site_domains, 1, length(local.site_domains))
  validation_method         = "DNS"

  tags = { Name = "${local.name}-site" }

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_acm_certificate_validation" "site" {
  count    = local.has_site_domain ? 1 : 0
  provider = aws.us_east_1

  certificate_arn = aws_acm_certificate.site[0].arn

  timeouts {
    create = "2h"
  }
}
