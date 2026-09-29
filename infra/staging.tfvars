# Staging: the owner's existing AWS account, which also runs other,
# unrelated applications. Synthetic data only — never a real report
# (ADR-0158). `diff infra/staging.tfvars infra/production.tfvars` is the
# complete list of differences between the two environments; nothing
# environment-specific lives anywhere else in this directory.

environment = "staging"

# Empty: no custom hostname, no certificate, no external DNS entry. The
# CloudFront default *.cloudfront.net address is the whole story.
site_domains = []

# The account infra/bootstrap.sh ran against (infra/SETUP.md step 2.2, #606):
# the provider refuses to apply this file against any other account.
allowed_account_ids = ["242157301937"]

# 1-day automated backups. Still real backups, still deletion-protected,
# still prevent_destroy (CON-INF-013) — staging holds synthetic data, but the
# Terraform resource itself is not special-cased.
db_backup_retention_days = 1

# No subscriber: the alarm topic exists, but nobody is on call for a
# synthetic-data environment (ADR-0158).
alarm_email_addresses = []

# The temporary interim issuer (issue #648, ADR-0172): staging has no real
# identity provider yet, so the API signs its own RS256 tokens here until one
# exists. Never set in production.tfvars. The two email lists are the same
# accounts already committed, in plain text, in
# src/HpacSafety.Api/appsettings.Development.json (ADR-0079) — not a secret.
interim_issuer_enabled               = true
interim_issuer_administrator_emails  = ["chase.florell@gmail.com", "bertrand.stoffel@gmail.com"]
interim_issuer_safety_officer_emails = []
