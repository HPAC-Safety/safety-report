# Two CloudFront distributions: one for the public report form, one for the admin
# review queue.
#
# TWO, not one with two paths. Different audiences and different risk. The admin
# surface can take network controls — a WAF IP allowlist, geo restriction, a
# signed-cookie origin — without any of it ever affecting a pilot's ability to
# file a report. Sharing a distribution would make every admin hardening step a
# change to the public form's delivery path. See ADR-0009.

resource "aws_cloudfront_function" "clean_urls" {
  name    = "${local.name}-clean-urls"
  runtime = "cloudfront-js-2.0"
  comment = "Viewer-request URI rewrite so clean URLs are served at 200, not redirected."
  publish = true
  code    = file("${path.module}/functions/clean-urls.js")
}

resource "aws_cloudfront_origin_access_control" "site" {
  for_each = local.site_buckets

  name                              = "${local.name}-${each.key}"
  description                       = "Signs CloudFront's requests to the ${each.key} site bucket, which is otherwise private."
  origin_access_control_origin_type = "s3"
  signing_behavior                  = "always"
  signing_protocol                  = "sigv4"
}

locals {
  site_aliases = {
    public = local.has_public_domain ? [var.public_site_domain] : []
    admin  = local.has_admin_domain ? [var.admin_site_domain] : []
  }
}

resource "aws_cloudfront_distribution" "site" {
  for_each = local.site_buckets

  enabled             = true
  is_ipv6_enabled     = true
  comment             = "${local.name} ${each.key} site"
  default_root_object = "index.html"

  # North America and Europe. Reports are filed by Canadians about incidents
  # mostly in Canada; paying for edge locations in every region would buy
  # latency nobody experiences.
  price_class = "PriceClass_100"

  aliases = local.site_aliases[each.key]

  origin {
    origin_id                = "s3-${each.key}"
    domain_name              = aws_s3_bucket.site[each.key].bucket_regional_domain_name
    origin_access_control_id = aws_cloudfront_origin_access_control.site[each.key].id
  }

  default_cache_behavior {
    target_origin_id       = "s3-${each.key}"
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
      restriction_type = "none"
    }
  }

  viewer_certificate {
    # Without a domain, CloudFront serves on its own *.cloudfront.net name and
    # uses its own certificate. With one, the us-east-1 certificate from acm.tf.
    cloudfront_default_certificate = length(local.site_aliases[each.key]) == 0
    acm_certificate_arn            = length(local.site_aliases[each.key]) == 0 ? null : aws_acm_certificate_validation.site[0].certificate_arn
    ssl_support_method             = length(local.site_aliases[each.key]) == 0 ? null : "sni-only"
    minimum_protocol_version       = length(local.site_aliases[each.key]) == 0 ? "TLSv1" : "TLSv1.2_2021"
  }

  tags = { Name = "${local.name}-${each.key}" }
}
