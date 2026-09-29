# ONE CloudFront distribution, serving the website and, at /api/*, the API.
#
# ADR-0009 specified two distributions — one public, one admin — "so the admin
# surface can take network controls the public form must not have". That is
# superseded: the admin review queue is a ROUTE on the website, under the admin
# path prefix. ADR-0048 reinstated that shape and ADR-0123 confirms it, serving
# the site from S3 behind this distribution. ADR-0159 adds the API: there is
# no ALB anywhere in front of it, in either account — CloudFront routes
# /api/* to the API's Lambda Function URL instead.
#
# WAF, geo restriction, and IP allowlisting are DISTRIBUTION-level in
# CloudFront, so collapsing what would otherwise be three origins (public
# form, admin route, API) into one distribution means those can no longer be
# applied to any one of them alone. What can still be applied per path is a
# cache behavior, an origin, and a response headers policy — all three are
# used below. That is an acceptable trade only because the admin bundle is
# static HTML and JavaScript containing no report data, and because the API
# authorizes every request itself (#24): the delivery path was never the
# security boundary.
#
# ONE HOSTNAME SET PER ENVIRONMENT (ADR-0158). Production answers to
# safety.hpac.ca and securite.acvl.ca, on one us-east-1 certificate (acm.tf).
# Staging answers only to its own *.cloudfront.net address — no aliases, no
# certificate needed, CloudFront's own default certificate covers it.

resource "aws_cloudfront_origin_access_control" "site" {
  name                              = "${local.name}-site"
  description                       = "Signs CloudFront's requests to the site bucket, which is otherwise private."
  origin_access_control_origin_type = "s3"
  signing_behavior                  = "always"
  signing_protocol                  = "sigv4"
}

# The admin area gets its own response headers policy, adding two things to the
# managed security headers the public form gets:
#
#   X-Robots-Tag        the review queue must never be indexed. robots.txt is
#                       content and lives in src/web; this is the header that
#                       does not depend on anyone remembering to write it.
#   Cache-Control       no-store, so no intermediary holds an admin response.
#                       The bundle is identical for every reviewer, so this
#                       protects little on its own — it is here so that the day
#                       something user-specific is added to that path, the
#                       default is already right.
resource "aws_cloudfront_response_headers_policy" "admin" {
  name    = "${local.name}-admin"
  comment = "Security headers for the admin route, plus noindex and no-store."

  security_headers_config {
    content_type_options {
      override = true
    }

    frame_options {
      frame_option = "DENY"
      override     = true
    }

    referrer_policy {
      referrer_policy = "same-origin"
      override        = true
    }

    strict_transport_security {
      access_control_max_age_sec = 31536000
      include_subdomains         = true
      preload                    = false
      override                   = true
    }

    xss_protection {
      protection = true
      mode_block = true
      override   = true
    }
  }

  custom_headers_config {
    items {
      header   = "X-Robots-Tag"
      value    = "noindex, nofollow"
      override = true
    }

    items {
      header   = "Cache-Control"
      value    = "no-store"
      override = true
    }
  }
}

