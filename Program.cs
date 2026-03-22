using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using PersonalCloud.Data;
using PersonalCloud.Helpers;
using PersonalCloud.Models;
using PersonalCloud.Services;
using Serilog;
using Microsoft.AspNetCore.Identity;

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

// Ensure all cookies are sent with Secure flag (HTTPS only)
builder.Services.Configure<CookieAuthenticationOptions>(
    Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.Secure = CookieSecurePolicy.Always;
    options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
    options.MinimumSameSitePolicy = SameSiteMode.Strict;
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
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// Configure strict HSTS for production


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
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

// HSTS and HTTPS redirect run first so the header is set before anything else
app.UseHsts();
app.UseHttpsRedirection();

// Security headers middleware (also sets HSTS explicitly to guarantee the header)
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCookiePolicy();
app.UseRouting();
app.UseSession();
app.UseAuthorization();
app.MapStaticAssets();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "UserDocs")),
    RequestPath = "/downloads",
    OnPrepareResponse = ctx =>
    {
        // Prevent execution of dangerous files
        var extensions = new[] { ".cs", ".exe", ".dll", ".config", ".json" };
        if (extensions.Contains(Path.GetExtension(ctx.File.PhysicalPath).ToLower()))
        {
            ctx.Context.Response.ContentType = "application/octet-stream";
        }
    }
});

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

app.Run();