# Every value here has been decided by the repository owner. Where a default was
# originally a guess it is now marked DECIDED with what was chosen, because
# skills/clarify-requirements/SKILL.md requires an answer to be captured in
# the pull request that received it — an answer given twice was not recorded the
# first time.
#
# No default in this file is a guess any more. If you add a variable whose value
# you had to invent, mark it plainly and say so in the pull request body.
#
# staging.tfvars and production.tfvars are the ONLY place the two environments
# differ (ADR-0158). `diff infra/staging.tfvars infra/production.tfvars` must be
# the complete story — if you find yourself branching on `var.environment`
# anywhere outside a variable default or a validation message, that is a defect.

variable "environment" {
  description = "Which of the two environments this is. Drives nothing by name in resource logic — only which tfvars file supplied the other variables below (ADR-0158)."
  type        = string

  validation {
    condition     = contains(["staging", "production"], var.environment)
    error_message = "environment must be \"staging\" or \"production\". There are exactly two, and no third."
  }
}

variable "project" {
  description = "Name prefix for every resource (lowercase; the Project tag is locals.tf's project_tag, HPAC-Safety). Identical in both accounts — they are separate AWS accounts, so the name never has to disambiguate an environment (see ADR-0158's deploy-role IAM, which scopes on this prefix)."
  type        = string
  default     = "hpac-safety"
}

variable "aws_region" {
  description = "Region for everything that touches report data."
  type        = string
  default     = "ca-central-1"

  validation {
    # Region choice here is a data-protection decision, not an infrastructure
    # preference — reports name real people and PIPEDA applies. ADR-0009 and
    # docs/data-handling.md both say not to move it. This makes "not to" cheap
    # to enforce and expensive to do by accident.
    condition     = var.aws_region == "ca-central-1"
    error_message = "Report data stays in ca-central-1. See docs/data-handling.md and ADR-0009 before changing this."
  }
}

variable "allowed_account_ids" {
  description = <<-EOT
    Guard against applying the wrong environment's tfvars against the wrong AWS
    account: if non-empty, the provider refuses to run against any account not
    in this list. Empty (the default) means no guard, which is the only
    possible value before an account exists to name — infra/bootstrap.sh prints
    the account id once it has run, and that id belongs in this environment's
    tfvars from then on. Never a value invented in this file: it is one
    specific account, decided by which account bootstrap ran in.
  EOT
  type        = list(string)
  default     = []
}

# --------------------------------------------------------------------------
# Network
# --------------------------------------------------------------------------

variable "vpc_cidr" {
  description = "CIDR block for the VPC."
  type        = string
  default     = "10.20.0.0/16"
}

variable "az_count" {
  description = "How many availability zones to spread across."
  type        = number
  default     = 2

  validation {
    condition     = var.az_count >= 2 && var.az_count <= 3
    error_message = "ca-central-1 has three availability zones; use 2 or 3."
  }
}

# --------------------------------------------------------------------------
# Domains
#
# DECIDED. Two hostnames in production, none in staging (ADR-0158). The admin
# review queue is a ROUTE on the website, not a site of its own — ADR-0031
# supersedes ADR-0009's "one distribution each for public and admin" — and the
# API is reached only at /api/* on the same distribution
# (ADR-0159): there is no api_domain and no second certificate.
# --------------------------------------------------------------------------

variable "site_domains" {
  description = <<-EOT
    Hostnames CloudFront answers to, in addition to its own *.cloudfront.net
    address. Empty in staging: the cloudfront.net address is the whole story,
    no certificate needed, no external DNS entry. Exactly
    ["safety.hpac.ca", "securite.acvl.ca"] in production: one CloudFront
    distribution, one us-east-1 ACM certificate covering both names
    (ADR-0158). Never a third name and never one name — see
    dns_records_to_publish for what a human publishes at each hostname's zone.
  EOT
  type        = list(string)
  default     = []

  validation {
    condition     = length(var.site_domains) == 0 || length(var.site_domains) == 2
    error_message = "site_domains is empty (staging) or exactly the production pair — safety.hpac.ca and securite.acvl.ca. Never one name and never a third."
  }
}

