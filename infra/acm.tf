# Certificates. Both hostnames are HTTPS-only; there is no plaintext path to
# either, and no configuration in which there is.
#
# Two certificates, because CloudFront only accepts one issued in us-east-1 while
# the ALB needs one in the region it lives in. Neither holds report data; both are
# DNS-validated.
#
# THE VALIDATION RECORDS ARE PUBLISHED BY SOMEBODY ELSE. `hpac.ca` is HPAC's
# zone, administered outside this account, so Terraform cannot create the
# validation CNAMEs. The `aws_acm_certificate_validation` resources below WAIT —
# for up to two hours — until those records exist. That is deliberate: the
# alternative is a listener serving a certificate that was never validated.
#
# The records to publish are in the `dns_records_to_publish` output, which is the
# single list the DNS administrator works from. Ask for them early; DNS on
# another organisation's zone takes days, not minutes.

resource "aws_acm_certificate" "api" {
  domain_name       = var.api_domain
  validation_method = "DNS"

  tags = { Name = "${local.name}-api" }

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_acm_certificate_validation" "api" {
  certificate_arn = aws_acm_certificate.api.arn

  timeouts {
    create = "2h"
  }
}

resource "aws_acm_certificate" "site" {
  provider = aws.us_east_1

  domain_name       = var.site_domain
  validation_method = "DNS"

  tags = { Name = "${local.name}-site" }

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_acm_certificate_validation" "site" {
  provider = aws.us_east_1

  certificate_arn = aws_acm_certificate.site.arn

  timeouts {
    create = "2h"
  }
}
