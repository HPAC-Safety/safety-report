# Outputs.
#
# The issue asks the deploy workflows to read names from state rather than
# duplicating them as GitHub variables — two sources of truth for a bucket name
# is one too many. `deploy_variables` is that single map, shaped so a workflow
# can turn it straight into the environment the deploy steps expect:
#
#   terraform output -json deploy_variables | jq -r 'to_entries[] | "\(.key)=\(.value)"' >> "$GITHUB_ENV"
#
# Nothing here is a secret. ARNs and resource names are not credentials, and
# marking them sensitive only makes a failed deploy log unreadable.

output "deploy_variables" {
  description = "Every name and id the deploy workflows need, read from state instead of copied into GitHub variables."

  value = {
    AWS_REGION                     = var.aws_region
    ECR_REPOSITORY_API             = aws_ecr_repository.this["api"].name
    ECR_REPOSITORY_WORKER          = aws_ecr_repository.this["worker"].name
    ECR_REGISTRY                   = split("/", aws_ecr_repository.this["api"].repository_url)[0]
    ECS_CLUSTER                    = aws_ecs_cluster.main.name
    ECS_SERVICE_API                = aws_ecs_service.api.name
    ECS_SERVICE_WORKER             = aws_ecs_service.worker.name
    ECS_TASK_DEFINITION_MIGRATE    = aws_ecs_task_definition.migrate.family
    ECS_SUBNETS                    = join(",", [for s in aws_subnet.private : s.id])
    ECS_SECURITY_GROUPS            = aws_security_group.api.id
    S3_BUCKET_PUBLIC               = aws_s3_bucket.site["public"].id
    S3_BUCKET_ADMIN                = aws_s3_bucket.site["admin"].id
    S3_BUCKET_UPLOADS              = aws_s3_bucket.uploads.id
    CLOUDFRONT_DISTRIBUTION_PUBLIC = aws_cloudfront_distribution.site["public"].id
    CLOUDFRONT_DISTRIBUTION_ADMIN  = aws_cloudfront_distribution.site["admin"].id
  }
}

output "site_urls" {
  description = "Where each static site is served from. The *.cloudfront.net name until an alias is configured."

  value = {
    public = local.has_public_domain ? "https://${var.public_site_domain}" : "https://${aws_cloudfront_distribution.site["public"].domain_name}"
    admin  = local.has_admin_domain ? "https://${var.admin_site_domain}" : "https://${aws_cloudfront_distribution.site["admin"].domain_name}"
  }
}

output "api_url" {
  description = "Where the API answers. Plain HTTP on the load balancer's own name until api_domain is set — which is not a production configuration."
  value       = local.has_api_domain ? "https://${var.api_domain}" : "http://${aws_lb.api.dns_name}"
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
        type  = "CNAME"
        name  = "${token}._domainkey.${var.ses_domain}"
        value = "${token}.dkim.amazonses.com"
      }
    ]

    ses_mail_from = [
      {
        type  = "MX"
        name  = "${var.ses_mail_from_subdomain}.${var.ses_domain}"
        value = "10 feedback-smtp.${var.aws_region}.amazonses.com"
      },
      {
        type  = "TXT"
        name  = "${var.ses_mail_from_subdomain}.${var.ses_domain}"
        value = "v=spf1 include:amazonses.com ~all"
      },
    ]

    dmarc = {
      type  = "TXT"
      name  = "_dmarc.${var.ses_domain}"
      value = "v=DMARC1; p=none; rua=mailto:dmarc@${var.ses_domain}"
    }

    acm_validation = concat(
      local.has_api_domain ? [
        for o in aws_acm_certificate.api[0].domain_validation_options : {
          type  = o.resource_record_type
          name  = o.resource_record_name
          value = o.resource_record_value
        }
      ] : [],
      local.has_site_domain ? [
        for o in aws_acm_certificate.site[0].domain_validation_options : {
          type  = o.resource_record_type
          name  = o.resource_record_name
          value = o.resource_record_value
        }
      ] : [],
    )

    aliases = concat(
      local.has_public_domain ? [{
        type  = "CNAME"
        name  = var.public_site_domain
        value = aws_cloudfront_distribution.site["public"].domain_name
      }] : [],
      local.has_admin_domain ? [{
        type  = "CNAME"
        name  = var.admin_site_domain
        value = aws_cloudfront_distribution.site["admin"].domain_name
      }] : [],
      local.has_api_domain ? [{
        type  = "CNAME"
        name  = var.api_domain
        value = aws_lb.api.dns_name
      }] : [],
    )
  }
}

output "alarm_topic_arn" {
  description = "SNS topic the alarms publish to. Subscribe addresses with the alarm_email_addresses variable, not by hand — a console click is drift."
  value       = aws_sns_topic.alarms.arn
}
