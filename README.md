# BrewStop

Campus coffee shop ordering and pickup system built with ASP.NET Core MVC (.NET 10). Students and faculty can customize drinks or pastries and book a pickup slot ahead of time, while baristas handle live orders at the counter and admins manage stock, promos, and reports.

## What It Does

**For Students & Faculty**
- Browse coffee and pastry menus with dynamic pricing for sizes (Small, Medium, Large) and add-ons.
- Pick a scheduled pickup window based on shop hours and prep lead time.
- Receive a single-use HMAC-SHA256 signed QR code per order for quick counter pickup.
- Earn and redeem loyalty points, bookmark favorite drinks, and leave reviews on past orders.
- Sign in with email verification or Google OAuth, protected by account lockouts, rate limiting, and a custom coffee-tile CAPTCHA.

**For Baristas & Admins**
- Live order queue powered by SignalR (`OrderHub`) so status changes show up immediately without page reloads.
- Counter QR scanner (`/api/barista/scan`) to validate and complete order pickups, plus barista shift clock-in/out tracking.
- Inventory tracking with automatic stock deduction on checkout, promo management, and activity audit logs.

## Tech Stack

- **Backend:** ASP.NET Core MVC & Web API (.NET 10)
- **Database:** MySQL 8.0+, Entity Framework Core 10 (`MySqlConnector` / `Microling.EntityFrameworkCore.MySql`)
- **Auth & Real-Time:** ASP.NET Core Identity, Google OAuth 2.0, SignalR
- **Frontend:** Razor Views, Tailwind CSS, Bootstrap 5, Font Awesome

## Local Setup

### Requirements
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022 or 2026 (ASP.NET and web development workload)
- MySQL 8.0+ (MySQL Server, XAMPP, or Laragon)

### 1. Clone the repo
```bash
git clone [https://github.com/MYLUNA21/BrewStop.git](https://github.com/MYLUNA21/BrewStop.git)
cd BrewStop
