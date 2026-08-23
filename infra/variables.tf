# Every value here has been decided by the repository owner. Where a default was
# originally a guess it is now marked DECIDED with what was chosen, because
# skills/clarify-hpac-requirements/SKILL.md requires an answer to be captured in
# the pull request that received it — an answer given twice was not recorded the
# first time.
#
# No default in this file is a guess any more. If you add a variable whose value
# you had to invent, mark it plainly and say so in the pull request body.

variable "project" {
  description = "Name prefix for every resource. Also the Project tag."
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

# --------------------------------------------------------------------------
# Network
# --------------------------------------------------------------------------

variable "vpc_cidr" {
  description = "CIDR block for the VPC."
  type        = string
  default     = "10.20.0.0/16"
}

variable "az_count" {
  description = "How many availability zones to spread across. RDS multi-AZ and the ALB both need at least two."
  type        = number
  default     = 2

  validation {
    condition     = var.az_count >= 2 && var.az_count <= 3
    error_message = "An ALB needs subnets in at least two availability zones, and ca-central-1 has three."
  }
}

# --------------------------------------------------------------------------
# Domains
#
# DECIDED. Two hostnames, not three: the admin review queue is a ROUTE on the
# website, not a site of its own. See ADR-0031, which supersedes ADR-0009's
# "one distribution each for public and admin".
# --------------------------------------------------------------------------

variable "site_domain" {
  description = "The website. Serves the public report form at / and the admin review queue under the admin path prefix."
  type        = string
  default     = "safety.hpac.ca"
}

variable "api_domain" {
  description = "The API, in front of the ALB. HTTPS only; port 80 redirects."
  type        = string
  default     = "api.hpac.ca"
}

variable "admin_path_prefix" {
  description = "Path prefix the admin review queue is served under, without slashes. Drives the CloudFront cache behavior, the response headers policy, and the URL-rewrite function, so it is defined once here rather than written into three places."
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
  # DECIDED: db.t4g.micro. ADR-0009 says 'the smallest viable instance sizes are
  # correct here' for an association receiving dozens of reports a year, and this
  # is the smallest Graviton class RDS PostgreSQL offers.
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
  description = "Automated backup retention, in days."
  type        = number
  # DECIDED: 7 days. Raw reports are retained indefinitely and are the record of a
  # real accident, so this is the window in which an accidental deletion is
  # recoverable by rolling back rather than by restoring a snapshot.
  default = 7

  validation {
    condition     = var.db_backup_retention_days >= 1
    error_message = "Automated backups must be on. This database holds the only copy of reports about real accidents."
  }
}

variable "db_multi_az" {
  description = "Run a standby in a second availability zone."
  type        = bool
  # DECIDED: off. It roughly doubles the RDS bill to shorten an outage of a system
  # that receives dozens of reports a year, and a failed submission is retried by
  # a pilot rather than lost.
  default = false
}

# --------------------------------------------------------------------------
# Compute
# --------------------------------------------------------------------------

variable "api_cpu" {
  description = "Fargate CPU units for the API task."
  type        = number
  default     = 512
}

variable "api_memory" {
  description = "Fargate memory (MiB) for the API task."
  type        = number
  default     = 1024
}

variable "api_desired_count" {
  description = "How many API tasks to run."
  type        = number
  default     = 1
}

variable "worker_cpu" {
  description = "Fargate CPU units for the Worker task."
  type        = number
  default     = 512
}

variable "worker_memory" {
  description = "Fargate memory (MiB) for the Worker task."
  type        = number
  default     = 1024
}

variable "worker_desired_count" {
  description = "How many Worker tasks to run. More than one is safe — the outbox is claimed FOR UPDATE SKIP LOCKED — but unnecessary at this volume."
  type        = number
  default     = 1
}

variable "container_port" {
  description = "Port the API container listens on."
  type        = number
  default     = 8080
}

# --------------------------------------------------------------------------
# Mail
# --------------------------------------------------------------------------

variable "ses_domain" {
  description = "Domain SES sends as. Verification, DKIM, SPF, and DMARC records for it are published by HPAC's DNS administrator."
  type        = string
  default     = "hpac.ca"
}

variable "ses_mail_from_subdomain" {
  description = "Subdomain used as the MAIL FROM domain, so SPF aligns with the From header."
  type        = string
  default     = "mail"
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
  description = "Addresses subscribed to the alarm topic. A role address, never a personal one."
  type        = list(string)
  # DECIDED: safety@hpac.ca — the single production address for this system, for
  # operational alarms and for report notifications alike. A role address, so an
  # alarm does not stop being read when one person leaves the safety committee.
  #
  # An email subscription is created PENDING CONFIRMATION and Terraform cannot
  # complete it: AWS sends a confirmation link to the address and a human has to
  # click it. Until someone does, the alarms fire, are visible in CloudWatch, and
  # email nobody. That step is in docs/deployment.md's manual-steps table.
  default = ["safety@hpac.ca"]
}

variable "summary_failed_alarm_threshold" {
  description = "SummaryFailed count within one period that raises the alarm."
  type        = number
  # DECIDED: 1 in five minutes. A summarization failure means a real report is
  # sitting unprocessed, which is not a thing to average out over an hour.
  default = 1
}

variable "summary_failed_alarm_period_seconds" {
  description = "Evaluation period for the SummaryFailed alarm."
  type        = number
  default     = 300
}

variable "outbox_age_alarm_seconds" {
  description = "Age of the oldest unprocessed outbox row that raises the alarm."
  type        = number
  # DECIDED: 900s, over two consecutive periods. A report waiting a quarter of an
  # hour means the worker is wedged, not that it is busy.
  default = 900
}
