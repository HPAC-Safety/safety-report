# Grouping and cost visibility, per account (ADR-0158). NOT a security
# boundary — the deploy role's IAM policy (infra/bootstrap.sh, #464) is what
# actually keeps staging's deploy role off the account's other, unrelated
# applications; this file only makes the system's resources visible together
# in the console.
#
#   myApplications (Service Catalog AppRegistry)   hpac-safety-staging / hpac-safety-production
#   Resource Group (tag-based)                     the same name, same query
#
# HOW A RESOURCE ACTUALLY JOINS THE MYAPPLICATIONS APP. The AWS provider has
# no `aws_servicecatalogappregistry_resource_association` for arbitrary
# resource types — only `aws_servicecatalogappregistry_attribute_group_association`
# exists, which associates an attribute group, not a VPC or an S3 bucket.
# What actually makes myApplications show a resource as a member is the
# `awsApplication` tag AWS looks for, which this resource exports as its
# `application_tag` computed attribute once created. A provider's
# `default_tags` block cannot reference a managed resource's attribute (the
# provider must be resolvable before Terraform can plan anything the
# provider creates), so `locals.tf`'s `app_tags` merges it separately, and
# every OTHER resource in this directory carries `tags = merge(local.app_tags,
# { Name = ... })` in addition to `default_tags`' Project/Environment/
# ManagedBy/Repo — the two combine at apply time exactly the way any
# resource-level tag adds to a provider-level default. This resource itself
# is the one exception (see its own comment): tagging the application with
# its own not-yet-created output would be a cycle.
#
# The Resource Group below is a second, independent membership signal
# (tag-matched, not the `awsApplication` tag) — useful on its own in the
# console, but the myApplications app's real membership comes from the tag
# above, not from this resource.

resource "aws_servicecatalogappregistry_application" "this" {
  name        = "hpac-safety-${var.environment}"
  description = "Every AWS resource HPAC-Safety owns in this account (myApplications). Grouping and cost visibility only — see ADR-0158."

  # NOT merge(local.app_tags, ...): local.app_tags is THIS resource's own
  # computed application_tag output (below), so tagging the application with
  # itself would be a dependency cycle. Every other resource in this
  # directory carries local.app_tags; this one and default_tags are enough
  # for the application to show its own Name.
  tags = { Name = "hpac-safety-${var.environment}" }
}

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
          Values = [var.project]
        },
        {
          Key    = "Environment"
          Values = [var.environment]
        },
      ]
    })
  }

  tags = merge(local.app_tags, { Name = "hpac-safety-${var.environment}" })
}
