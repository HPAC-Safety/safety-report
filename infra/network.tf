# VPC, subnets, and egress.
#
# Public subnets hold only the NAT instance's network interface and its
# Elastic IP. Nothing else lands here: the API and the Worker are Lambda
# functions in the private subnets (lambda.tf), reached only through CloudFront
# — there is no ALB (ADR-0159). Private subnets hold the Lambda functions and
# RDS, and have no route from the internet — the database is not reachable
# from outside the VPC at all, which is the point.

resource "aws_vpc" "main" {
  cidr_block           = var.vpc_cidr
  enable_dns_support   = true
  enable_dns_hostnames = true

  tags = merge(local.app_tags, { Name = local.name })
}

resource "aws_internet_gateway" "main" {
  vpc_id = aws_vpc.main.id

  tags = merge(local.app_tags, { Name = local.name })
}

resource "aws_subnet" "public" {
  for_each = { for i, az in local.azs : az => i }

  vpc_id            = aws_vpc.main.id
  availability_zone = each.key
  cidr_block        = local.public_subnet_cidrs[each.value]

  # Only the NAT instance's ENI gets a public address, and it gets one through
  # its own Elastic IP (below), not auto-assignment.
  map_public_ip_on_launch = false

  tags = merge(local.app_tags, {
    Name = "${local.name}-public-${each.key}"
    Tier = "public"
  })
}

resource "aws_subnet" "private" {
  for_each = { for i, az in local.azs : az => i }

  vpc_id            = aws_vpc.main.id
  availability_zone = each.key
  cidr_block        = local.private_subnet_cidrs[each.value]

  tags = merge(local.app_tags, {
    Name = "${local.name}-private-${each.key}"
    Tier = "private"
  })
}

# --------------------------------------------------------------------------
# Egress: a NAT instance, not a managed NAT gateway (ADR-0158, CON-INF-013)
# --------------------------------------------------------------------------
#
# ONE NAT instance, not one per availability zone — the same trade ADR-0031
# made for the NAT gateway this replaces: an AZ failure stops outbound calls
# until the instance is recreated (which the one-instance ASG below does on
# its own), and inbound report submission (CloudFront to the API's Function
# URL, API to RDS) keeps working because none of that path traverses it.
#
# Egress is needed at all because the API validates a member's token against
# the identity provider's published signing keys and calls DeepL, and the
# Worker calls Gemini and DeepL. The S3 gateway endpoint below covers uploads
# traffic; there is no endpoint for somebody else's public API.
#
# fck-nat (t4g.nano) instead of aws_nat_gateway: roughly $4/month — the
# instance plus its Elastic IP — against roughly $36/month for a managed NAT
# gateway, for a resource that holds no data and is stateless by design. THE
# ONE RESOURCE THIS SYSTEM EVER DELETES AND RECREATES: every release replaces
# it, so it never runs long enough to drift from the pinned AMI or fill up,
# and the one-instance Auto Scaling group heals it on its own between
# releases if it fails a health check. Everything else in this directory is
# created once and updated in place (CON-INF-013). The documented fallback,
# if this project is ever abandoned, is swapping this module for
# aws_nat_gateway — a one-resource change, not a redesign.
#
# The module and the AMI are pinned SEPARATELY (variables.tf) so Renovate can
# bump either without the other silently following: the module version is
# this Terraform wrapper's own release; the AMI version is fck-nat's own
# image release. A floating "most recent AMI" lookup would let the running
# image drift on every apply without anyone deciding to move it.

data "aws_ami" "fck_nat" {
  owners = ["568608671756"] # fck-nat's own publishing account (fck-nat.dev)

  filter {
    name   = "name"
    values = ["fck-nat-al2023-hvm-${var.fck_nat_ami_version}-arm64-ebs"]
  }

  filter {
    name   = "architecture"
    values = ["arm64"]
  }
}

resource "aws_eip" "nat" {
  domain = "vpc"

  tags = merge(local.app_tags, { Name = "${local.name}-nat" })
}

module "fck_nat" {
  source = "RaJiska/fck-nat/aws"
  # Exact, not "~> 1.6": a third-party module must not float on its own, even
  # within a minor range. Renovate's terraform manager opens a pull request to
  # bump this; a human reviews it like any other version bump.
  version = "1.6.1"

  name      = "${local.name}-nat"
  vpc_id    = aws_vpc.main.id
  subnet_id = aws_subnet.public[local.azs[0]].id

  ami_id        = data.aws_ami.fck_nat.id
  instance_type = var.nat_instance_type

  eip_allocation_ids = [aws_eip.nat.id]

  # A one-instance Auto Scaling group replaces a failed instance on its own
  # health check, with no human and no redeploy (CON-INF-013). Not the same
  # thing as multi-AZ resilience — it is still one instance, in one subnet —
  # only self-healing between releases.
  ha_mode = true

  # This module can update the route tables itself; Terraform still owns
  # creating them (below), so the route each references is only the default
  # egress route, matching what aws_nat_gateway.main used to own directly.
  update_route_tables = true
  route_tables_ids = {
    private = aws_route_table.private.id
  }

  tags = merge(local.app_tags, { Name = "${local.name}-nat" })

  depends_on = [aws_internet_gateway.main]
}

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.main.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.main.id
  }

  tags = merge(local.app_tags, { Name = "${local.name}-public" })
}

# No 0.0.0.0/0 route defined here for the private table: the fck-nat module
# adds it (route_tables_ids, above) against the NAT instance's own network
# interface, and repoints it every time the instance is replaced — the same
# job aws_nat_gateway.main's static route used to do, except this one has to
# follow a resource that is deliberately recreated on every release.
resource "aws_route_table" "private" {
  vpc_id = aws_vpc.main.id

  tags = merge(local.app_tags, { Name = "${local.name}-private" })

  lifecycle {
    # The fck-nat module manages the default route on this table out-of-band
    # (route_tables_ids above); ignoring it here is what stops every plan from
    # proposing to remove the route the module just added.
    ignore_changes = [route]
  }
}

resource "aws_route_table_association" "public" {
  for_each = aws_subnet.public

  subnet_id      = each.value.id
  route_table_id = aws_route_table.public.id
}

resource "aws_route_table_association" "private" {
  for_each = aws_subnet.private

  subnet_id      = each.value.id
  route_table_id = aws_route_table.private.id
}

# --------------------------------------------------------------------------
# VPC endpoints
# --------------------------------------------------------------------------
#
# S3 through a gateway endpoint costs nothing and keeps uploads — which are
# photographs of crash sites — off the public internet entirely.

resource "aws_vpc_endpoint" "s3" {
  vpc_id            = aws_vpc.main.id
  service_name      = "com.amazonaws.${var.aws_region}.s3"
  vpc_endpoint_type = "Gateway"
  route_table_ids   = [aws_route_table.private.id]

  tags = merge(local.app_tags, { Name = "${local.name}-s3" })
}
