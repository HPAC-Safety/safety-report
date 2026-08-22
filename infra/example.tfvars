# Example variable values. Copy to production.tfvars — which is gitignored — and
# fill in. Every value here is a NON-SECRET configuration choice; a secret in a
# .tfvars file is a secret in Terraform state, and there is nowhere in this
# directory that a secret value belongs. See ADR-0010.
#
# Nothing here is required: every variable has a default, and the defaults marked
# ASSUMPTION in variables.tf are the ones a human still has to confirm. They are
# listed in infra/README.md under "Values a human still has to decide".

# Hostnames. Null (the default) means no alias and no certificate: CloudFront
# serves on *.cloudfront.net and the ALB listens on plain HTTP, which is not a
# production configuration for a service receiving names and injury details.
# public_site_domain = "safety.hpac.ca"
# admin_site_domain  = "safety-admin.hpac.ca"
# api_domain         = "safety-api.hpac.ca"

# Database. ADR-0009 says the smallest viable sizes are correct here.
# db_instance_class        = "db.t4g.micro"
# db_backup_retention_days = 7
# db_multi_az              = false

# Who hears an alarm. Empty means the alarms still fire and are visible in
# CloudWatch, and nobody is emailed.
# alarm_email_addresses = ["safety@hpac.ca"]

# Alarm thresholds. See the metric contract in infra/README.md — the Worker has
# to publish these two metrics or the alarms watch nothing.
# summary_failed_alarm_threshold = 1
# outbox_age_alarm_seconds       = 900
