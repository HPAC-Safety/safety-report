# Application Load Balancer, in front of the API only. The Worker serves no
# traffic and has no load balancer (ADR-0009).

resource "aws_lb" "api" {
  name               = "${local.name}-api"
  load_balancer_type = "application"
  internal           = false
  security_groups    = [aws_security_group.alb.id]
  subnets            = [for s in aws_subnet.public : s.id]

  drop_invalid_header_fields = true
  enable_deletion_protection = false
  idle_timeout               = 60

  tags = { Name = "${local.name}-api" }
}

resource "aws_lb_target_group" "api" {
  name        = "${local.name}-api"
  port        = var.container_port
  protocol    = "HTTP"
  vpc_id      = aws_vpc.main.id
  target_type = "ip"

  # Fargate replaces tasks rather than draining them for long; 30s is enough for
  # in-flight requests without holding a deploy open.
  deregistration_delay = 30

  health_check {
    path                = "/health"
    matcher             = "200"
    interval            = 30
    timeout             = 5
    healthy_threshold   = 2
    unhealthy_threshold = 3
  }

  lifecycle {
    create_before_destroy = true
  }
}

# --------------------------------------------------------------------------
# Listeners
# --------------------------------------------------------------------------
#
# With a domain: 443 terminates TLS and 80 redirects to it. Without one: 80
# forwards directly, because there is no certificate to attach and no hostname
# to redirect to.
#
# The second case is NOT a production configuration — this system receives
# names, phone numbers, and injury details over that port. It exists so the
# module is applyable before HPAC's DNS administrator has published anything,
# and the `api_domain` variable is the thing that closes it.

resource "aws_lb_listener" "https" {
  count = local.has_api_domain ? 1 : 0

  load_balancer_arn = aws_lb.api.arn
  port              = 443
  protocol          = "HTTPS"
  ssl_policy        = "ELBSecurityPolicy-TLS13-1-2-2021-06"
  certificate_arn   = aws_acm_certificate_validation.api[0].certificate_arn

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.api.arn
  }
}

resource "aws_lb_listener" "http_redirect" {
  count = local.has_api_domain ? 1 : 0

  load_balancer_arn = aws_lb.api.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type = "redirect"

    redirect {
      port        = "443"
      protocol    = "HTTPS"
      status_code = "HTTP_301"
    }
  }
}

resource "aws_lb_listener" "http_only" {
  count = local.has_api_domain ? 0 : 1

  load_balancer_arn = aws_lb.api.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.api.arn
  }
}
