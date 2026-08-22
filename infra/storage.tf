# S3 buckets: one private uploads bucket, one static site bucket.
#
# ONE site bucket, not two. The admin review queue is a route on the website —
# objects under the `admin/` key prefix — not a site of its own. ADR-0031
# supersedes ADR-0009's two-bucket, two-distribution design and records what that
# costs.
#
# Both buckets are fully private. The site bucket is read by CloudFront through
# an Origin Access Control, not by the public — S3 website hosting is not used at
# all, because clean URLs come from a CloudFront Function (ADR-0009) and website
# hosting would require the bucket to be public to work.

locals {
  # Account id in the name for the same reason as the state bucket: S3 names are
  # globally unique, and "hpac-safety-site" is a name somebody else may hold.
  bucket_suffix = data.aws_caller_identity.current.account_id

  site_bucket = "${local.name}-site-${local.bucket_suffix}"
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

  # EVERY upload lands under quarantine/ first and is PROMOTED out of it only
  # after ingest validates it — content type sniffed rather than trusted, EXIF
  # stripped. So this rule is not a cleanup for rejected files. It is what
  # removes any upload whose ingest never completed: a rejected file, a crashed
  # ingest, an upload slot issued for a report the pilot never submitted. Those
  # are unverified bytes of a crash photograph sitting in a bucket, and nothing
  # else deletes them.
  #
  # A PREFIX, not a tag filter, and that is the load-bearing part. Both ways of
  # applying a tag fail OPEN:
  #
  #   tag at upload   means signing x-amz-tagging into the pre-signed PUT and
  #                   trusting the browser to send it. A client that omits it
  #                   produces an object that never expires.
  #   tag after ingest  means an object whose ingest never ran never gets
  #                   tagged — precisely the case this rule exists for.
  #
  # The prefix fails CLOSED: quarantine/ is the only place an upload URL can
  # write, so an object that got there is covered whatever else went wrong.
  #
  # The rest of the bucket is report-id-first — <report-id>/original/…,
  # <report-id>/stripped/… — and quarantine/ is the one deliberate departure,
  # for unverified files only. Per-report enumeration is still a single literal
  # prefix. See #16.
  rule {
    id     = "expire-quarantine"
    status = "Enabled"

    filter {
      prefix = "quarantine/"
    }

    expiration {
      days = 1
    }

    abort_incomplete_multipart_upload {
      days_after_initiation = 1
    }

    # NOT part of the rule as specified in #16, and needed for it to do what it
    # says on THIS bucket, because versioning is enabled above.
    #
    # On a versioned bucket, `expiration` does not delete anything: it writes a
    # delete marker and makes the object version noncurrent. Without this clause
    # the bytes would then fall to the bucket-wide 90-day noncurrent rule below
    # — so an unverified crash photograph would survive three months in a bucket
    # whose lifecycle claims to clear it in a day.
    #
    # Overlapping rules: the shorter period governs this prefix.
    noncurrent_version_expiration {
      noncurrent_days = 1
    }
  }

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
# The website
# --------------------------------------------------------------------------
#
# Layout inside the bucket:
#
#   /                    the public report form
#   /admin/              the review queue, under the admin path prefix
#
# The admin objects are not secret: they are the same static HTML and JavaScript
# for every visitor, and every byte of report data arrives from the API, which
# authorizes each request. See ADR-0031.

resource "aws_s3_bucket" "site" {
  bucket = local.site_bucket

  tags = { Name = "${local.name}-site" }
}

resource "aws_s3_bucket_public_access_block" "site" {
  bucket                  = aws_s3_bucket.site.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_ownership_controls" "site" {
  bucket = aws_s3_bucket.site.id

  rule {
    object_ownership = "BucketOwnerEnforced"
  }
}

resource "aws_s3_bucket_versioning" "site" {
  bucket = aws_s3_bucket.site.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "site" {
  bucket = aws_s3_bucket.site.id

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
  statement {
    sid     = "AllowCloudFrontOriginAccessControl"
    effect  = "Allow"
    actions = ["s3:GetObject"]

    resources = ["${aws_s3_bucket.site.arn}/*"]

    principals {
      type        = "Service"
      identifiers = ["cloudfront.amazonaws.com"]
    }

    condition {
      test     = "StringEquals"
      variable = "AWS:SourceArn"
      values   = [aws_cloudfront_distribution.site.arn]
    }
  }

  statement {
    sid     = "DenyPlaintextTransport"
    effect  = "Deny"
    actions = ["s3:*"]

    resources = [
      aws_s3_bucket.site.arn,
      "${aws_s3_bucket.site.arn}/*",
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
  bucket = aws_s3_bucket.site.id
  policy = data.aws_iam_policy_document.site.json

  depends_on = [aws_s3_bucket_public_access_block.site]
}