variable "site_origins" {
  description = "Origins a browser may PUT an attachment to the uploads bucket from, through the pre-signed URL the API mints (ADR-0126). Scheme and host, no path. Empty means every https://<site_domains> host, or the CloudFront default address in staging where site_domains is empty."
  type        = list(string)
  default     = []

  validation {
    condition     = alltrue([for origin in var.site_origins : can(regex("^https://[a-z0-9.-]+(:[0-9]+)?$", origin))])
    error_message = "Each site origin is https://host or https://host:port, with no path and no trailing slash."
  }
}

variable "admin_path_prefix" {
  description = "Path prefix the admin review queue is served under, without slashes. Drives the CloudFront cache behavior and the response headers policy, so it is defined once here rather than written into two places."
  type        = string
  default     = "admin"

  validation {
    condition     = can(regex("^[a-z0-9-]+$", var.admin_path_prefix))
    error_message = "The admin path prefix is a single path segment: lowercase letters, digits, and hyphens, with no slashes."
  }
}

# --------------------------------------------------------------------------
# Database
# --------------------------------------------------------------------------

variable "db_engine_version" {
  description = "PostgreSQL major version. Major only, so a minor upgrade is not a Terraform diff."
  type        = string
  default     = "17"
}

variable "db_instance_class" {
  description = "RDS instance class."
  type        = string
  # DECIDED: db.t4g.micro, in both environments. The smallest Graviton class RDS
  # PostgreSQL offers, and correct for an association receiving dozens of
  # reports a year — including in production (ADR-0158's "sized the same way").
  default = "db.t4g.micro"
}

variable "db_allocated_storage" {
  description = "Initial storage in GiB."
  type        = number
  default     = 20
}

variable "db_max_allocated_storage" {
  description = "Ceiling for RDS storage autoscaling, in GiB."
  type        = number
  default     = 100
}

variable "db_backup_retention_days" {
  description = "Automated backup retention, in days. DECIDED per environment (ADR-0158): 1 in staging.tfvars, 7 in production.tfvars — this variable is the whole difference; the resource itself is identical."
  type        = number
  default     = 7

  validation {
    condition     = var.db_backup_retention_days >= 1
    error_message = "Automated backups must be on, in every environment. See CON-INF-013: a Terraform plan that would turn them off fails review."
  }
}

variable "db_multi_az" {
  description = "Run a standby in a second availability zone."
  type        = bool
  # DECIDED: off, in both environments. It roughly doubles the RDS bill to
  # shorten an outage of a system that receives dozens of reports a year, and a
  # failed submission is retried by a pilot rather than lost. Out of scope per
  # issue #30 and issue #465.
  default = false
}

# --------------------------------------------------------------------------
# Compute
#
# Both the API and the Worker are Lambda functions (ADR-0042, ADR-0123, #443):
# no CPU units, no desired count — Lambda scales invocations on its own, and
# the outbox's FOR UPDATE SKIP LOCKED claim already makes overlapping
# invocations safe. Identical in both environments; #465 does not split these
# per environment because nothing about #30's traffic estimate needs it yet.
# --------------------------------------------------------------------------

variable "api_memory_mb" {
  description = "Lambda memory for the API function. Bursty, sub-second request handling; the smallest size that keeps the Lambda Web Adapter's cold start reasonable."
  type        = number
  default     = 1024
}

variable "api_timeout_seconds" {
  description = "Lambda timeout for the API function. Every request today completes in well under this; nothing in features/README.md needs a long-lived connection (ADR-0042)."
  type        = number
  default     = 30
}

variable "worker_memory_mb" {
  description = "Lambda memory for the Worker function, sized for a 250 MB video remux streamed to and from /tmp rather than buffered in memory (issue #462, #443)."
  type        = number
  default     = 3008
}

variable "worker_timeout_seconds" {
  description = "Lambda timeout for the Worker function. A remux is at most two minutes (ADR-0123); this leaves room for a full drain pass across several due messages before the EventBridge sweep's next minute arrives."
  type        = number
  default     = 300
}

variable "worker_ephemeral_storage_mb" {
  description = "The Worker's /tmp size. A 250 MB original plus its remuxed derivative, with headroom, never the full Lambda ceiling (issue #462, #443)."
  type        = number
  default     = 2048
}

