# Provider configuration for both regions this system ever touches, and the
# guard that stops staging.tfvars or production.tfvars from being applied
# against the wrong AWS account (ADR-0158: the two environments are two
# unrelated accounts, never linked, so applying one environment's plan against
# the other's account is exactly the mistake allowed_account_ids exists to
# make loud and immediate instead of silent).

provider "aws" {
  region = var.aws_region

  allowed_account_ids = var.allowed_account_ids

  default_tags {
    tags = local.tags
  }
}

# CloudFront only accepts an ACM certificate issued in us-east-1. This alias
# exists for that one purpose and manages no data. Nothing that touches report
# data may be created through it — see docs/data-handling.md.
provider "aws" {
  alias  = "us_east_1"
  region = "us-east-1"

  allowed_account_ids = var.allowed_account_ids

  default_tags {
    tags = local.tags
  }
}

data "aws_caller_identity" "current" {}

data "aws_availability_zones" "available" {
  state = "available"
}
