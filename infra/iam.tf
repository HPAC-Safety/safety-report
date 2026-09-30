# Runtime IAM. The deploy role is not here — it is created by infra/bootstrap.sh,
# because a workflow cannot create the thing that lets it authenticate.
#
# The API and the Worker are Lambda functions (lambda.tf, ADR-0042, ADR-0123,
# #443), not ECS tasks, so there is no execution-role/task-role split here any
# more — Lambda's own execution role covers both what the ECS agent used to do
# (pull the image implicitly, write logs) and what the application may do,
# and `AWSLambdaVPCAccessExecutionRole` below is the Lambda equivalent of the
# execution role's job. `api_task`/`worker_task` stay as policy DOCUMENTS —
# "what the application may do" hasn't changed — attached to each function's
# own role instead of to a role an ECS task assumed.

data "aws_iam_policy_document" "lambda_assume" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }

    condition {
      test     = "StringEquals"
      variable = "aws:SourceAccount"
      values   = [data.aws_caller_identity.current.account_id]
    }
  }
}

# Scoped to each function's OWN secrets, not every entry in secrets.tf's map:
# Gemini is read by both the API (translation drafts) and the Worker (the
# summary call, and an answer's or a comment's second language, ADR-0179);
# DeepL, dormant but kept so translation can be switched back (issue #614),
# by both as before; the CloudFront origin-verify secret only by the API
# (secrets.tf). Each function
# resolves its own value itself, by ARN, at cold start (SecretArnResolver,
# #597) — granting a function read access to a secret it never opens would
# widen its role for no reason.

data "aws_iam_policy_document" "api_function_secrets_read" {
  statement {
    sid     = "ResolveSecretsAtColdStart"
    effect  = "Allow"
    actions = ["secretsmanager:GetSecretValue"]
    resources = concat(
      [
        aws_secretsmanager_secret.this["deepl_api_key"].arn,
        aws_secretsmanager_secret.this["gemini_api_key"].arn,
        aws_secretsmanager_secret.cloudfront_origin_secret.arn,
      ],
      # The temporary interim issuer's signing key (issue #648, ADR-0172) —
      # present only where var.interim_issuer_enabled created the secret.
      var.interim_issuer_enabled ? [aws_secretsmanager_secret.interim_issuer_signing_key[0].arn] : [],
    )
  }
}

data "aws_iam_policy_document" "worker_function_secrets_read" {
  statement {
    sid     = "ResolveSecretsAtColdStart"
    effect  = "Allow"
    actions = ["secretsmanager:GetSecretValue"]
    resources = [
      aws_secretsmanager_secret.this["deepl_api_key"].arn,
      aws_secretsmanager_secret.this["gemini_api_key"].arn,
    ]
  }
}

# --------------------------------------------------------------------------
# What each application may do
# --------------------------------------------------------------------------
#
# The API writes uploads and reads them back for pre-signed GETs. The Worker
# reads uploads and publishes the two custom metrics the alarms in
# observability.tf watch.

data "aws_iam_policy_document" "api_task" {
  statement {
    sid    = "Uploads"
    effect = "Allow"

    actions = [
      "s3:PutObject",
      "s3:GetObject",
      "s3:DeleteObject",
    ]

    resources = ["${aws_s3_bucket.uploads.arn}/*"]
  }

  # Removing an unclaimed upload erases every version of it, so the bytes do
  # not survive a day as a noncurrent version on this versioned bucket. Fenced
  # to quarantine/: a report's own media is never physically deleted
  # (AGENTS.md invariant 8, ADR-0096).
  statement {
    sid       = "EraseUnclaimedUploads"
    effect    = "Allow"
    actions   = ["s3:DeleteObjectVersion"]
    resources = ["${aws_s3_bucket.uploads.arn}/quarantine/*"]
  }

  statement {
    sid       = "ListUploads"
    effect    = "Allow"
    actions   = ["s3:ListBucket", "s3:ListBucketVersions"]
    resources = [aws_s3_bucket.uploads.arn]
  }

  statement {
    sid       = "Metrics"
    effect    = "Allow"
    actions   = ["cloudwatch:PutMetricData"]
    resources = ["*"]

    condition {
      test     = "StringEquals"
      variable = "cloudwatch:namespace"
      values   = [local.metric_namespace]
    }
  }
}

