# VPC, subnets, and egress.
#
# Public subnets hold the ALB and the NAT gateway. Nothing else. Private subnets
# hold the Fargate tasks and RDS, and have no route from the internet — the
# database is not reachable from outside the VPC at all, which is the point.

resource "aws_vpc" "main" {
  cidr_block           = var.vpc_cidr
  enable_dns_support   = true
  enable_dns_hostnames = true

  tags = { Name = local.name }
}

resource "aws_internet_gateway" "main" {
  vpc_id = aws_vpc.main.id

  tags = { Name = local.name }
}

resource "aws_subnet" "public" {
  for_each = { for i, az in local.azs : az => i }

  vpc_id            = aws_vpc.main.id
  availability_zone = each.key
  cidr_block        = local.public_subnet_cidrs[each.value]

  # The ALB and the NAT gateway are given explicit EIPs; nothing else lands here,
  # so auto-assignment would only ever help something that should not exist.
  map_public_ip_on_launch = false

  tags = {
    Name = "${local.name}-public-${each.key}"
    Tier = "public"
  }
}

resource "aws_subnet" "private" {
  for_each = { for i, az in local.azs : az => i }

  vpc_id            = aws_vpc.main.id
  availability_zone = each.key
  cidr_block        = local.private_subnet_cidrs[each.value]

  tags = {
    Name = "${local.name}-private-${each.key}"
    Tier = "private"
  }
}

# --------------------------------------------------------------------------
# Egress
# --------------------------------------------------------------------------
#
# ONE NAT gateway, not one per availability zone. A NAT gateway is the single
# largest fixed line on this bill after RDS, and the thing it buys — egress
# surviving the loss of one AZ — protects a workload that processes dozens of
# reports a year. The trade is recorded in ADR-0031: an AZ failure stops
# outbound calls until the NAT is recreated, and inbound report submission (ALB
# to API, API to RDS) keeps working because none of that path traverses it.
#
# Egress is needed at all because the Worker calls a model API. Interface
# endpoints cover ECR, Secrets Manager, and CloudWatch Logs, but there is no
# endpoint for somebody else's public API.

resource "aws_eip" "nat" {
  domain = "vpc"

  tags = { Name = "${local.name}-nat" }
}

resource "aws_nat_gateway" "main" {
  allocation_id = aws_eip.nat.id
  subnet_id     = aws_subnet.public[local.azs[0]].id

  tags = { Name = local.name }

  depends_on = [aws_internet_gateway.main]
}

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.main.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.main.id
  }

  tags = { Name = "${local.name}-public" }
}

resource "aws_route_table" "private" {
  vpc_id = aws_vpc.main.id

  route {
    cidr_block     = "0.0.0.0/0"
    nat_gateway_id = aws_nat_gateway.main.id
  }

  tags = { Name = "${local.name}-private" }
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

  tags = { Name = "${local.name}-s3" }
}
