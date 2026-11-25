namespace SecretSanta.Services;

public static class SecurityHeaders
{
    // Everything comes from this site: scripts, styles, the service worker and fetches; images may also be data: URLs.
    // Inline scripts and styles are not allowed. The pages have none; scripts set styles through the DOM, which CSP permits.
    public const string ContentSecurityPolicy =
        "default-src 'self'; img-src 'self' data:; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    public static void Apply(HttpResponse response)
    {
        response.Headers.ContentSecurityPolicy = ContentSecurityPolicy;
        response.Headers.XContentTypeOptions = "nosniff";
    }
}
