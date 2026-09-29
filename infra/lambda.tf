# The API and the Worker, both container-image Lambda functions in the private
# subnets (ADR-0042, ADR-0123, #443).
#
# WHO OWNS THE IMAGE. Terraform's `image_uri` (`:latest`) matters only when a
# function is first created, and Lambda refuses to create one from an image
# ECR does not hold. So deploy-environment.yml creates the registries and
# pushes `:latest` before the full apply (#623), then updates each function to
# the release's image digest; both functions ignore `image_uri` after that.
#
# WHO OWNS THE ENVIRONMENT. Every value in `environment` is plain, non-secret
# configuration Terraform sets directly — including every secret's ARN (not
# its VALUE, just where to find it). The application reads its own secrets
# itself, by ARN, at cold start, exactly the way DatabaseConnectionStringResolver
# reads the RDS-managed master-user secret (#443/#465): SecretArnResolver in
# HpacSafety.Infrastructure does the same for the CloudFront origin-verify
# secret (API) and the Gemini and DeepL keys (Worker), so none of their
# VALUES ever touches Terraform state, a GitHub secret beyond the one human
# puts into Secrets Manager once, or the Lambda environment (#597). Nothing
# else writes `environment` any more — the deploy workflow only ever updates
# `image_uri` — so Terraform owns it outright; only `image_uri` is ignored
# after the first apply.

locals {
  images = {
    api    = "${aws_ecr_repository.this["api"].repository_url}:latest"
    worker = "${aws_ecr_repository.this["worker"].repository_url}:latest"
  }

  common_environment = [
    # No AWS_REGION: Lambda sets it for every function and refuses it as
    # input (#643).
    { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
    # The key the API binds (HpacSafety:Media:Storage:S3:BucketName). No
    # ServiceUrl and no access key: in AWS the SDK reaches S3 as the
    # function's own role.
    { name = "HpacSafety__Media__Storage__S3__BucketName", value = aws_s3_bucket.uploads.id },

    # None of these four is secret — DatabaseConnectionStringResolver reads
    # the ARN's current value from Secrets Manager itself, at cold start, to
    # assemble the real connection string (#443, coordinated with #465).
    { name = "HpacSafety__Database__Host", value = aws_db_instance.main.address },
    { name = "HpacSafety__Database__Port", value = tostring(aws_db_instance.main.port) },
    { name = "HpacSafety__Database__Name", value = aws_db_instance.main.db_name },
    { name = "HpacSafety__Database__MasterSecretArn", value = aws_db_instance.main.master_user_secret[0].secret_arn },
  ]
}

resource "aws_lambda_function" "api" {
  function_name = "${local.name}-api"
  description   = "The API (ADR-0042). Behind a Function URL CloudFront alone may reach (ADR-0159, #465)."
  role          = aws_iam_role.api_function.arn

  package_type  = "Image"
  image_uri     = local.images.api
  architectures = ["x86_64"]

  memory_size = var.api_memory_mb
  timeout     = var.api_timeout_seconds

  vpc_config {
    subnet_ids         = [for s in aws_subnet.private : s.id]
    security_group_ids = [aws_security_group.api.id]
  }

  logging_config {
    log_format = "Text"
    log_group  = aws_cloudwatch_log_group.this["api"].name
  }

  environment {
    variables = merge(
      { for entry in local.common_environment : entry.name => entry.value },
      {
        # The Lambda Web Adapter (ADR-0042): translates each Function URL
        # event into a loopback HTTP request against Kestrel on this port,
        # and polls this path until the process answers before routing real
        # traffic to it.
        AWS_LWA_PORT                 = tostring(var.container_port)
        AWS_LWA_READINESS_CHECK_PATH = "/health"

        # The Worker's async nudge (ADR-0123) — set once the Worker function
        # exists, below.
        HpacSafety__Worker__Nudge__FunctionName = aws_lambda_function.worker.function_name

        # ARNs only — OriginVerificationServiceCollectionExtensions and
        # AddHpacSafetyTranslation each resolve their own secret's current
        # value from Secrets Manager themselves, at cold start (#597); see
        # this file's header comment.
        HpacSafety__Security__OriginVerification__SecretArn = aws_secretsmanager_secret.cloudfront_origin_secret.arn
        Translation__ApiKeySecretArn                        = aws_secretsmanager_secret.this["deepl_api_key"].arn
      },

      # The temporary interim issuer (issue #648, ADR-0172) — staging only.
      # Empty where var.interim_issuer_enabled is false, so production's API
      # function carries none of this. The flag and the secret's ARN only;
      # AddHpacSafetyAuthentication resolves the signing key's value itself,
      # at cold start, the same way as every other secret above. The email
      # lists are not secret — see interim_issuer_administrator_emails.
      var.interim_issuer_enabled ? merge(
        {
          HpacSafety__Authentication__InterimIssuer__Enabled             = "true"
          HpacSafety__Authentication__InterimIssuer__SigningKeySecretArn = aws_secretsmanager_secret.interim_issuer_signing_key[0].arn
        },
        { for idx, email in var.interim_issuer_administrator_emails : "MembersSiteLogin__AdministratorEmails__${idx}" => email },
        { for idx, email in var.interim_issuer_safety_officer_emails : "MembersSiteLogin__SafetyOfficerEmails__${idx}" => email },
      ) : {}
    )
  }

  tags = { Name = "${local.name}-api" }

  lifecycle {
    ignore_changes = [image_uri]
  }
}

# authorization_type = NONE is the only choice Lambda Function URLs offer
# besides AWS_IAM, and AWS_IAM would require CloudFront to sign every request,
# which it cannot do for a custom origin. NONE means the URL itself is public;
# what actually keeps a caller out is the origin-secret header CloudFront's
# origin request policy injects and the API verifies before doing anything
# else (ADR-0159). CloudFront's side of that wiring is #465's.
resource "aws_lambda_function_url" "api" {
  function_name      = aws_lambda_function.api.function_name
  authorization_type = "NONE"
}

# Required for a NONE-auth Function URL to answer at all — this is Lambda's own
# resource policy, separate from (and in addition to) the origin-secret check
# the API itself performs. Without it, every request is refused before the
# function even runs.
resource "aws_lambda_permission" "api_function_url_public" {
  statement_id           = "AllowPublicFunctionUrlInvocation"
  action                 = "lambda:InvokeFunctionUrl"
  function_name          = aws_lambda_function.api.function_name
  principal              = "*"
  function_url_auth_type = "NONE"
}

resource "aws_lambda_function" "worker" {
  function_name = "${local.name}-worker"
  description   = "The Worker (ADR-0123): drains due outbox messages and returns. Invoked by the API's async nudge and by the EventBridge sweep below."
  role          = aws_iam_role.worker_function.arn

  package_type  = "Image"
  image_uri     = local.images.worker
  architectures = ["x86_64"]

  memory_size = var.worker_memory_mb
  timeout     = var.worker_timeout_seconds

  ephemeral_storage {
    size = var.worker_ephemeral_storage_mb
  }

  vpc_config {
    subnet_ids         = [for s in aws_subnet.private : s.id]
    security_group_ids = [aws_security_group.worker.id]
  }

  logging_config {
    log_format = "Text"
    log_group  = aws_cloudwatch_log_group.this["worker"].name
  }

  environment {
    variables = merge(
      { for entry in local.common_environment : entry.name => entry.value },
      {
        # The one application metric this system emits, OutboxOldestAgeSeconds
        # (issue #467) — the namespace observability.tf's one remaining
        # app-metric alarm reads.
        Metrics__Namespace = local.metric_namespace

        # ARNs only — AddHpacSafetyAiChatClient and AddHpacSafetyTranslation
        # each resolve their own secret's current value from Secrets Manager
        # themselves, at cold start (#597); see this file's header comment.
        AiChatClient__ApiKeySecretArn = aws_secretsmanager_secret.this["gemini_api_key"].arn
        Translation__ApiKeySecretArn  = aws_secretsmanager_secret.this["deepl_api_key"].arn
      }
    )
  }

  tags = { Name = "${local.name}-worker" }

  lifecycle {
    ignore_changes = [image_uri]
  }
}

# --------------------------------------------------------------------------
# EventBridge Scheduler: the one-minute sweep that guarantees delivery
# --------------------------------------------------------------------------
#
# A lost or throttled nudge only delays work until this runs (ADR-0123). It
# invokes the Worker directly — no payload, same as the API's own nudge — so
# a sweep and a nudge are indistinguishable to the function once it starts.

data "aws_iam_policy_document" "scheduler_assume" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["scheduler.amazonaws.com"]
    }

    condition {
      test     = "StringEquals"
      variable = "aws:SourceAccount"
      values   = [data.aws_caller_identity.current.account_id]
    }
  }
}

