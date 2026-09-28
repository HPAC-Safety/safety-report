# Production: a separate, HPAC-owned AWS account, holding real reports
# (ADR-0158). `diff infra/staging.tfvars infra/production.tfvars` is the
# complete list of differences between the two environments; nothing
# environment-specific lives anywhere else in this directory.

environment = "production"

# The two production hostnames, one CloudFront distribution, one us-east-1
# certificate covering both (ADR-0158, acm.tf). safety.hpac.ca is HPAC's own
# zone; securite.acvl.ca is the French-language marketing site's zone. A
# human publishes the CNAME and certificate-validation records the
# dns_records_to_publish output lists, grouped by zone, at each host (issue
# #30, "Human work" H7).
site_domains = ["safety.hpac.ca", "securite.acvl.ca"]

# DECIDED once infra/bootstrap.sh has run against this account (issue #30,
# "Human work" H3): paste the account id bootstrap.sh prints, so the provider
# refuses to apply this file against any other account.
# allowed_account_ids = ["123456789012"]

# 7-day automated backups. Raw reports are retained indefinitely and are the
# record of a real accident, so this is the window in which an accidental
# deletion is recoverable by rolling back rather than by restoring a
# snapshot.
db_backup_retention_days = 7

# The one production alarm subscriber. A role address, so an alarm does not
# stop being read when one person leaves the safety committee. The
# subscription is created PENDING CONFIRMATION — see
# alarm_subscriptions_pending_confirmation and issue #30, "Human work" H8.
alarm_email_addresses = ["safety@hpac.ca"]