variable "worker_sweep_schedule_expression" {
  description = "The EventBridge Scheduler expression that invokes the Worker as the delivery guarantee behind the API's async nudge (ADR-0123)."
  type        = string
  default     = "rate(1 minute)"
}

variable "container_port" {
  description = "Port the API's container listens on. The Lambda Web Adapter (AWS_LWA_PORT) forwards each Function URL event to it as a loopback HTTP request (ADR-0042)."
  type        = number
  default     = 8080
}

# --------------------------------------------------------------------------
# NAT instance (CON-INF-013): fck-nat on a t4g.nano, in a one-instance ASG,
# the one resource this system ever deletes and recreates. See network.tf.
# --------------------------------------------------------------------------

variable "nat_instance_type" {
  description = "Instance type for the NAT instance."
  type        = string
  # DECIDED: t4g.nano — a NAT gateway would cost roughly $36/month per account
  # against roughly $4/month for this instance plus its public IPv4 address
  # (ADR-0158). Nothing at HPAC's traffic needs more than a nano's 5 Gbps
  # burst ceiling.
  default = "t4g.nano"
}

variable "fck_nat_ami_version" {
  description = <<-EOT
    Pinned fck-nat AMI build (published by AWS account 568608671756, image
    name fck-nat-al2023-hvm-<version>-<build date>-arm64-ebs), as
    "<version>-<build date>": fck-nat publishes several dated builds of one
    version, so the date is what names exactly one image. List them with
    `aws ec2 describe-images --owners 568608671756 --filters
    'Name=name,Values=fck-nat-al2023-*-arm64-ebs'`. Deliberately not "most recent" —
    a floating lookup would let the NAT instance's image drift on every
    release without anyone deciding to move it. The module version itself is
    pinned as a literal in network.tf's `module "fck_nat" { version = ... }`,
    because a module block's version argument must be a literal Terraform can
    resolve before any variable is evaluated; Renovate's terraform manager
    bumps that literal directly. Bump this AMI version alongside it when that
    pull request opens — the two numbers are not required to match, but
    reviewing them together is the point.
  EOT
  type        = string
  default     = "1.4.0-20260701"
}

# --------------------------------------------------------------------------
# Observability
# --------------------------------------------------------------------------

variable "log_retention_days" {
  description = "CloudWatch log retention. Application logs never contain report content — see docs/data-handling.md — so this is an operational window, not a personal-data one."
  type        = number
  default     = 90
}

variable "alarm_email_addresses" {
  description = "Addresses subscribed to the alarm topic. A role address, never a personal one. DECIDED per environment (ADR-0158): empty in staging.tfvars (the topic exists, unsubscribed — staging never holds a real report and nobody is on call for it), [\"safety@hpac.ca\"] in production.tfvars."
  type        = list(string)
  default     = []
}

variable "outbox_age_alarm_seconds" {
  description = "Age of the oldest unprocessed outbox row that raises the alarm."
  type        = number
  # DECIDED: 900s, over two consecutive periods. A report waiting a quarter of an
  # hour means the worker is wedged, not that it is busy.
  default = 900
}

# --------------------------------------------------------------------------
# Issue #467 — the owner's alarm thresholds (2026-09-28 decision), scaled
# back to four alarms total for a lightly used system, every one tunable per
# environment in tfvars.
# --------------------------------------------------------------------------

variable "lambda_error_alarm_threshold" {
  description = "Lambda Errors count within one period that raises an alarm, for the API and the Worker alike."
  type        = number
  # DECIDED: any error in five minutes — not an average to smooth out, a
  # single one is a real request or a real piece of work that failed.
  default = 1
}

variable "lambda_error_alarm_period_seconds" {
  description = "Evaluation period for the Lambda Errors alarms."
  type        = number
  default     = 300
}

variable "nat_unhealthy_alarm_period_seconds" {
  description = "Evaluation period for the NAT Auto Scaling group health alarm."
  type        = number
  # DECIDED: five minutes, one period. The ASG's own EC2 health check already
  # folds a failed status check into a lower in-service count, so one alarm
  # on GroupInServiceInstances covers both conditions the owner named.
  default = 300
}
