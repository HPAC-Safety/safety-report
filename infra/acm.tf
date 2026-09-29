# The website's certificate. HTTPS-only; there is no plaintext path, and no
# configuration in which there is.
#
# ONE certificate, PRODUCTION ONLY. CloudFront is the only public entry point
# now that the API has no ALB in front of it (ADR-0042, ADR-0159) — the Lambda
# Function URL is served over TLS by AWS itself and needs no certificate of
# its own. Staging serves only its default *.cloudfront.net address, which
# CloudFront already terminates TLS for with its own default certificate, so
# staging creates no certificate at all (ADR-0158). It never holds report data
# and is DNS-validated.
#
# ONE name covers BOTH production hostnames: safety.hpac.ca and
# securite.acvl.ca sit on one CloudFront distribution, so one us-east-1
# certificate carries both as its domain name and its one subject alternative
# name (ADR-0158).
#
# THE VALIDATION RECORDS ARE PUBLISHED BY SOMEBODY ELSE. hpac.ca and acvl.ca
# are HPAC's zones, administered outside this account, so Terraform cannot
# create the validation CNAMEs. The aws_acm_certificate_validation resource
# below WAITS — for up to two hours — until those records exist. That is
# deliberate: the alternative is a distribution serving a certificate that was
# never validated.
#
# The records to publish are in the dns_records_to_publish output, grouped by
# zone, which is the single list the DNS administrator works from. Ask for
# them early; DNS on another organisation's zone takes days, not minutes.

resource "aws_acm_certificate" "site" {
  count = length(var.site_domains) > 0 ? 1 : 0

  provider = aws.us_east_1

  domain_name               = var.site_domains[0]
  subject_alternative_names = slice(var.site_domains, 1, length(var.site_domains))
  validation_method         = "DNS"

  tags = { Name = "${local.name}-site" }

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_acm_certificate_validation" "site" {
  count = length(var.site_domains) > 0 ? 1 : 0

  provider = aws.us_east_1

  certificate_arn = aws_acm_certificate.site[0].arn

  timeouts {
    create = "2h"
  }
}
