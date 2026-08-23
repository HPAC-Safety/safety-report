# tflint configuration for the HPAC Safety Terraform.
#
# Versions are pinned in .tflint-version (the tflint binary) and here (the AWS
# ruleset plugin). Each number appears once; the `infra` CI job reads the binary
# version from .tflint-version rather than repeating it, per AGENTS.md.

config {
  call_module_type = "local"
}

plugin "terraform" {
  enabled = true
  preset  = "recommended"
}

plugin "aws" {
  enabled = true
  version = "0.48.0"
  source  = "github.com/terraform-linters/tflint-ruleset-aws"
}

# The Terraform CLI version is pinned in .terraform-version — the one file tfenv,
# asdf, mise, and CI all read. A `required_version` constraint in versions.tf
# would be a second copy of that number, and AGENTS.md is explicit that a version
# pinned twice is a version that will drift. See versions.tf.
rule "terraform_required_version" {
  enabled = false
}
