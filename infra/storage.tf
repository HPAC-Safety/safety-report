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

  # The website is the one place a reporter attaches a file from (ADR-0126).
  site_origins = length(var.site_origins) > 0 ? var.site_origins : ["https://${var.site_domain}"]
}

# --------------------------------------------------------------------------
# Uploads
# --------------------------------------------------------------------------
#
# A report's attachments: photos, videos, and documents. A crash photo identifies
# a person and a site regardless of how clean the text is, so: no public object
# URL ever, admin views use short-lived pre-signed GETs, and both the original
# bytes and the EXIF-stripped derivative live here. See docs/data-handling.md.

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

# A reporter's browser sends each attachment straight here, through a pre-signed
# PUT the API mints for one quarantine key, signed for the declared type and
# exact length (ADR-0126). That is a cross-origin request from the website, so
# the bucket answers CORS for PUT only, only from the site origins, and only
# with the one header the browser sets itself. Nothing is readable
# cross-origin: reviewer and public reads are top-level navigations or media
# elements, which need no CORS.
resource "aws_s3_bucket_cors_configuration" "uploads" {
  bucket = aws_s3_bucket.uploads.id

  cors_rule {
    allowed_methods = ["PUT"]
    allowed_origins = local.site_origins
    allowed_headers = ["content-type"]
    max_age_seconds = 600
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "uploads" {
  bucket = aws_s3_bucket.uploads.id

  # EVERY upload lands under quarantine/<upload id> the moment a reporter
  # attaches it, sent by the browser through a pre-signed PUT and not yet
  # validated, and is PROMOTED out only when a submission validates and claims
  # it (ADR-0096, ADR-0126). So this rule is what removes an upload
  # nobody claimed: a file on a report the pilot never submitted, a failed
  # submission, a claim whose tidy-up delete failed. Those are bytes of a crash
  # photograph that belong to no report, and nothing else deletes them.
  #
  # A PREFIX, not a tag filter, and that is the load-bearing part. Both ways of
  # applying a tag fail OPEN:
  #
  #   tag at upload   means trusting whoever writes the object to tag it. A
  #                   write path that forgets produces an object that never
  #                   expires.
  #   tag after ingest  means an object whose ingest never ran never gets
  #                   tagged — precisely the case this rule exists for.
  #
  # The prefix fails CLOSED: quarantine/ is the only place an upload is ever
  # written, so an object that got there is covered whatever else went wrong.
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

    # Fifteen days: the same window as the browser's saved report, which is the
    # only thing that names an unclaimed upload (ADR-0100). S3 rounds expiry up
    # to the next midnight UTC, so a key never stops resolving before the draft
    # naming it has expired.
    expiration {
      days = 15
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
    # whose lifecycle claims to clear it.
    #
    # Overlapping rules: the shorter period governs this prefix.
    #
    # Two day-granular hops means two distinct moments, and they are NOT the
    # same number. The key stops resolving after the first; the bytes are gone
    # after the second. docs/deployment.md states both, and neither is a
    # deadline.
    noncurrent_version_expiration {
      noncurrent_days = 1
    }
  }

  # After both hops above, S3 leaves an EXPIRED OBJECT DELETE MARKER behind: a
  # delete marker with no versions under it. Quarantine objects are created
  # continuously, so those markers accumulate indefinitely. The cost is near
  # zero; the clutter is not, because a listing of quarantine/ eventually says
  # more about objects that are gone than about ones that are there.
  #
  # A SEPARATE rule, necessarily: S3 rejects `expired_object_delete_marker`
  # and `days` in the same expiration block.
  rule {
    id     = "expire-quarantine-delete-markers"
    status = "Enabled"

    filter {
      prefix = "quarantine/"
    }

    expiration {
      expired_object_delete_marker = true
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
