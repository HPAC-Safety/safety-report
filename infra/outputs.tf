# Outputs.
#
# The issue asks the deploy workflows to read names from state rather than
# duplicating them as GitHub variables — two sources of truth for a bucket name
# is one too many. `deploy_variables` is that single map, shaped so a workflow
# can turn it straight into the environment the deploy steps expect:
#
#   terraform output -json deploy_variables | jq -r 'to_entries[] | "\(.key)=\(.value)"' >> "$GITHUB_ENV"
#
# ONE site bucket and ONE distribution: the admin review queue is a path on the
# website, not a second site, so S3_BUCKET_PUBLIC / S3_BUCKET_ADMIN and
# CLOUDFRONT_DISTRIBUTION_PUBLIC / CLOUDFRONT_DISTRIBUTION_ADMIN — named in #32
# and in docs/deployment.md — are superseded by S3_BUCKET_SITE,
# CLOUDFRONT_DISTRIBUTION_SITE, and SITE_ADMIN_PREFIX. See ADR-0031.
#
# Nothing here is a secret. ARNs and resource names are not credentials, and
# marking them sensitive only makes a failed deploy log unreadable.

output "deploy_variables" {
  description = "Every name and id the deploy workflows need, read from state instead of copied into GitHub variables."

  value = {
    AWS_REGION                   = var.aws_region
    ECR_REPOSITORY_API           = aws_ecr_repository.this["api"].name
    ECR_REPOSITORY_WORKER        = aws_ecr_repository.this["worker"].name
    ECR_REGISTRY                 = split("/", aws_ecr_repository.this["api"].repository_url)[0]
    ECS_CLUSTER                  = aws_ecs_cluster.main.name
    ECS_SERVICE_API              = aws_ecs_service.api.name
    ECS_SERVICE_WORKER           = aws_ecs_service.worker.name
    ECS_TASK_DEFINITION_MIGRATE  = aws_ecs_task_definition.migrate.family
    ECS_SUBNETS                  = join(",", [for s in aws_subnet.private : s.id])
    ECS_SECURITY_GROUPS          = aws_security_group.api.id
    S3_BUCKET_SITE               = aws_s3_bucket.site.id
    S3_BUCKET_UPLOADS            = aws_s3_bucket.uploads.id
    CLOUDFRONT_DISTRIBUTION_SITE = aws_cloudfront_distribution.site.id
    SITE_ADMIN_PREFIX            = var.admin_path_prefix
  }
}

output "site_urls" {
  description = "Where the website answers. ONE site: the public report form at the root, the review queue under the admin prefix. See ADR-0031."

  value = {
    public = "https://${var.site_domain}/"
    admin  = "https://${var.site_domain}${local.admin_prefix}/"
  }
}

output "api_url" {
  description = "Where the API answers. HTTPS only; port 80 redirects."
  value       = "https://${var.api_domain}"
}

output "database_master_password_secret_arn" {
  description = "The secret RDS created and rotates for the master password. Terraform never reads it; this is how an operator finds it in order to assemble the connection string."
  value       = aws_db_instance.main.master_user_secret[0].secret_arn
}

output "secret_entries" {
  description = "The Secrets Manager entries Terraform created. Their VALUES are set out of band with `aws secretsmanager put-secret-value`; Terraform holds none of them."
  value       = { for k, s in aws_secretsmanager_secret.this : k => s.name }
}

output "dns_records_to_publish" {
  description = <<-EOT
    Every DNS record HPAC's DNS administrator has to publish on hpac.ca, in one
    place, because that is an external dependency on another organisation and it
    takes days rather than minutes.

    SPF and DMARC are literal strings rather than computed: they are policy, and
    the DMARC policy in particular is a decision for HPAC. `p=none` here reports
    without rejecting, which is the correct starting point — tighten to
    quarantine and then reject once the reports show only SES sending.
  EOT

  value = {
    ses_dkim = [
      for token in aws_sesv2_email_identity.main.dkim_signing_attributes[0].tokens : {
        type    = "CNAME"
        name    = "${token}._domainkey.${var.ses_domain}"
        value   = "${token}.dkim.amazonses.com"
        purpose = "DKIM. Publishes the public half of the key SES signs with, and verifies the domain identity. All three are required; two out of three is a domain that stays unverified."
      }
    ]

    ses_mail_from = [
      {
        type    = "MX"
        name    = "${var.ses_mail_from_subdomain}.${var.ses_domain}"
        value   = "10 feedback-smtp.${var.aws_region}.amazonses.com"
        purpose = "Where bounces for the custom MAIL FROM subdomain go. On the SUBDOMAIN only — it does not touch the MX for ${var.ses_domain}, so it cannot affect mail delivered TO safety@${var.ses_domain}."
      },
      {
        type    = "TXT"
        name    = "${var.ses_mail_from_subdomain}.${var.ses_domain}"
        value   = "v=spf1 include:amazonses.com ~all"
        purpose = "SPF for the MAIL FROM subdomain, so the envelope sender authenticates and DMARC aligns with the visible From header."
      },
    ]

    dmarc = {
      type    = "TXT"
      name    = "_dmarc.${var.ses_domain}"
      value   = "v=DMARC1; p=none; rua=mailto:dmarc@${var.ses_domain}"
      purpose = "DMARC policy. p=none reports without rejecting, which is the correct starting point; tighten to quarantine then reject once the reports show only SES sending."
    }

    acm_validation = concat(
      [
        for o in aws_acm_certificate.api.domain_validation_options : {
          type    = o.resource_record_type
          name    = o.resource_record_name
          value   = o.resource_record_value
          purpose = "Proves we control ${var.api_domain}, so ACM will issue the API's certificate."
        }
      ],
      [
        for o in aws_acm_certificate.site.domain_validation_options : {
          type    = o.resource_record_type
          name    = o.resource_record_name
          value   = o.resource_record_value
          purpose = "Proves we control ${var.site_domain}, so ACM will issue the website's certificate."
        }
      ],
    )

    aliases = [
      {
        type    = "CNAME"
        name    = var.site_domain
        value   = aws_cloudfront_distribution.site.domain_name
        purpose = "Points the website at CloudFront. One record: the review queue is a path on this host, not a second name."
      },
      {
        type    = "CNAME"
        name    = var.api_domain
        value   = aws_lb.api.dns_name
        purpose = "Points the API at the load balancer."
      },
    ]
  }
}

output "alarm_topic_arn" {
  description = "SNS topic the alarms publish to. Subscribe addresses with the alarm_email_addresses variable, not by hand — a console click is drift."
  value       = aws_sns_topic.alarms.arn
}

output "alarm_subscriptions_pending_confirmation" {
  description = <<-EOT
    Addresses subscribed to the alarm topic. Each is created PENDING
    CONFIRMATION: AWS emails a confirmation link and a human has to click it.
    Terraform cannot do that step and will report the subscription as created
    regardless, so this output exists to make the gap visible.

    Until someone clicks, the alarms fire, are visible in CloudWatch, and email
    nobody.
  EOT

  value = var.alarm_email_addresses
}
