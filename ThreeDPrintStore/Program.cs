using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using System.Threading.RateLimiting;
using ThreeDPrintStore.Models;
using ThreeDPrintStore.Services;
using Stripe;

if (args.Length == 1 && args[0] == "--generate-admin-password-hash")
{
    Environment.ExitCode = AdminPasswordHashCommand.Run();
    return;
}

var builder = WebApplication.CreateBuilder(args);

var persistentDataDirectory = builder.Configuration["Store:DataDirectory"];
if (string.IsNullOrWhiteSpace(persistentDataDirectory) &&
    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")) &&
    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HOME")))
    persistentDataDirectory = Path.Combine(Environment.GetEnvironmentVariable("HOME")!, "site", "data");
if (!string.IsNullOrWhiteSpace(persistentDataDirectory))
{
    persistentDataDirectory = Path.GetFullPath(persistentDataDirectory);
    Directory.CreateDirectory(persistentDataDirectory);
    var configuredConnection = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(configuredConnection))
    {
        configuredConnection = "Data Source=store.db";
    }
    var sqlite = new SqliteConnectionStringBuilder(configuredConnection);
    if (sqlite.DataSource != ":memory:" && !Path.IsPathRooted(sqlite.DataSource))
    {
        var databasePath = Path.GetFullPath(Path.Combine(persistentDataDirectory, sqlite.DataSource));
        var relativeDatabasePath = Path.GetRelativePath(persistentDataDirectory, databasePath);
        if (relativeDatabasePath == ".." ||
            relativeDatabasePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relativeDatabasePath))
            throw new InvalidOperationException("The relative SQLite database path must remain within Store:DataDirectory.");
        sqlite.DataSource = databasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(sqlite.DataSource)!);
    }
    builder.Configuration["ConnectionStrings:DefaultConnection"] = sqlite.ConnectionString;
    builder.Services.AddDataProtection()
        .SetApplicationName("Anaya3DPrintStore")
        .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(persistentDataDirectory, "DataProtection-Keys")));
}

StripeConfiguration.ApiKey = builder.Configuration["StripeSettings:SecretKey"];

// 1. Add MVC Controllers and Views
builder.Services.AddControllersWithViews();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Denied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = false;
        options.Events.OnValidatePrincipal = AdminCredentialValidator.ValidatePrincipalAsync;
    });
builder.Services.AddAuthorization(options =>
    options.AddPolicy("Admin", policy => policy.RequireAuthenticatedUser().RequireRole("Admin")));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("AdminLogin", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
// 2. Add Database Context
builder.Services.AddDbContext<ThreeDPrintStore.Data.ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddScoped<IEmailService, MailKitEmailService>();
builder.Services.AddScoped<IQuoteService, ThreeDPrintStore.Services.QuoteService>();
builder.Services.AddHostedService<ThreeDPrintStore.Services.OrderNotificationWorker>();

// 4. Configure Session memory for the Shopping Basket
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Basket expires after 30 mins of inactivity
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});


var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Store:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ThreeDPrintStore.Data.ApplicationDbContext>();
    await db.Database.MigrateAsync();
}


/** Seed the SQLite database with products automatically on boot
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<StoreDbContext>();
    //ThreeDPrintStore.Data.DbInitializer.Seed(context);
}**/


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    // Block legacy files before the static-file middleware can serve them.
    var path = context.Request.Path.Value ?? "";
    for (var i = 0; i < 3; i++)
    {
        path = Uri.UnescapeDataString(path).Replace('\\', '/');
        if (path.Equals("/uploads", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
    }
    await next();
});
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();

// Activate Session memory BEFORE Authorization
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Home}/{id?}");

app.Run();
