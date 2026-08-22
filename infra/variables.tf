# Every variable that has no safe default is either null (the resource that would
# need it is not created) or carries a marked ASSUMPTION comment naming what a
# human still has to confirm. AGENTS.md forbids inventing a requirement quietly;
# where a number had to exist for the code to be written at all, it is labelled
# here and repeated in infra/README.md so nobody has to grep for it.

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
# Null means "not known yet": no alias, no ACM certificate, and CloudFront serves
# on its own *.cloudfront.net name. Setting one adds the certificate and the
# alias, and produces the DNS records HPAC's DNS administrator has to publish.
# --------------------------------------------------------------------------

variable "public_site_domain" {
  description = "Hostname for the public report form, e.g. safety.hpac.ca. Null until decided."
  type        = string
  default     = null
}

variable "admin_site_domain" {
  description = "Hostname for the admin review queue. Null until decided."
  type        = string
  default     = null
}

variable "api_domain" {
  description = "Hostname for the API in front of the ALB. Null until decided — the ALB then listens on HTTP only, which is not a production configuration."
  type        = string
  default     = null
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
  # ASSUMPTION (unconfirmed): ADR-0009 says 'the smallest viable instance sizes
  # are correct here' for an association receiving dozens of reports a year, and
  # db.t4g.micro is the smallest Graviton class RDS PostgreSQL offers.
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
  # ASSUMPTION (unconfirmed): 7 days. Raw reports are retained indefinitely and
  # are the record of a real accident, so this is the window in which an
  # accidental deletion is recoverable. A safety officer may want longer.
  default = 7

  validation {
    condition     = var.db_backup_retention_days >= 1
    error_message = "Automated backups must be on. This database holds the only copy of reports about real accidents."
  }
}

variable "db_multi_az" {
  description = "Run a standby in a second availability zone."
  type        = bool
  # ASSUMPTION (unconfirmed): off. It roughly doubles the RDS bill to shorten an
  # outage of a system that receives dozens of reports a year, and a failed
  # submission is retried by a pilot rather than lost.
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
  description = "Addresses subscribed to the alarm topic. Empty means the topic exists and nothing is subscribed; the alarms still fire and are visible in CloudWatch."
  type        = list(string)
  default     = []
}

variable "summary_failed_alarm_threshold" {
  description = "SummaryFailed count within one period that raises the alarm."
  type        = number
  # ASSUMPTION (unconfirmed): 1. A summarization failure means a real report is
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
  # ASSUMPTION (unconfirmed): 900s. A report waiting a quarter of an hour means
  # the worker is wedged, not that it is busy.
  default = 900
}
