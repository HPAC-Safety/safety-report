# Certificates. HTTPS-only; there is no plaintext path, and no configuration in
# which there is.
#
# ONE certificate: CloudFront needs one issued in us-east-1, and CloudFront is
# the only public entry point now that the API has no ALB in front of it
# (ADR-0042, ADR-0159) — the Lambda Function URL is served over TLS by AWS
# itself and needs no certificate of its own. It never holds report data and is
# DNS-validated.
#
# THE VALIDATION RECORDS ARE PUBLISHED BY SOMEBODY ELSE. `hpac.ca` is HPAC's
# zone, administered outside this account, so Terraform cannot create the
# validation CNAMEs. The `aws_acm_certificate_validation` resource below WAITS —
# for up to two hours — until those records exist. That is deliberate: the
# alternative is a distribution serving a certificate that was never validated.
#
# The records to publish are in the `dns_records_to_publish` output, which is the
# single list the DNS administrator works from. Ask for them early; DNS on
# another organisation's zone takes days, not minutes.

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
