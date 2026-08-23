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
# HTTPS only. Port 80 exists to redirect, and does nothing else — this service
# receives names, phone numbers, and injury details, and there is no
# configuration of this module in which any of that crosses the internet in
# plaintext.

resource "aws_lb_listener" "https" {
  load_balancer_arn = aws_lb.api.arn
  port              = 443
  protocol          = "HTTPS"
  ssl_policy        = "ELBSecurityPolicy-TLS13-1-2-2021-06"
  certificate_arn   = aws_acm_certificate_validation.api.certificate_arn

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.api.arn
  }
}

resource "aws_lb_listener" "http_redirect" {
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
