# Outputs.
#
# The issue asks the deploy workflows to read names from state rather than
# duplicating them as GitHub variables — two sources of truth for a bucket name
# is one too many. `deploy_variables` is that single map, shaped so a workflow
# can turn it straight into the environment the deploy steps expect:
#
#   terraform output -json deploy_variables | jq -r 'to_entries[] | "\(.key)=\(.value)"' >> "$GITHUB_ENV"
#
# ONE site bucket and ONE distribution, in each account: the admin review
# queue is a path on the website and the API is a path on the same
# distribution, not separate sites (ADR-0031, ADR-0159). S3_BUCKET_PUBLIC /
# S3_BUCKET_ADMIN and CLOUDFRONT_DISTRIBUTION_PUBLIC /
# CLOUDFRONT_DISTRIBUTION_ADMIN, named in #32, are superseded by
# S3_BUCKET_SITE, CLOUDFRONT_DISTRIBUTION_SITE, and SITE_ADMIN_PREFIX.
#
# Nothing here is a secret. ARNs and resource names are not credentials, and
# marking them sensitive only makes a failed deploy log unreadable.

output "deploy_variables" {
  description = "Every name and id the deploy workflows need, read from state instead of copied into GitHub variables."

  value = {
    AWS_REGION                   = var.aws_region
    ENVIRONMENT                  = var.environment
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
  description = "Where the website answers. ONE site per environment: the public report form at the root, the review queue under the admin prefix. Staging has only the CloudFront default address; production has both hostnames. See ADR-0048, ADR-0123, ADR-0158."

  value = {
    for host in(length(var.site_domains) > 0 ? var.site_domains : [aws_cloudfront_distribution.site.domain_name]) :
    host => {
      public = "https://${host}/"
      admin  = "https://${host}${local.admin_prefix}/"
    }
  }
}

output "api_url" {
  description = "The API's Function URL. Not the public entry point — that is CloudFront's /api/* (ADR-0159); this is what the origin-secret check refuses a direct request to."
  value       = aws_lambda_function_url.api.function_url
}

output "database_master_password_secret_arn" {
  description = "The secret RDS created and rotates for the master password. Terraform never reads it; this is how an operator finds it in order to assemble the connection string."
  value       = aws_db_instance.main.master_user_secret[0].secret_arn
}

output "secret_entries" {
  description = "The Secrets Manager entries Terraform created. Every one but cloudfront_origin_secret has its VALUE set out of band with `aws secretsmanager put-secret-value`; secrets.tf explains why that one entry is the exception."
  value = merge(
    { for k, s in aws_secretsmanager_secret.this : k => s.name },
    { cloudfront_origin_secret = aws_secretsmanager_secret.cloudfront_origin_secret.name }
  )
}

output "dns_records_to_publish" {
  description = <<-EOT
    Every DNS record a human has to publish, grouped by the zone that owns it,
    because each is administered outside AWS by a different organisation and
    this takes days rather than minutes. Empty in staging: there is no
    external DNS entry when site_domains is empty (ADR-0158).
  EOT

  value = length(var.site_domains) == 0 ? {} : {
    for zone in distinct([for d in var.site_domains : local.site_zone[d]]) :
    zone => {
      acm_validation = [
        for o in aws_acm_certificate.site[0].domain_validation_options : {
          type    = o.resource_record_type
          name    = o.resource_record_name
          value   = o.resource_record_value
          purpose = "Proves we control ${o.domain_name}, so ACM will issue the shared production certificate."
        }
        if local.site_zone[o.domain_name] == zone
      ]

      aliases = [
        for d in var.site_domains : {
          type = "CNAME"
          name = d
          # Both hostnames point at the same distribution: the review queue
          # and the API are paths on it, not second names.
          value   = aws_cloudfront_distribution.site.domain_name
          purpose = "Points ${d} — and, through it, /admin and /api/* — at the one CloudFront distribution."
        }
        if local.site_zone[d] == zone
      ]
    }
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
    regardless, so this output exists to make the gap visible. Empty in
    staging: the topic exists there with no subscriber (ADR-0158).

    Until someone clicks, the alarms fire, are visible in CloudWatch, and
    email nobody.
  EOT

  value = var.alarm_email_addresses
}

output "nat_autoscaling_group_arn" {
  description = "The NAT instance's one-instance Auto Scaling group (network.tf). #466's release job deletes and recreates this instance on every release (CON-INF-013) by refreshing this ASG; nothing else in this directory is ever destroyed and recreated by a release."
  value       = module.fck_nat.autoscaling_group_arn
}

output "myapplications" {
  description = "The AppRegistry application and Resource Group grouping every resource in this account (ADR-0158). Grouping and cost visibility only, never a security boundary."

  value = {
    application_name    = aws_servicecatalogappregistry_application.this.name
    application_arn     = aws_servicecatalogappregistry_application.this.arn
    resource_group_name = aws_resourcegroups_group.this.name
    resource_group_arn  = aws_resourcegroups_group.this.arn
  }
}
