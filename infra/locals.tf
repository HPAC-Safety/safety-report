locals {
  name = var.project

  tags = {
    Project   = var.project
    ManagedBy = "terraform"
    Repo      = "HPAC-Safety/safety-report"
  }

  azs = slice(data.aws_availability_zones.available.names, 0, var.az_count)

  # One /20 per subnet: 10.20.0.0/20, 10.20.16.0/20 public; 10.20.128.0/20,
  # 10.20.144.0/20 private. Room to add a third AZ without renumbering.
  public_subnet_cidrs  = [for i in range(var.az_count) : cidrsubnet(var.vpc_cidr, 4, i)]
  private_subnet_cidrs = [for i in range(var.az_count) : cidrsubnet(var.vpc_cidr, 4, i + 8)]

  # A domain is configured, or it is not. Every alias, certificate, and listener
  # below keys off these rather than repeating the null check.
  has_api_domain    = var.api_domain != null
  has_public_domain = var.public_site_domain != null
  has_admin_domain  = var.admin_site_domain != null
  has_site_domain   = local.has_public_domain || local.has_admin_domain

  site_domains = compact([var.public_site_domain, var.admin_site_domain])

  # Log group names, in one place, because the task definitions, the log groups,
  # and the alarms all have to agree on them.
  log_groups = {
    api     = "/aws/ecs/${local.name}/api"
    worker  = "/aws/ecs/${local.name}/worker"
    migrate = "/aws/ecs/${local.name}/migrate"
  }
}
