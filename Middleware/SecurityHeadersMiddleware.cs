namespace SecureApi.Middleware;

/// <summary>
/// Aplica cabeceras defensivas a la respuesta final sin interferir con las
/// cabeceras funcionales emitidas por autenticación o rate limiting.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    internal const string ApiContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    internal const string BrowserContentSecurityPolicy = "frame-ancestors 'none'";

    internal const string PermissionsPolicy = "camera=(), geolocation=(), microphone=()";

    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var httpContext = (HttpContext)state;
            ApplyHeaders(httpContext);
            return Task.CompletedTask;
        }, context);

        await _next(context);
    }

    private static void ApplyHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Kestrel se configura por separado; estas eliminaciones cubren
        // cabeceras agregadas posteriormente dentro del pipeline.
        headers.Remove("Server");
        headers.Remove("X-Powered-By");

        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        headers["X-Frame-Options"] = "DENY";
        headers["Permissions-Policy"] = PermissionsPolicy;

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            headers["Content-Security-Policy"] = ApiContentSecurityPolicy;
            headers["Cache-Control"] = "no-store";
            return;
        }

        // Swagger necesita cargar sus recursos; una política default-src 'none'
        // rompería la interfaz, por lo que aquí solo se impide el framing.
        headers["Content-Security-Policy"] = BrowserContentSecurityPolicy;
    }
}
