# ONE CloudFront distribution, serving one website.
#
# ADR-0009 specified two distributions — one public, one admin — "so the admin
# surface can take network controls the public form must not have". That is
# superseded: the admin review queue is a ROUTE on the website, at
# https://safety.hpac.ca/admin/. ADR-0031 records the decision, what was given
# up, and the assessment of whether it is safe.
#
# The short version, because it belongs next to the code: WAF, geo restriction,
# and IP allowlisting are DISTRIBUTION-level in CloudFront, so collapsing two
# distributions into one means those can no longer be applied to the admin area
# alone. What can still be applied per path is a cache behavior, a function, and
# a response headers policy — all three are used below. That is an acceptable
# trade only because the admin bundle is static HTML and JavaScript containing no
# report data: every byte a reviewer sees comes from the API, which authorizes
# each request (#24). The delivery path was never the security boundary.

resource "aws_cloudfront_function" "clean_urls" {
  name    = "${local.name}-clean-urls"
  runtime = "cloudfront-js-2.0"
  comment = "Viewer-request URI rewrite so clean URLs are served at 200, not redirected."
  publish = true

  code = templatefile("${path.module}/functions/clean-urls.js.tftpl", {
    admin_prefix = local.admin_prefix
  })
}

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
  comment             = "${local.name} website — public form at /, review queue at ${local.admin_prefix}/"
  default_root_object = "index.html"

  # North America and Europe. Reports are filed by Canadians about incidents
  # mostly in Canada; paying for edge locations in every region would buy
  # latency nobody experiences.
  price_class = "PriceClass_100"

  aliases = [var.site_domain]

  origin {
    origin_id                = "s3-site"
    domain_name              = aws_s3_bucket.site.bucket_regional_domain_name
    origin_access_control_id = aws_cloudfront_origin_access_control.site.id
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

    function_association {
      event_type   = "viewer-request"
      function_arn = aws_cloudfront_function.clean_urls.arn
    }
  }

  # The review queue. Same origin, different rules — this is the per-path
  # isolation that survives collapsing the two distributions into one.
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

    function_association {
      event_type   = "viewer-request"
      function_arn = aws_cloudfront_function.clean_urls.arn
    }
  }

  # S3 answers 403 rather than 404 for a missing key on a private bucket, so both
  # map to the same page. Served at the right status code: a 404 that returns 200
  # gets indexed.
  custom_error_response {
    error_code            = 403
    response_code         = 404
    response_page_path    = "/404.html"
    error_caching_min_ttl = 60
  }

  custom_error_response {
    error_code            = 404
    response_code         = 404
    response_page_path    = "/404.html"
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
    acm_certificate_arn      = aws_acm_certificate_validation.site.certificate_arn
    ssl_support_method       = "sni-only"
    minimum_protocol_version = "TLSv1.2_2021"
  }

  tags = { Name = "${local.name}-site" }
}