data "aws_iam_policy_document" "worker_task" {
  statement {
    sid       = "ReadUploads"
    effect    = "Allow"
    actions   = ["s3:GetObject"]
    resources = ["${aws_s3_bucket.uploads.arn}/*"]
  }

  # The Worker writes each attachment's reviewer-safe derivative and nothing
  # else: <report id>/stripped/<file id> (ADR-0098). It never writes an
  # original, never touches quarantine/, and never deletes.
  statement {
    sid       = "WriteDerivatives"
    effect    = "Allow"
    actions   = ["s3:PutObject"]
    resources = ["${aws_s3_bucket.uploads.arn}/*/stripped/*"]
  }

  statement {
    sid       = "Metrics"
    effect    = "Allow"
    actions   = ["cloudwatch:PutMetricData"]
    resources = ["*"]

    condition {
      test     = "StringEquals"
      variable = "cloudwatch:namespace"
      values   = [local.metric_namespace]
    }
  }
}

# --------------------------------------------------------------------------
# Lambda execution roles
# --------------------------------------------------------------------------

resource "aws_iam_role" "api_function" {
  name               = "${local.name}-api-function"
  description        = "Assumed by the API's Lambda function. What it may do once running, and what it may read at cold start."
  assume_role_policy = data.aws_iam_policy_document.lambda_assume.json
}

# Lets Lambda create the function's ENIs in the private subnets and write its
# own CloudWatch log group/stream.
resource "aws_iam_role_policy_attachment" "api_function_vpc" {
  role       = aws_iam_role.api_function.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaVPCAccessExecutionRole"
}

resource "aws_iam_role_policy" "api_function" {
  name   = "api"
  role   = aws_iam_role.api_function.id
  policy = data.aws_iam_policy_document.api_task.json
}

resource "aws_iam_role_policy" "api_function_secrets" {
  name   = "secrets"
  role   = aws_iam_role.api_function.id
  policy = data.aws_iam_policy_document.api_function_secrets_read.json
}

# The API's async nudge (ADR-0123) — the only cross-function permission either
# Lambda role needs.
data "aws_iam_policy_document" "api_function_nudge" {
  statement {
    sid       = "NudgeWorker"
    effect    = "Allow"
    actions   = ["lambda:InvokeFunction"]
    resources = [aws_lambda_function.worker.arn]
  }
}

resource "aws_iam_role_policy" "api_function_nudge" {
  name   = "nudge-worker"
  role   = aws_iam_role.api_function.id
  policy = data.aws_iam_policy_document.api_function_nudge.json
}

resource "aws_iam_role" "worker_function" {
  name               = "${local.name}-worker-function"
  description        = "Assumed by the Worker's Lambda function. What it may do once running, and what it may read at cold start."
  assume_role_policy = data.aws_iam_policy_document.lambda_assume.json
}

resource "aws_iam_role_policy_attachment" "worker_function_vpc" {
  role       = aws_iam_role.worker_function.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaVPCAccessExecutionRole"
}

resource "aws_iam_role_policy" "worker_function" {
  name   = "worker"
  role   = aws_iam_role.worker_function.id
  policy = data.aws_iam_policy_document.worker_task.json
}

resource "aws_iam_role_policy" "worker_function_secrets" {
  name   = "secrets"
  role   = aws_iam_role.worker_function.id
  policy = data.aws_iam_policy_document.worker_function_secrets_read.json
}

# --------------------------------------------------------------------------
# The RDS-managed master-user secret (#443, coordinated with #465)
# --------------------------------------------------------------------------
#
# DatabaseConnectionStringResolver (HpacSafety.Infrastructure) reads this one
# secret itself, at cold start, to assemble the connection string — it is not
# one of secrets.tf's entries, so it needs its own scoped grant rather than
# reusing secrets_read above, which would otherwise widen to every entry in
# that map for no reason either function needs.

data "aws_iam_policy_document" "database_master_secret_read" {
  statement {
    sid       = "ReadDatabaseMasterSecret"
    effect    = "Allow"
    actions   = ["secretsmanager:GetSecretValue"]
    resources = [aws_db_instance.main.master_user_secret[0].secret_arn]
  }
}

resource "aws_iam_role_policy" "api_function_database_secret" {
  name   = "database-master-secret"
  role   = aws_iam_role.api_function.id
  policy = data.aws_iam_policy_document.database_master_secret_read.json
}

resource "aws_iam_role_policy" "worker_function_database_secret" {
  name   = "database-master-secret"
  role   = aws_iam_role.worker_function.id
  policy = data.aws_iam_policy_document.database_master_secret_read.json
}
