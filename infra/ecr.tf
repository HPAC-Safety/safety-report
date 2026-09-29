# Container registries.
#
# One per deployable. Images are tagged with the commit SHA CI tested — see
# docs/deployment.md on why `head_sha` and not `github.sha` — so a rollback is
# "deploy that tag", and the lifecycle policy has to keep enough history for
# that to still be true a few weeks later.

locals {
  ecr_repositories = {
    api    = "${local.name}/api"
    worker = "${local.name}/worker"
  }

  # Keep the last 30 SHA-tagged images: at a handful of deploys a week that is
  # roughly a quarter of rollback history. Untagged layers go after a day.
  ecr_lifecycle_policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Expire untagged images after 1 day."
        selection = {
          tagStatus   = "untagged"
          countType   = "sinceImagePushed"
          countUnit   = "days"
          countNumber = 1
        }
        action = { type = "expire" }
      },
      {
        rulePriority = 2
        description  = "Keep the last 30 images."
        selection = {
          tagStatus   = "any"
          countType   = "imageCountMoreThan"
          countNumber = 30
        }
        action = { type = "expire" }
      }
    ]
  })
}

resource "aws_ecr_repository" "this" {
  for_each = local.ecr_repositories

  name = each.value

  # Mutable, because the deploy workflow also moves a `latest` tag. The SHA tags
  # are what a rollback uses and nothing overwrites those.
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }

  encryption_configuration {
    encryption_type = "AES256"
  }

  tags = { Name = "${local.name}-${each.key}" }
}

resource "aws_ecr_lifecycle_policy" "this" {
  for_each = aws_ecr_repository.this

  repository = each.value.name
  policy     = local.ecr_lifecycle_policy
}
