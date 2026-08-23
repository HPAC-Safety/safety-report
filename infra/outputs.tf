# Non-secret values consumed by deployment workflows and operators.

output "deploy_variables" {
  description = "Resource names and ids used by deployment workflows."

  value = {
    AWS_REGION                   = var.aws_region
    ECR_REPOSITORY_API           = aws_ecr_repository.this["api"].name
    ECR_REPOSITORY_WORKER        = aws_ecr_repository.this["worker"].name
    ECR_REGISTRY                 = split("/", aws_ecr_repository.this["api"].repository_url)[0]
    ECS_CLUSTER                  = aws_ecs_cluster.main.name
    ECS_SERVICE_API              = aws_ecs_service.api.name
    ECS_SERVICE_WORKER           = aws_ecs_service.worker.name
    ECS_TASK_DEFINITION_MIGRATE  = aws_ecs_task_definition.migrate.family
    ECS_SUBNETS                  = join(",", [for subnet in aws_subnet.private : subnet.id])
    ECS_SECURITY_GROUPS          = aws_security_group.api.id
    S3_BUCKET_SITE               = aws_s3_bucket.site.id
    S3_BUCKET_UPLOADS            = aws_s3_bucket.uploads.id
    CLOUDFRONT_DISTRIBUTION_SITE = aws_cloudfront_distribution.site.id
    SITE_ADMIN_PREFIX            = var.admin_path_prefix
  }
}

output "site_urls" {
  description = "Public form and admin review URLs."
  value = {
    public = "https://${var.site_domain}/"
    admin  = "https://${var.site_domain}${local.admin_prefix}/"
  }
}

output "api_url" {
  description = "Public API URL."
  value       = "https://${var.api_domain}"
}

output "database_master_password_secret_arn" {
  description = "RDS-managed master-password secret used to assemble the application connection string."
  value       = aws_db_instance.main.master_user_secret[0].secret_arn
}

output "secret_entries" {
  description = "Secrets Manager entry names. Values are set outside Terraform."
  value       = { for key, secret in aws_secretsmanager_secret.this : key => secret.name }
}

output "dns_records_to_publish" {
  description = "Certificate validation and application aliases HPAC must publish."

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
        for option in aws_acm_certificate.api.domain_validation_options : {
          type    = option.resource_record_type
          name    = option.resource_record_name
          value   = option.resource_record_value
          purpose = "Validate ${var.api_domain}."
        }
      ],
      [
        for option in aws_acm_certificate.site.domain_validation_options : {
          type    = option.resource_record_type
          name    = option.resource_record_name
          value   = option.resource_record_value
          purpose = "Validate ${var.site_domain}."
        }
      ],
    )

    aliases = [
      {
        type    = "CNAME"
        name    = var.site_domain
        value   = aws_cloudfront_distribution.site.domain_name
        purpose = "Route the website to CloudFront."
      },
      {
        type    = "CNAME"
        name    = var.api_domain
        value   = aws_lb.api.dns_name
        purpose = "Route the API to its load balancer."
      },
    ]
  }
}

output "alarm_topic_arn" {
  description = "SNS topic for failed or stuck Worker alerts."
  value       = aws_sns_topic.alarms.arn
}

output "alarm_subscriptions_pending_confirmation" {
  description = "Alarm email subscriptions that require human confirmation."
  value       = var.alarm_email_addresses
}
