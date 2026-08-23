# Provider constraints.
#
# There is deliberately NO `required_version` here. The Terraform CLI version is
# pinned in exactly one file — `.terraform-version`, which tfenv, asdf, mise, and
# the `infra` CI job all read. A `required_version` constraint repeating that
# number would be a second copy that can drift.
#
# tflint's `terraform_required_version` rule is disabled in `.tflint.hcl` for the
# same reason, with the same note.

terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.61"
    }
  }
}