resource "aws_cloudfront_distribution" "site" {
  enabled             = true
  is_ipv6_enabled     = true
  comment             = "${local.name} website — public form at /, review queue at ${local.admin_prefix}/, API at /api/*"
  default_root_object = "index.html"

  # North America and Europe. Reports are filed by Canadians about incidents
  # mostly in Canada; paying for edge locations in every region would buy
  # latency nobody experiences.
  price_class = "PriceClass_100"

  # Empty in staging: no alias, only the default *.cloudfront.net address
  # (ADR-0158).
  aliases = var.site_domains

  # --------------------------------------------------------------------------
  # Origins
  # --------------------------------------------------------------------------

  origin {
    origin_id                = "s3-site"
    domain_name              = aws_s3_bucket.site.bucket_regional_domain_name
    origin_access_control_id = aws_cloudfront_origin_access_control.site.id
  }

  # The API's Lambda Function URL (ADR-0159). NONE auth at the AWS layer — a
  # Function URL cannot itself verify a CloudFront-signed request — so the
  # origin_custom_header below is what actually stops a caller who bypasses
  # CloudFront and hits the Function URL directly.
  #
  # THE HEADER VALUE COMES FROM random_password.cloudfront_origin_secret
  # (secrets.tf), not a variable and not a literal: this is the one value in
  # the whole directory Terraform originates itself rather than leaving for a
  # human to supply, because both readers of it — this config and the API,
  # which reads it from Secrets Manager at deploy time — must agree on the
  # exact same value, and CloudFront cannot read Secrets Manager at request
  # time to check. secrets.tf's header comment explains why this is the one
  # documented exception to "entries, never values". Nothing here is a
  # first-apply placeholder and nothing needs ignore_changes: Terraform
  # manages the real value on both sides, every apply.
  origin {
    origin_id   = "api"
    domain_name = trimsuffix(trimprefix(aws_lambda_function_url.api.function_url, "https://"), "/")

    custom_origin_config {
      http_port              = 80
      https_port             = 443
      origin_protocol_policy = "https-only"
      origin_ssl_protocols   = ["TLSv1.2"]
    }

    custom_header {
      name  = "X-Origin-Verify"
      value = random_password.cloudfront_origin_secret.result
    }
  }

  # The public report form.
  default_cache_behavior {
    target_origin_id       = "s3-site"
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD", "OPTIONS"]
    cached_methods         = ["GET", "HEAD"]
    compress               = true

    # AWS managed policies, by id, because a hand-rolled equivalent is a thing to
    # maintain for no gain:
    #   CachingOptimized             658327ea-f89d-4fab-a63d-7e88639e58f6
    #   SecurityHeadersPolicy        67f7725c-6f97-4210-82d7-5512b31e9d03
    cache_policy_id            = "658327ea-f89d-4fab-a63d-7e88639e58f6"
    response_headers_policy_id = "67f7725c-6f97-4210-82d7-5512b31e9d03"
  }

  # The review queue. Same origin, different rules — this is the per-path
  # isolation that survives collapsing what used to be two distributions into
  # one.
  ordered_cache_behavior {
    path_pattern           = "${local.admin_prefix}/*"
    target_origin_id       = "s3-site"
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD", "OPTIONS"]
    cached_methods         = ["GET", "HEAD"]
    compress               = true

    #   CachingDisabled              4135ea2d-6df8-44a3-9df3-4b5a84be39ad
    cache_policy_id            = "4135ea2d-6df8-44a3-9df3-4b5a84be39ad"
    response_headers_policy_id = aws_cloudfront_response_headers_policy.admin.id
  }

  # The API (ADR-0159). Caching disabled — every response is per-request and
  # never cacheable at the edge — and every viewer header, cookie, and query
  # string is forwarded except Host, which would otherwise carry this
  # distribution's own hostname to the Function URL instead of the value it
  # expects.
  ordered_cache_behavior {
    path_pattern           = "/api/*"
    target_origin_id       = "api"
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD", "OPTIONS", "PUT", "POST", "PATCH", "DELETE"]
    cached_methods         = ["GET", "HEAD"]
    compress               = true

    #   CachingDisabled                    4135ea2d-6df8-44a3-9df3-4b5a84be39ad
    #   Managed-AllViewerExceptHostHeader  b689b0a8-53d0-40ab-baf2-68738e2966ac
    cache_policy_id          = "4135ea2d-6df8-44a3-9df3-4b5a84be39ad"
    origin_request_policy_id = "b689b0a8-53d0-40ab-baf2-68738e2966ac"
  }

  # SPA fallback (issue #465). The Vite build produces one index.html and its
  # asset bundle, not a per-route .html file and not a 404.html — so a deep
  # link (e.g. /report, /admin/queue) is a path CloudFront's default
  # behavior looks up on the site bucket and does not find. S3 answers 403,
  # not 404, for a missing key on a PRIVATE bucket (there is no object to
  # leak the existence of), so both status codes are mapped here to the same
  # place: index.html, served at 200 so the client-side router — not
  # CloudFront — decides what the path means. This replaces the
  # ADR-0009-era CloudFront Function that rewrote /x to /x.html: that scheme
  # matched a previous, non-Vite build and 404'd every deep link against this
  # one.
  custom_error_response {
    error_code            = 403
    response_code         = 200
    response_page_path    = "/index.html"
    error_caching_min_ttl = 60
  }

  custom_error_response {
    error_code            = 404
    response_code         = 200
    response_page_path    = "/index.html"
    error_caching_min_ttl = 60
  }

  restrictions {
    geo_restriction {
      # None. A Canadian pilot files from wherever they crashed, and geo
      # restriction is distribution-level — applying it to keep the review queue
      # in-country would also block a report from a pilot flying abroad.
      restriction_type = "none"
    }
  }

  viewer_certificate {
    # Staging (site_domains empty): CloudFront's own default certificate,
    # valid only for its *.cloudfront.net address. Production: the one
    # us-east-1 certificate covering both hostnames (acm.tf).
    cloudfront_default_certificate = length(var.site_domains) == 0
    acm_certificate_arn            = length(var.site_domains) > 0 ? aws_acm_certificate_validation.site[0].certificate_arn : null
    ssl_support_method             = length(var.site_domains) > 0 ? "sni-only" : null
    minimum_protocol_version       = "TLSv1.2_2021"
  }

  tags = { Name = "${local.name}-site" }
}
