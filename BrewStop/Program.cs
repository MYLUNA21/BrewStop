using BrewStop.Data;
using BrewStop.Hubs;
using BrewStop.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("BrewStop")
    ?? throw new InvalidOperationException("Connection string 'BrewStop' not found.");
builder.Services.AddDbContext<BrewStopDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 36)), // or your specific MySQL version
        mySql => mySql.EnableRetryOnFailure()));


builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddScoped<IOrderNotifier, OrderNotifier>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapHub<OrderHub>("/hubs/orders");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();