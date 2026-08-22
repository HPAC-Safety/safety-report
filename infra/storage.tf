# S3 buckets: one private uploads bucket, two static site buckets.
#
# All three are fully private. The site buckets are read by CloudFront through an
# Origin Access Control, not by the public — S3 website hosting is not used at
# all, because clean URLs come from a CloudFront Function (ADR-0009) and website
# hosting would require the bucket to be public to work.

locals {
  # Account id in the name for the same reason as the state bucket: S3 names are
  # globally unique, and "hpac-safety-public" is a name somebody else may hold.
  bucket_suffix = data.aws_caller_identity.current.account_id

  site_buckets = {
    public = "${local.name}-public-${local.bucket_suffix}"
    admin  = "${local.name}-admin-${local.bucket_suffix}"
  }
}

# --------------------------------------------------------------------------
# Uploads
# --------------------------------------------------------------------------
#
# One photo or video per report. A crash photo identifies a person and a site
# regardless of how clean the text is, so: no public object URL ever, admin views
# use short-lived pre-signed GETs, and both the original bytes and the
# EXIF-stripped derivative live here. See docs/data-handling.md.

resource "aws_s3_bucket" "uploads" {
  bucket = "${local.name}-uploads-${local.bucket_suffix}"

  tags = { Name = "${local.name}-uploads" }
}

resource "aws_s3_bucket_public_access_block" "uploads" {
  bucket                  = aws_s3_bucket.uploads.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_ownership_controls" "uploads" {
  bucket = aws_s3_bucket.uploads.id

  rule {
    object_ownership = "BucketOwnerEnforced"
  }
}

resource "aws_s3_bucket_versioning" "uploads" {
  bucket = aws_s3_bucket.uploads.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "uploads" {
  bucket = aws_s3_bucket.uploads.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
    bucket_key_enabled = true
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "uploads" {
  bucket = aws_s3_bucket.uploads.id

  rule {
    id     = "abort-incomplete-multipart-uploads"
    status = "Enabled"

    filter {}

    abort_incomplete_multipart_upload {
      days_after_initiation = 7
    }
  }

  # Noncurrent versions exist to undo a mistaken overwrite, not as an archive.
  # Media is retained with the report (docs/data-handling.md); this rule only
  # touches superseded copies.
  rule {
    id     = "expire-noncurrent-versions"
    status = "Enabled"

    filter {}

    noncurrent_version_expiration {
      noncurrent_days = 90
    }
  }
}

resource "aws_s3_bucket_policy" "uploads" {
  bucket = aws_s3_bucket.uploads.id
  policy = data.aws_iam_policy_document.uploads.json

  depends_on = [aws_s3_bucket_public_access_block.uploads]
}

data "aws_iam_policy_document" "uploads" {
  statement {
    sid     = "DenyPlaintextTransport"
    effect  = "Deny"
    actions = ["s3:*"]

    resources = [
      aws_s3_bucket.uploads.arn,
      "${aws_s3_bucket.uploads.arn}/*",
    ]

    principals {
      type        = "*"
      identifiers = ["*"]
    }

    condition {
      test     = "Bool"
      variable = "aws:SecureTransport"
      values   = ["false"]
    }
  }
}

# --------------------------------------------------------------------------
# Static sites
# --------------------------------------------------------------------------

resource "aws_s3_bucket" "site" {
  for_each = local.site_buckets

  bucket = each.value

  tags = { Name = "${local.name}-${each.key}-site" }
}

resource "aws_s3_bucket_public_access_block" "site" {
  for_each = aws_s3_bucket.site

  bucket                  = each.value.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_ownership_controls" "site" {
  for_each = aws_s3_bucket.site

  bucket = each.value.id

  rule {
    object_ownership = "BucketOwnerEnforced"
  }
}

resource "aws_s3_bucket_versioning" "site" {
  for_each = aws_s3_bucket.site

  bucket = each.value.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "site" {
  for_each = aws_s3_bucket.site

  bucket = each.value.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
    bucket_key_enabled = true
  }
}

# Read access is granted to one CloudFront distribution, by ARN. Not to
# CloudFront generally — that would let any distribution in any account read
# these objects.
data "aws_iam_policy_document" "site" {
  for_each = local.site_buckets

  statement {
    sid     = "AllowCloudFrontOriginAccessControl"
    effect  = "Allow"
    actions = ["s3:GetObject"]

    resources = ["${aws_s3_bucket.site[each.key].arn}/*"]

    principals {
      type        = "Service"
      identifiers = ["cloudfront.amazonaws.com"]
    }

    condition {
      test     = "StringEquals"
      variable = "AWS:SourceArn"
      values   = [aws_cloudfront_distribution.site[each.key].arn]
    }
  }

  statement {
    sid     = "DenyPlaintextTransport"
    effect  = "Deny"
    actions = ["s3:*"]

    resources = [
      aws_s3_bucket.site[each.key].arn,
      "${aws_s3_bucket.site[each.key].arn}/*",
    ]

    principals {
      type        = "*"
      identifiers = ["*"]
    }

    condition {
      test     = "Bool"
      variable = "aws:SecureTransport"
      values   = ["false"]
    }
  }
}

resource "aws_s3_bucket_policy" "site" {
  for_each = local.site_buckets

  bucket = aws_s3_bucket.site[each.key].id
  policy = data.aws_iam_policy_document.site[each.key].json

  depends_on = [aws_s3_bucket_public_access_block.site]
}
