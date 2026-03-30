namespace PersonalCloud.Helpers
{
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Strict-Transport-Security (HSTS) — set explicitly so it is always present,
            // regardless of environment or whether UseHsts() fires for this request.
            context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains; preload");

            // Content-Security-Policy
            var csp = "default-src 'self' https://challenges.cloudflare.com https://pid.cz https://data.pid.cz;" +
                      "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://challenges.cloudflare.com;" +
                      "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net;" +
                      "img-src 'self' data:;" +
                      "font-src 'self' https://cdn.jsdelivr.net;" +
                      "connect-src 'self' https://challenges.cloudflare.com;" +
                      "frame-src 'self' https://challenges.cloudflare.com https://pid.cz https://data.pid.cz;" +
                      "object-src 'none';" +
                      "frame-ancestors 'self';" +
                      "form-action 'self' https://localhost:7135 https://veghtp.dev.spsejecna.net https://accounts.google.com;";

            context.Response.Headers.Append("Content-Security-Policy", csp);

            // X-Content-Type-Options
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

            // X-Frame-Options
            //context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");

            // X-XSS-Protection
            context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

            // Referrer-Policy
            context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

            // Permissions-Policy
            context.Response.Headers.Append("Permissions-Policy", "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");

            // Cross-Origin-Resource-Policy
            context.Response.Headers.Append("Cross-Origin-Resource-Policy", "same-origin");

            await _next(context);
        }
    }
}
