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
    LAMBDA_FUNCTION_API          = aws_lambda_function.api.function_name
    LAMBDA_FUNCTION_WORKER       = aws_lambda_function.worker.function_name
    S3_BUCKET_SITE               = aws_s3_bucket.site.id
    S3_BUCKET_UPLOADS            = aws_s3_bucket.uploads.id
    CLOUDFRONT_DISTRIBUTION_SITE = aws_cloudfront_distribution.site.id
    SITE_ADMIN_PREFIX            = var.admin_path_prefix
  }
}

output "site_urls" {
  description = "Where the website answers. ONE site: the public report form at the root, the review queue under the admin prefix. See ADR-0048 and ADR-0123."

  value = {
    public = "https://${var.site_domain}/"
    admin  = "https://${var.site_domain}${local.admin_prefix}/"
  }
}

output "api_url" {
  description = "The API's Function URL. Not the public entry point — that is CloudFront's /api/* (#465); this is what the origin-secret check refuses direct requests to (ADR-0159)."
  value       = aws_lambda_function_url.api.function_url
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
  EOT

  value = {
    acm_validation = [
      for o in aws_acm_certificate.site.domain_validation_options : {
        type    = o.resource_record_type
        name    = o.resource_record_name
        value   = o.resource_record_value
        purpose = "Proves we control ${var.site_domain}, so ACM will issue the website's certificate."
      }
    ]

    aliases = [
      {
        type = "CNAME"
        name = var.site_domain
        # The API has no domain of its own any more (ADR-0042, ADR-0159): it is
        # reached only through this same CloudFront distribution's /api/*
        # behavior (#465), so there is only ever the one CNAME to publish.
        value   = aws_cloudfront_distribution.site.domain_name
        purpose = "Points the website — and, through it, the API at /api/* — at CloudFront. One record: neither the review queue nor the API is a second name."
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
