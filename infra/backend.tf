# Remote state, created by infra/bootstrap.sh before Terraform ever runs.
#
# This is a PARTIAL configuration. The bucket name carries the AWS account id —
# S3 bucket names are globally unique, so a fixed one would collide with any
# other account that ran the bootstrap — and an account id is not something to
# commit. It is supplied at init time:
#
#   terraform init -backend-config="bucket=$TF_STATE_BUCKET"
#
# Everything that is not account-specific is here, so there is one place to read
# what the backend actually is.
terraform {
  backend "s3" {
    key    = "hpac-safety/production.tfstate"
    region = "ca-central-1"

    # State locking. The issue and ADR-0010 both specify a DynamoDB lock table,
    # and infra/bootstrap.sh creates it, so that is what this uses.
    #
    # NOTE: Terraform 1.11 deprecated `dynamodb_table` in favour of `use_lockfile`
    # (native S3 conditional-write locking, no second service), and 1.13+ emits a
    # deprecation warning on every `init`. Migrating is a one-line change plus
    # deleting the table, but it is a deliberate deviation from the accepted ADR
    # rather than something to slip in here. Tracked in ADR-0031.
    dynamodb_table = "hpac-safety-tfstate-lock"
    encrypt        = true
  }
}
