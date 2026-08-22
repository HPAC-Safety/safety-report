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

    # State locking is S3-NATIVE: Terraform writes a `.tflock` object with a
    # conditional PutObject, so the bucket that already holds the state also
    # holds the lock. No DynamoDB table, no second service, no second thing to
    # bootstrap.
    #
    # Issue #32 and ADR-0010 both specified a DynamoDB lock table. That clause is
    # superseded — deliberately, before any live state existed to migrate — by
    # ADR-0031. `dynamodb_table` was deprecated in Terraform 1.11 and warns on
    # every `init` from 1.13 onward.
    use_lockfile = true
    encrypt      = true
  }
}
