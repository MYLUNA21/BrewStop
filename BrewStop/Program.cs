using BrewStop.Data;
using BrewStop.Hubs; // Add once SignalR Hub is created
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedEmail = true;          // SRS-FR-01
    options.User.RequireUniqueEmail = true;

    options.Lockout.AllowedForNewUsers = true;            // SRS-FR-02
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>       // SRS-NFR-05
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = (ctx, _) =>
    {
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var ra))
            ctx.HttpContext.Response.Headers.RetryAfter = ((int)ra.TotalSeconds).ToString();

        ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("RateLimit")
            .LogWarning("Rate limit hit {Path} from {Ip}", ctx.HttpContext.Request.Path, ctx.HttpContext.Connection.RemoteIpAddress);
        return ValueTask.CompletedTask;
    };

    static bool IsOrder(HttpContext c) => HttpMethods.IsPost(c.Request.Method)
        && c.Request.Path.StartsWithSegments("/Checkout/Place", StringComparison.OrdinalIgnoreCase);

    static bool IsAuth(HttpContext c) => HttpMethods.IsPost(c.Request.Method)
        && (c.Request.Path.StartsWithSegments("/Account/Login", StringComparison.OrdinalIgnoreCase)
         || c.Request.Path.StartsWithSegments("/Account/Register", StringComparison.OrdinalIgnoreCase));

    static FixedWindowRateLimiterOptions Win(int n) => new()
    { PermitLimit = n, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 };

    o.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        PartitionedRateLimiter.Create<HttpContext, string>(c =>
            IsOrder(c) ? RateLimitPartition.GetFixedWindowLimiter("ord-ip:" + c.Connection.RemoteIpAddress, _ => Win(5))
          : IsAuth(c) ? RateLimitPartition.GetFixedWindowLimiter("auth-ip:" + c.Connection.RemoteIpAddress, _ => Win(10))
          : RateLimitPartition.GetNoLimiter("none")),
        PartitionedRateLimiter.Create<HttpContext, string>(c =>
            IsOrder(c) && c.User.Identity?.IsAuthenticated == true
                ? RateLimitPartition.GetFixedWindowLimiter("ord-user:" + c.User.FindFirstValue(ClaimTypes.NameIdentifier), _ => Win(5))
                : RateLimitPartition.GetNoLimiter("none")));
});

builder.Services.AddScoped<IEmailService, LoggingEmailService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IImageResolver, ImageResolver>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".BrewStop.Cart";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.IdleTimeout = TimeSpan.FromHours(2);
});

builder.Services.Configure<ShopOptions>(builder.Configuration.GetSection("Shop"));
builder.Services.Configure<QrOptions>(builder.Configuration.GetSection("Qr")); 
builder.Services.AddScoped<IQrTokenService, QrTokenService>();               
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IPickupSlotService, PickupSlotService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddMemoryCache();
builder.Services.Configure<CaptchaOptions>(builder.Configuration.GetSection("Captcha"));
builder.Services.AddScoped<ICaptchaService, CaptchaService>();


builder.Services.AddSignalR();

builder.Services.AddControllersWithViews(options =>          // SRS-NFR-06
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();


app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' data: https://images.unsplash.com; connect-src 'self' ws: wss:; " +
        "script-src 'self' 'unsafe-inline' https://cdn.tailwindcss.com https://cdnjs.cloudflare.com; " +
        "style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://fonts.googleapis.com; " +
        "font-src 'self' https://cdnjs.cloudflare.com https://fonts.gstatic.com; frame-ancestors 'none'";
    await next();
});

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseRateLimiter(); 
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


app.MapHub<OrderHub>("/hubs/orders");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

await DbInitializer.SeedAsync(app.Services);
app.Run();