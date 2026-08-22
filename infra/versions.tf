# Provider constraints.
#
# There is deliberately NO `required_version` here. The Terraform CLI version is
# pinned in exactly one file — `.terraform-version`, which tfenv, asdf, mise, and
# the `infra` CI job all read — and AGENTS.md is explicit that a tool version
# pinned in two places is a version that will drift. A `required_version`
# constraint repeating that number would be the second copy.
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