resource "aws_iam_role" "worker_sweep" {
  name               = "${local.name}-worker-sweep"
  description        = "Assumed by EventBridge Scheduler to invoke the Worker once a minute (ADR-0123)."
  assume_role_policy = data.aws_iam_policy_document.scheduler_assume.json
}

data "aws_iam_policy_document" "worker_sweep" {
  statement {
    effect    = "Allow"
    actions   = ["lambda:InvokeFunction"]
    resources = [aws_lambda_function.worker.arn]
  }
}

resource "aws_iam_role_policy" "worker_sweep" {
  name   = "invoke-worker"
  role   = aws_iam_role.worker_sweep.id
  policy = data.aws_iam_policy_document.worker_sweep.json
}

resource "aws_scheduler_schedule" "worker_sweep" {
  name       = "${local.name}-worker-sweep"
  group_name = "default"

  # ADR-0123 measures the sweep against the Worker's own claim rules, not
  # exact-once delivery, so a slightly-late invocation while one is still
  # running is fine — it will simply claim nothing that is not yet due.
  flexible_time_window {
    mode = "OFF"
  }

  schedule_expression = var.worker_sweep_schedule_expression

  target {
    arn      = aws_lambda_function.worker.arn
    role_arn = aws_iam_role.worker_sweep.arn

    retry_policy {
      maximum_retry_attempts = 0
    }
  }
}
