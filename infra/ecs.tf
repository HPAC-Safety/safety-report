# ECS cluster, task definitions, and the two Fargate services.
#
# Containers are built by `dotnet publish /t:PublishContainer` — .NET 10 emits an
# OCI image directly, so there is no Dockerfile for this file to agree with.
#
# WHO OWNS THE IMAGE TAG. Terraform defines the task definition; the deploy
# workflow registers a NEW REVISION of it with the commit SHA CI tested and
# updates the service. If Terraform also asserted the revision, every deploy
# would show up as drift on the next plan and every apply would roll the service
# back to `latest`. So the services below ignore `task_definition`, and the
# `image` here is only ever the shape of a first boot. See ADR-0031.

locals {
  # The container images the FIRST apply points at. Nothing has been pushed at
  # that point, so the service will not stabilise until the first deploy runs —
  # which is why docs/deployment.md orders it: apply, push, deploy.
  images = {
    api    = "${aws_ecr_repository.this["api"].repository_url}:latest"
    worker = "${aws_ecr_repository.this["worker"].repository_url}:latest"
  }

  # Injected by the ECS agent from Secrets Manager, by ARN. The values are not
  # here and are not in state — see secrets.tf.
  common_secrets = [
    {
      name      = "ConnectionStrings__Default"
      valueFrom = aws_secretsmanager_secret.this["connection_string"].arn
    },
  ]

  common_environment = [
    { name = "AWS_REGION", value = var.aws_region },
    { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
    { name = "Storage__UploadsBucket", value = aws_s3_bucket.uploads.id },
  ]
}

resource "aws_ecs_cluster" "main" {
  name = local.name

  setting {
    name  = "containerInsights"
    value = "enhanced"
  }

  tags = { Name = local.name }
}

resource "aws_ecs_cluster_capacity_providers" "main" {
  cluster_name       = aws_ecs_cluster.main.name
  capacity_providers = ["FARGATE"]

  default_capacity_provider_strategy {
    capacity_provider = "FARGATE"
    weight            = 1
  }
}

# --------------------------------------------------------------------------
# API
# --------------------------------------------------------------------------

resource "aws_ecs_task_definition" "api" {
  family                   = "${local.name}-api"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.api_cpu
  memory                   = var.api_memory
  execution_role_arn       = aws_iam_role.task_execution.arn
  task_role_arn            = aws_iam_role.api_task.arn

  runtime_platform {
    cpu_architecture        = "X86_64"
    operating_system_family = "LINUX"
  }

  container_definitions = jsonencode([
    {
      name      = "api"
      image     = local.images.api
      essential = true

      portMappings = [
        {
          containerPort = var.container_port
          protocol      = "tcp"
        }
      ]

      environment = local.common_environment
      secrets = concat(local.common_secrets, [
        {
          name      = "Turnstile__SecretKey"
          valueFrom = aws_secretsmanager_secret.this["turnstile_secret_key"].arn
        },
      ])

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = local.log_groups.api
          "awslogs-region"        = var.aws_region
          "awslogs-stream-prefix" = "api"
        }
      }
    }
  ])

  tags = { Name = "${local.name}-api" }

  lifecycle {
    # The deploy workflow registers revisions of this family. Terraform owns the
    # shape, not the revision.
    ignore_changes = [container_definitions]
  }
}

resource "aws_ecs_service" "api" {
  name            = "${local.name}-api"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.api.arn
  desired_count   = var.api_desired_count
  launch_type     = "FARGATE"

  # A report submission that fails because a task was replaced mid-request is a
  # pilot who may not come back and file it again.
  deployment_minimum_healthy_percent = 100
  deployment_maximum_percent         = 200
  health_check_grace_period_seconds  = 60
  enable_execute_command             = false
  propagate_tags                     = "SERVICE"

  network_configuration {
    subnets          = [for s in aws_subnet.private : s.id]
    security_groups  = [aws_security_group.api.id]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.api.arn
    container_name   = "api"
    container_port   = var.container_port
  }

  tags = { Name = "${local.name}-api" }

  lifecycle {
    ignore_changes = [task_definition, desired_count]
  }

  depends_on = [
    aws_lb_listener.https,
    aws_lb_listener.http_only,
  ]
}

