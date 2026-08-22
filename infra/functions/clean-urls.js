// CloudFront Function, viewer-request event.
//
// The static sites need clean URLs served at 200 — /report, not /report.html and
// not a 301 to it. A viewer-request function rewrites the URI before CloudFront
// looks in the origin, so there is no round trip and no redirect. This is the
// mechanism ADR-0009 names; deploy-web.yml only ships content and never touches
// it.
//
// CloudFront Functions run a constrained ES5-era JavaScript runtime: no
// `const`, no arrow functions, no template literals, no regular expression
// lookbehind, and a 1 ms budget. Keep it boring.

function handler(event) {
    var request = event.request;
    var uri = request.uri;

    // "/" and "/admin/" -> the directory index. charAt rather than endsWith:
    // the CloudFront Functions runtime is not a browser and its ES surface is
    // documented rather than assumed.
    if (uri.charAt(uri.length - 1) === '/') {
        request.uri = uri + 'index.html';
        return request;
    }

    // Anything with a file extension is a real asset: stylesheet, script,
    // locale JSON, image. Leave it alone.
    var lastSegment = uri.substring(uri.lastIndexOf('/') + 1);
    if (lastSegment.indexOf('.') !== -1) {
        return request;
    }

    // "/report" -> "/report.html".
    request.uri = uri + '.html';
    return request;
}
