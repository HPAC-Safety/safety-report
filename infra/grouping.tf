# Grouping and cost visibility, per account (ADR-0158, ADR-0170). NOT a
# security boundary — the deploy role's IAM policy (infra/bootstrap.sh, #464)
# is what actually keeps staging's deploy role off the account's other,
# unrelated applications; this file only makes the system's resources visible
# together in the console.
#
#   Resource Group (tag-based)   hpac-safety-staging / hpac-safety-production
#
# There is no myApplications (Service Catalog AppRegistry) application: AWS
# closed AppRegistry to accounts that had never used it on 2026-07-30, and
# named a tag-based Resource Group as its replacement (#633, ADR-0170).

# A tag-based query, not an explicit per-resource association: every resource
# already carries Project/Environment (locals.tf), so the group's membership
# is derived from the same tags a policy condition checks, with nothing extra
# to keep in sync as resources are added.
resource "aws_resourcegroups_group" "this" {
  name        = "hpac-safety-${var.environment}"
  description = "Tag-based grouping of every HPAC-Safety resource in this account. See ADR-0158."

  resource_query {
    query = jsonencode({
      ResourceTypeFilters = ["AWS::AllSupported"]
      TagFilters = [
        {
          Key    = "Project"
          Values = [local.project_tag]
        },
        {
          Key    = "Environment"
          Values = [var.environment]
        },
      ]
    })
  }

  tags = { Name = "hpac-safety-${var.environment}" }
}
