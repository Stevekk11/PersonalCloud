using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

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
            // Content-Security-Policy
            // Allowing self, bootstrap icons cdn, and potentially needed inline styles/scripts for Razor views
            // For a production 'A', we should ideally avoid 'unsafe-inline', but ASP.NET Core Identity and some libraries often need it.
            // We'll start with a reasonably strict policy and adjust if needed.
            var csp = "default-src 'self'; " +
                      "script-src 'self' 'unsafe-inline' 'unsafe-eval'; https://cdn.jsdelivr.net;" + // Added unsafe-inline/eval for common library support, can be tightened later
                      "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
                      "img-src 'self' data:; " +
                      "font-src 'self' https://cdn.jsdelivr.net; " +
                      "connect-src 'self'; " +
                      "frame-ancestors 'none'; " +
                      "form-action 'self';";

            context.Response.Headers.Append("Content-Security-Policy", csp);

            // X-Content-Type-Options
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

            // X-Frame-Options
            context.Response.Headers.Append("X-Frame-Options", "DENY");

            // X-XSS-Protection
            context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

            // Referrer-Policy
            context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

            // Permissions-Policy
            context.Response.Headers.Append("Permissions-Policy", "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");

            await _next(context);
        }
    }
}

