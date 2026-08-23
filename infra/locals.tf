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

  # Log group names, in one place, because the task definitions, the log groups,
  # and the alarms all have to agree on them.
  log_groups = {
    api     = "/aws/ecs/${local.name}/api"
    worker  = "/aws/ecs/${local.name}/worker"
    migrate = "/aws/ecs/${local.name}/migrate"
  }
}
