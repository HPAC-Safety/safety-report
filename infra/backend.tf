# Remote state, created independently by infra/bootstrap.sh in each account
# before Terraform ever runs there (ADR-0158: staging and production are
# unrelated AWS accounts, each bootstrapped on its own).
#
# This is a PARTIAL configuration. Neither the bucket name nor the state key is
# here:
#
#   bucket   carries the AWS account id — S3 bucket names are globally unique,
#            so a fixed one would collide with any other account that ran the
#            bootstrap — and an account id is not something to commit.
#   key      is per environment, so the same bucket (staging's account also
#            runs other applications, but each account still gets its own
#            state bucket) can never mix up a staging and a production state
#            file if the two ever shared one bucket by mistake. Committing one
#            fixed key would be exactly the kind of environment-specific value
#            ADR-0158 says belongs only in tfvars, and a backend block cannot
#            read a .tfvars file — Terraform does not evaluate variables before
#            choosing a backend — so it is supplied at init time instead, from
#            the same place the bucket name comes from:
#
#   terraform init \
#     -backend-config="bucket=$TF_STATE_BUCKET" \
#     -backend-config="key=hpac-safety/${ENVIRONMENT}.tfstate"
#
# Everything that is not account- or environment-specific is here, so there is
# one place to read what the backend actually is.
terraform {
  backend "s3" {
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
