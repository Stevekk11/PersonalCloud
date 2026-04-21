using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using PersonalCloud.Data;
using PersonalCloud.Helpers;
using PersonalCloud.Models;
using PersonalCloud.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// Register Syncfusion license
var syncfusionLicenseKey = builder.Configuration["SYNCFUSION_LICENSE_KEY"];
if (!string.IsNullOrEmpty(syncfusionLicenseKey))
{
    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionLicenseKey);
}

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                       throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? throw new InvalidOperationException("Google ClientId not found.");
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? throw new InvalidOperationException("Google ClientSecret not found.");
    });

// Ensure all cookies are sent with Secure flag (HTTPS only)
builder.Services.Configure<CookieAuthenticationOptions>(
    Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.Configure<CookieAuthenticationOptions>(
    Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme, options =>
    {
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.Secure = CookieSecurePolicy.Always;
    options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
});
builder.Services.AddControllersWithViews().AddViewLocalization().AddDataAnnotationsLocalization();
builder.Services.AddScoped<DocumentService>(provider =>
{
    var context = provider.GetRequiredService<ApplicationDbContext>();
    var configuration = provider.GetRequiredService<IConfiguration>();
    var storageRoot = configuration.GetValue<string>("Storage:Root") ?? "UserDocs";
    var logger = provider.GetRequiredService<ILogger<DocumentService>>();
    return new DocumentService(context, storageRoot, logger);
});
builder.Services.AddScoped<IPremiumCapacityService, PremiumCapacityService>();
builder.Services.AddSingleton<SensorService>();
builder.Services.AddHttpClient<ITurnstileService, TurnstileService>();
// Configure max upload / request limits (5 GB)
const long fiveGb = 5L * 1024 * 1024 * 1024;

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = fiveGb; // 5 GB
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

// Kestrel server limits for large uploads
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = fiveGb; // 5 GB
});

builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// Configure strict HSTS for production
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    // Clear known networks and proxies to allow headers from Nginx
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});


//build the app
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
}

//add localization
var supportedCultures = new[] { "en", "cs" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("cs")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

localizationOptions.RequestCultureProviders.Insert(0, new QueryStringRequestCultureProvider());
app.UseRequestLocalization(localizationOptions);

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseHsts();
app.UseHttpsRedirection();


// Security headers middleware (also sets HSTS explicitly to guarantee the header)
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCookiePolicy();
app.UseRouting();
app.UseSession();
app.UseAuthorization();
app.MapStaticAssets();

// Configure static files to serve .wasm files correctly
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".wasm"] = "application/wasm";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "UserDocs")),
    RequestPath = "/downloads",
    OnPrepareResponse = ctx =>
    {
        // Enforce security headers for all files in UserDocs
        ctx.Context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        
        // Prevent execution of dangerous files by forcing download
        var dangerousExtensions = new[] { ".cs", ".exe", ".dll", ".config", ".json", ".cshtml", ".js", ".html", ".htm", ".cmd", ".bat", ".vbs", ".ps1" };
        var extension = Path.GetExtension(ctx.File.PhysicalPath).ToLower();
        
        if (dangerousExtensions.Contains(extension))
        {
            ctx.Context.Response.ContentType = "application/octet-stream";
            ctx.Context.Response.Headers.Append("Content-Disposition", "attachment; filename=\"" + Path.GetFileName(ctx.File.PhysicalPath) + "\"");
        }
    }
});

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

app.MapControllerRoute(
    name: "mandelbrot",
    pattern: "Mandelbrot",
    defaults: new { controller = "Home", action = "Mandelbrot" });

app.Run();