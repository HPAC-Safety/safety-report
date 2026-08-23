# Example variable values. Copy to production.tfvars — which is gitignored — and
# override only what you actually want to change. Every value here is a
# NON-SECRET configuration choice; a secret in a .tfvars file is a secret in
# Terraform state, and there is nowhere in this directory that a secret value
# belongs. See ADR-0010.
#
# Nothing here is required. Every default in variables.tf has been decided by the
# repository owner and is marked DECIDED with the reasoning, so an empty
# production.tfvars is the correct production configuration.

# The two hostnames. ONE website: the review queue is a route on it, under the
# admin path prefix, not a site of its own. See ADR-0031.
# site_domain       = "safety.hpac.ca"
# api_domain        = "api.hpac.ca"
# admin_path_prefix = "admin"

# Database. ADR-0009 says the smallest viable sizes are correct here.
# db_instance_class        = "db.t4g.micro"
# db_backup_retention_days = 7
# db_multi_az              = false

# Who hears an alarm. A role address, never a personal one — and the subscription
# is pending until a human clicks the confirmation link AWS emails to it.
# alarm_email_addresses = ["safety@hpac.ca"]

# Alarm thresholds. See the metric contract in infra/README.md — the Worker has
# to publish these two metrics or the alarms watch nothing.
# summary_failed_alarm_threshold = 1
# outbox_age_alarm_seconds       = 900
