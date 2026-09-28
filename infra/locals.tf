locals {
  name = var.project

  # The Project tag's value. Not var.project: the name prefix is lowercase
  # (hpac-safety-*), but infra/bootstrap.sh's deploy policy (TAG_VALUE) and
  # every document require the tag Project=HPAC-Safety, and IAM compares tag
  # values case-sensitively. The two must never drift (#617).
  project_tag = "HPAC-Safety"

  # Every resource carries these four (ADR-0158). Environment is the ONE tag
  # that differs between accounts; nothing else about the tag set does — the
  # per-account myApplications name (grouping.tf) is a separate, human-facing
  # label, not a fifth tag.
  tags = {
    Project     = local.project_tag
    Environment = var.environment
    ManagedBy   = "terraform"
    Repo        = "HPAC-Safety/safety-report"
  }

  # AppRegistry's own tag proving myApplications membership (grouping.tf).
  # NOT in providers.tf's default_tags: a provider configuration block cannot
  # reference a managed resource's attribute — providers must be resolvable
  # before Terraform can plan any resource, and application_tag is only known
  # after aws_servicecatalogappregistry_application.this is created. So every
  # OTHER resource in this directory merges this into its own explicit `tags`
  # argument instead (`tags = merge(local.app_tags, { Name = ... })`), which
  # AWS combines with default_tags exactly the same way at apply time. The
  # application resource itself is the one exception — see its own comment.
  app_tags = aws_servicecatalogappregistry_application.this.application_tag

  azs = slice(data.aws_availability_zones.available.names, 0, var.az_count)

  # One /20 per subnet: 10.20.0.0/20, 10.20.16.0/20 public; 10.20.128.0/20,
  # 10.20.144.0/20 private. Room to add a third AZ without renumbering.
  public_subnet_cidrs  = [for i in range(var.az_count) : cidrsubnet(var.vpc_cidr, 4, i)]
  private_subnet_cidrs = [for i in range(var.az_count) : cidrsubnet(var.vpc_cidr, 4, i + 8)]

  # ONE website. The admin review queue is a route on it, not a second site —
  # ADR-0031 supersedes ADR-0009 on this. These two are what everything else
  # derives from, so the shape is stated once.
  #
  #   https://safety.hpac.ca/          the public report form
  #   https://safety.hpac.ca/admin/    the review queue
  #
  # Origin-level isolation between the two is therefore gone. What protects the
  # review queue is the API's authorization (#24), not the delivery path — the
  # admin bundle is static HTML and JS and holds no report data. ADR-0031 has the
  # full assessment and the edge rules that partly compensate.
  admin_prefix = "/${var.admin_path_prefix}"

  # Which external zone a production hostname's DNS records belong to —
  # hpac.ca and acvl.ca are two different organisations' zones, administered
  # outside AWS, so dns_records_to_publish groups by this map rather than by
  # hostname alone (outputs.tf). Staging never reaches this: site_domains is
  # empty there.
  site_zone = { for d in var.site_domains : d => endswith(d, "hpac.ca") ? "hpac.ca" : "acvl.ca" }

  # Log group names, in one place, because the compute resources, the log
  # groups, and the alarms all have to agree on them. The API and the Worker
  # are Lambda functions (lambda.tf, ADR-0042, ADR-0123, #443): Lambda always
  # logs to /aws/lambda/<function name>, so these ARE those functions' names
  # — creating the log group ourselves, ahead of the function, is what puts our
  # retention and prevent_destroy on it instead of an ungoverned default.
  log_groups = {
    api    = "/aws/lambda/${local.name}-api"
    worker = "/aws/lambda/${local.name}-worker"
  }
}