# --------------------------------------------------------------------------
# Worker
# --------------------------------------------------------------------------
#
# No load balancer, no ingress, no public IP. It claims outbox rows FOR UPDATE
# SKIP LOCKED and calls out to Anthropic. See ADR-0002 and ADR-0003.

resource "aws_ecs_task_definition" "worker" {
  family                   = "${local.name}-worker"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.worker_cpu
  memory                   = var.worker_memory
  execution_role_arn       = aws_iam_role.task_execution.arn
  task_role_arn            = aws_iam_role.worker_task.arn

  runtime_platform {
    cpu_architecture        = "X86_64"
    operating_system_family = "LINUX"
  }

  container_definitions = jsonencode([
    {
      name      = "worker"
      image     = local.images.worker
      essential = true
      environment = concat(local.common_environment, [
        { name = "Ses__ConfigurationSet", value = aws_sesv2_configuration_set.main.configuration_set_name },
        { name = "Ses__FromDomain", value = var.ses_domain },
        { name = "Metrics__Namespace", value = local.metric_namespace },
      ])

      secrets = concat(local.common_secrets, [
        {
          name      = "Anthropic__ApiKey"
          valueFrom = aws_secretsmanager_secret.this["anthropic_api_key"].arn
        },
        {
          name      = "Notifications__To"
          valueFrom = aws_secretsmanager_secret.this["notifications_to"].arn
        },
      ])

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = local.log_groups.worker
          "awslogs-region"        = var.aws_region
          "awslogs-stream-prefix" = "worker"
        }
      }
    }
  ])

  tags = { Name = "${local.name}-worker" }

  lifecycle {
    ignore_changes = [container_definitions]
  }
}

resource "aws_ecs_service" "worker" {
  name            = "${local.name}-worker"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.worker.arn
  desired_count   = var.worker_desired_count
  launch_type     = "FARGATE"

  # A worker may be stopped before its replacement is up: the outbox row it was
  # holding is released and another run claims it.
  deployment_minimum_healthy_percent = 0
  deployment_maximum_percent         = 100
  enable_execute_command             = false
  propagate_tags                     = "SERVICE"

  network_configuration {
    subnets          = [for s in aws_subnet.private : s.id]
    security_groups  = [aws_security_group.worker.id]
    assign_public_ip = false
  }

  tags = { Name = "${local.name}-worker" }

  lifecycle {
    ignore_changes = [task_definition, desired_count]
  }
}

# --------------------------------------------------------------------------
# Migrations
# --------------------------------------------------------------------------
#
# Its own task definition, run as a one-off by deploy-api.yml between pushing the
# image and updating the service. Migrations deliberately do NOT run at
# application startup: two API tasks booting together would both take the
# migration lock and the loser either crashes or serves a half-migrated schema.
# See docs/deployment.md.

resource "aws_ecs_task_definition" "migrate" {
  family                   = "${local.name}-migrate"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.api_cpu
  memory                   = var.api_memory
  execution_role_arn       = aws_iam_role.task_execution.arn
  task_role_arn            = aws_iam_role.api_task.arn

  runtime_platform {
    cpu_architecture        = "X86_64"
    operating_system_family = "LINUX"
  }

  container_definitions = jsonencode([
    {
      name        = "migrate"
      image       = local.images.api
      essential   = true
      command     = ["--migrate"]
      environment = local.common_environment
      secrets     = local.common_secrets

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = local.log_groups.migrate
          "awslogs-region"        = var.aws_region
          "awslogs-stream-prefix" = "migrate"
        }
      }
    }
  ])

  tags = { Name = "${local.name}-migrate" }

  lifecycle {
    ignore_changes = [container_definitions]
  }
}
