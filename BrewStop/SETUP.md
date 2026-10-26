# BrewStop: Setup (Tasks 1 & 2)

## 1. Packages

```bash
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Pomelo.EntityFrameworkCore.MySql   # use the latest release that targets EF Core 10
```

SignalR is built into the ASP.NET Core shared framework (no package needed on the server).

## 2. appsettings.json

```json
{
  "ConnectionStrings": {
    "BrewStop": "Server=localhost;Port=3306;Database=brewstop;User=brewstop_app;Password=CHANGE_ME;"
  }
}
```

Keep real passwords in User Secrets or environment variables, not in source control.

## 3. Program.cs wiring

```csharp
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
        new MySqlServerVersion(new Version(8, 4, 0)),   // match your server version
        mySql => mySql.EnableRetryOnFailure()));

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddScoped<IOrderNotifier, OrderNotifier>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
// app.UseAuthentication();  // add when Identity is introduced
app.UseAuthorization();

app.MapHub<OrderHub>("/hubs/orders");
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

## 4. Create the database

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## 5. Client usage (preview for the frontend task)

```js
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/orders")
  .withAutomaticReconnect()
  .build();

connection.on("OrderStatusChanged", update => { /* animate status tile */ });

await connection.start();
await connection.invoke("TrackOrder", "BS-261020-K7M2");
```
