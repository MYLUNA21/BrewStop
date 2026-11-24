using BrewStop.Data;
using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BrewStop.Controllers;

public class AccountController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IEmailService email,
    ICaptchaService captcha,
    IAuditService audit,
    IConfiguration config,
    ApplicationDbContext db,
    IWebHostEnvironment env,
    IOptions<IdentityOptions> identityOptions,
    IMemoryCache cache,
    ILogger<AccountController> log) : Controller
{
    // Register
    [HttpGet, AllowAnonymous]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Register(RegisterViewModel m)
    {
        if (!ModelState.IsValid) return View(m);

        if (!await captcha.VerifyAsync(Request, "register", HttpContext.RequestAborted))
        {
            ModelState.AddModelError(string.Empty, "Captcha verification failed. Please try again.");
            return View(m);
        }

        var userName = m.UserName.Trim();
        var emailAddress = string.IsNullOrWhiteSpace(m.Email) ? null : m.Email.Trim();

        var domain = config["Campus:AllowedEmailDomain"];
        if (emailAddress is not null && !string.IsNullOrWhiteSpace(domain) &&
            !emailAddress.EndsWith("@" + domain, StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(m.Email), $"Use your campus email (@{domain}).");

        if (m.Role is not (AppRoles.Student or AppRoles.Faculty))
            ModelState.AddModelError(nameof(m.Role), "Invalid role.");

        if (await users.FindByNameAsync(userName) is not null)
            ModelState.AddModelError(nameof(m.UserName), "Username is already taken.");

        if (emailAddress is not null && await users.FindByEmailAsync(emailAddress) is not null)
            ModelState.AddModelError(nameof(m.Email), "Email is already registered.");

        if (!ModelState.IsValid) return View(m);

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = emailAddress,
            EmailConfirmed = true,
            FullName = m.FullName.Trim(),
            CampusId = await NewCampusIdAsync()
        };

        var result = await users.CreateAsync(user, m.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(m);
        }

        await users.AddToRoleAsync(user, m.Role);
        await audit.LogAsync(user.Id, "RoleAssigned", $"Role={m.Role}; Source=Registration");
        log.LogInformation("User registered {UserId} as {Role}", user.Id, m.Role);

        await signIn.SignInAsync(user, isPersistent: false);
        TempData["Success"] = "Welcome to BrewStop! Your account is ready.";
        return LocalRedirect("/");
    }

    private async Task<string> NewCampusIdAsync()
    {
        for (var i = 0; i < 10; i++)
        {
            var id = $"BS-{Random.Shared.Next(100000, 999999)}";
            if (!await users.Users.AnyAsync(u => u.CampusId == id)) return id;
        }
        return $"BS-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? token)
    {
        var user = userId is null || token is null ? null : await users.FindByIdAsync(userId);
        var ok = user is not null && (await users.ConfirmEmailAsync(user, token!)).Succeeded;

        return View("Info", ok
            ? new InfoViewModel("Email verified", "Your account is active. You can sign in now.")
            : new InfoViewModel("Invalid link", "This verification link is invalid or expired."));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    // Login / Logout
    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel m)
    {
        if (!ModelState.IsValid) return View(m);

        if (!await captcha.VerifyAsync(Request, "login", HttpContext.RequestAborted))
        {
            ModelState.AddModelError(string.Empty, "Please complete the captcha.");
            return View(m);
        }

        var login = m.UserNameOrEmail.Trim();
        var existing = login.Contains('@')
            ? await users.FindByEmailAsync(login) ?? await users.FindByNameAsync(login)
            : await users.FindByNameAsync(login);

        // Unknown or soft-deleted accounts can never sign in
        if (existing is null or { IsDeleted: true })
        {
            ModelState.AddModelError("", "Invalid username/email or password.");
            return View(m);
        }

        var result = await signIn.PasswordSignInAsync(existing, m.Password,
            isPersistent: false, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            log.LogInformation("Login success for {UserName}", existing.UserName);
            return LocalRedirect(Url.IsLocalUrl(m.ReturnUrl) ? m.ReturnUrl! : "/");
        }

        if (result.IsLockedOut)
        {
            log.LogWarning("Account locked out: {UserName}", existing.UserName);
            ModelState.AddModelError("", "Account locked for 15 minutes after 5 failed attempts.");
        }
        else if (result.IsNotAllowed)
        {
            ModelState.AddModelError("", "This account is not allowed to sign in.");
        }
        else
        {
            log.LogWarning("Failed login for {UserName}", existing.UserName);
            ModelState.AddModelError("", "Invalid username/email or password.");
        }

        return View(m);
    }

    // Google OAuth 2.0 External Login & CampusId Onboarding
    private static bool NeedsCampusId(ApplicationUser u) =>
        string.IsNullOrWhiteSpace(u.CampusId) ||
        u.CampusId.StartsWith("GOOGLE-", StringComparison.OrdinalIgnoreCase);

    private ContentResult RedirectAfterExternal(ApplicationUser user, string? returnUrl, string target) =>
        ClientRedirect(NeedsCampusId(user)
            ? Url.Action(nameof(CompleteCampusId), "Account",
                new { returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null })!
            : target);

    [HttpPost, AllowAnonymous]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        if (!string.Equals(provider, GoogleDefaults.AuthenticationScheme, StringComparison.OrdinalIgnoreCase))
            return BadRequest();

        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account",
            new { returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
        var props = signIn.ConfigureExternalAuthenticationProperties(GoogleDefaults.AuthenticationScheme, redirectUrl);
        return Challenge(props, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        var target = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/Menu";

        IActionResult Fail(string message)
        {
            ModelState.AddModelError(string.Empty, message);
            return View("Login", new LoginViewModel { ReturnUrl = returnUrl });
        }

        if (!string.IsNullOrEmpty(remoteError))
        {
            log.LogWarning("Google remote error: {Error}", remoteError);
            return Fail("Google sign-in failed. Please try again or use your campus email.");
        }

        var info = await signIn.GetExternalLoginInfoAsync();
        if (info is null)
            return Fail("Google sign-in session expired. Please try again.");

        // 1) Already linked
        var result = await signIn.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

        if (result.Succeeded)
        {
            var linked = await users.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (linked is null)
                return ClientRedirect(target);

            await audit.LogAsync(linked.Id, "ExternalLogin", $"Provider={info.LoginProvider}");
            return RedirectAfterExternal(linked, returnUrl, target);
        }

        if (result.IsLockedOut)
            return Fail("Account locked for 15 minutes after 5 failed attempts.");

        // 2) Not linked yet
        var emailAddr = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(emailAddr))
            return Fail("Google did not return an email address.");

        if (string.Equals(info.Principal.FindFirstValue("urn:google:verified_email"), "false",
                StringComparison.OrdinalIgnoreCase))
            return Fail("Your Google email address is not verified.");

        var user = await users.FindByEmailAsync(emailAddr);

        if (user is null)
        {
            var name = info.Principal.FindFirstValue(ClaimTypes.Name);
            if (string.IsNullOrWhiteSpace(name)) name = emailAddr.Split('@')[0];
            if (name.Length > 150) name = name[..150];

            user = new ApplicationUser
            {
                UserName = emailAddr,
                Email = emailAddr,
                EmailConfirmed = true,
                FullName = name,
                CampusId = "GOOGLE-" + Guid.NewGuid().ToString("N")[..8].ToUpper()
            };

            var create = await users.CreateAsync(user);
            if (!create.Succeeded)
            {
                log.LogWarning("Google user creation failed for {Email}: {Errors}",
                    emailAddr, string.Join("; ", create.Errors.Select(e => e.Description)));
                return Fail("Could not create your account. Please contact support.");
            }

            await users.AddToRoleAsync(user, AppRoles.Student);
            await audit.LogAsync(user.Id, "RoleAssigned", $"Role={AppRoles.Student}; Source=GoogleOAuth");
            await audit.LogAsync(user.Id, "UserRegistered", $"Provider={info.LoginProvider}; Source=GoogleOAuth");
        }
        else
        {
            if (await users.IsLockedOutAsync(user))
                return Fail("Account locked for 15 minutes after 5 failed attempts.");

            var dirty = false;
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                dirty = true;
            }
            if (string.IsNullOrWhiteSpace(user.CampusId))
            {
                user.CampusId = "GOOGLE-" + Guid.NewGuid().ToString("N")[..8].ToUpper();
                dirty = true;
            }
            if (dirty) await users.UpdateAsync(user);
        }

        var existingLogins = await users.GetLoginsAsync(user);
        if (!existingLogins.Any(l => l.LoginProvider == info.LoginProvider && l.ProviderKey == info.ProviderKey))
        {
            var add = await users.AddLoginAsync(user, info);
            if (!add.Succeeded)
            {
                log.LogWarning("AddLogin failed for {Email}: {Errors}",
                    emailAddr, string.Join("; ", add.Errors.Select(e => e.Description)));
                return Fail("Could not link your Google account.");
            }
            await audit.LogAsync(user.Id, "ExternalLoginLinked", $"Provider={info.LoginProvider}");
        }

        await signIn.SignInAsync(user, isPersistent: false, info.LoginProvider);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        await audit.LogAsync(user.Id, "ExternalLogin", $"Provider={info.LoginProvider}");

        return RedirectAfterExternal(user, returnUrl, target);
    }

    [HttpGet, Authorize]
    public async Task<IActionResult> CompleteCampusId(string? returnUrl = null)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return RedirectToAction(nameof(Login));

        if (!NeedsCampusId(user))
            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/Menu");

        return View(new CompleteCampusIdViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteCampusId(CompleteCampusIdViewModel m)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return RedirectToAction(nameof(Login));

        var target = Url.IsLocalUrl(m.ReturnUrl) ? m.ReturnUrl! : "/Menu";

        if (!NeedsCampusId(user))
            return LocalRedirect(target);

        var campusId = (m.CampusId ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(campusId))
            ModelState.AddModelError(nameof(m.CampusId), "Campus ID is required.");
        else if (campusId.StartsWith("GOOGLE-", StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(m.CampusId), "Please enter your real Campus ID.");
        else if (campusId.Length > 30)
            ModelState.AddModelError(nameof(m.CampusId), "Campus ID must be at most 30 characters.");
        else if (await users.Users.AnyAsync(u => u.Id != user.Id && u.CampusId == campusId,
                     HttpContext.RequestAborted))
            ModelState.AddModelError(nameof(m.CampusId), "This Campus ID is already registered to another account.");

        if (!ModelState.IsValid) return View(m);

        user.CampusId = campusId;
        var update = await users.UpdateAsync(user);
        if (!update.Succeeded)
        {
            foreach (var e in update.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(m);
        }

        await audit.LogAsync(user.Id, "GoogleOAuthRegistered", $"CampusId={campusId}; Source=GoogleOAuth");
        log.LogInformation("Campus ID completed for {UserId}", user.Id);

        return LocalRedirect(target);
    }

    // Breaks the cross-site redirect chain so the SameSite=Strict app cookie is sent on the next request
    private ContentResult ClientRedirect(string url)
    {
        Response.Headers.CacheControl = "no-store";
        var safe = WebUtility.HtmlEncode(url);
        return new ContentResult
        {
            ContentType = "text/html; charset=utf-8",
            Content = $"<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta http-equiv=\"refresh\" content=\"0;url={safe}\"><title>Signing in...</title></head><body style=\"background:#1c1917;color:#a8a29e;font-family:sans-serif;text-align:center;padding-top:20vh\">Signing you in...<noscript> <a href=\"{safe}\">Continue</a></noscript></body></html>"
        };
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet, AllowAnonymous]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    private const int MaxResetAttempts = 5;
    private static readonly TimeSpan ResetCodeLifetime = TimeSpan.FromMinutes(10);

    private sealed class ResetCodeEntry
    {
        public string Code { get; init; } = "";
        public int Attempts { get; set; }
    }

    private string ResetKey(string emailAddress) => "pwreset:" + (users.NormalizeEmail(emailAddress.Trim()) ?? "");

    // Password reset - step 1: email a 6-digit code
    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel m)
    {
        if (!ModelState.IsValid) return View(m);

        var emailAddress = m.Email.Trim();
        var user = await users.FindByEmailAsync(emailAddress);

        if (user is null || user.IsDeleted || string.IsNullOrWhiteSpace(user.Email))
        {
            log.LogWarning("Password reset requested for unknown email {Email}", emailAddress);
        }
        else
        {
            var code = Random.Shared.Next(100000, 999999).ToString();
            cache.Set(ResetKey(emailAddress), new ResetCodeEntry { Code = code }, ResetCodeLifetime);

            await email.SendAsync(user.Email, "Your BrewStop password reset code",
                $"<p>Your BrewStop password reset code is: <b>{code}</b></p>" +
                "<p>It expires in 10 minutes. If you didn't request this, you can ignore this email.</p>");
        }

        // Same response whether or not the account exists
        return RedirectToAction(nameof(ResetPassword), new { email = emailAddress });
    }

    // Password reset - step 2: verify code + set new password
    [HttpGet, AllowAnonymous]
    public IActionResult ResetPassword(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return RedirectToAction(nameof(ForgotPassword));
        return View(new ResetPasswordViewModel { Email = email });
    }

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel m)
    {
        if (!ModelState.IsValid) return View(m);

        var key = ResetKey(m.Email);
        if (!cache.TryGetValue(key, out ResetCodeEntry? entry) || entry is null)
        {
            ModelState.AddModelError(nameof(m.Code), "This code has expired. Request a new one.");
            return View(m);
        }

        bool ok;
        int attempts;
        lock (entry)
        {
            ok = entry.Attempts < MaxResetAttempts &&
                 CryptographicOperations.FixedTimeEquals(
                     Encoding.UTF8.GetBytes(entry.Code), Encoding.UTF8.GetBytes(m.Code.Trim()));
            if (!ok) entry.Attempts++;
            attempts = entry.Attempts;
        }

        if (!ok)
        {
            if (attempts >= MaxResetAttempts)
            {
                cache.Remove(key);
                ModelState.AddModelError(nameof(m.Code), "Too many incorrect attempts. Request a new code.");
            }
            else
                ModelState.AddModelError(nameof(m.Code), $"Incorrect code. {MaxResetAttempts - attempts} attempt(s) left.");
            return View(m);
        }

        var user = await users.FindByEmailAsync(m.Email.Trim());
        if (user is null || user.IsDeleted)
        {
            cache.Remove(key);
            ModelState.AddModelError(nameof(m.Code), "This code has expired. Request a new one.");
            return View(m);
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, m.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(m);
        }

        cache.Remove(key);
        TempData["Success"] = "Password updated. You can now sign in.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    // Helpers
    private async Task SendConfirmationAsync(ApplicationUser user)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var link = Url.Action(nameof(ConfirmEmail), "Account",
            new { userId = user.Id, token }, Request.Scheme)!;

        await email.SendAsync(user.Email!, "Confirm your BrewStop account",
            $"<p>Hi {WebUtility.HtmlEncode(user.FullName)}, <a href=\"{link}\">confirm your email</a>.</p>");
    }

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> ResendConfirmation(string emailAddress)
    {
        var user = await users.FindByEmailAsync(emailAddress);
        if (user is not null && !await users.IsEmailConfirmedAsync(user))
            await SendConfirmationAsync(user);

        return View("Info", new InfoViewModel("Check your email",
            "If that account needs verification, a new link has been sent."));
    }

    // ===================== Account Settings Hub =====================
    private const long MaxAvatarBytes = 2 * 1024 * 1024;
    private static readonly string[] AvatarExts = [".jpg", ".png", ".webp"];
    private string AvatarDir => Path.Combine(env.WebRootPath, "uploads", "avatars");

    // Accepts 09XXXXXXXXX, 639XXXXXXXXX or +639XXXXXXXXX and stores +639XXXXXXXXX. Empty is allowed.
    private static bool TryNormalizePhone(string? raw, out string? phone)
    {
        phone = null;
        var s = new string((raw ?? string.Empty).Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (s.Length == 0) return true;

        string national;
        if (s.StartsWith("+63")) national = s[3..];
        else if (s.StartsWith("63")) national = s[2..];
        else if (s.StartsWith("09")) national = s[1..];
        else return false;

        if (!Regex.IsMatch(national, @"^9\d{9}$")) return false;
        phone = "+63" + national;
        return true;
    }

    // Checks the real file signature (magic bytes), not just the file name.
    private static string? DetectImageExt(byte[] h, int n)
    {
        if (n >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ".jpg";
        if (n >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47
                   && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A) return ".png";
        if (n >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F'
                    && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P') return ".webp";
        return null;
    }

    private void DeleteAvatarFile(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("/uploads/avatars/")) return;
        var path = Path.Combine(AvatarDir, Path.GetFileName(url));
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
    }

    private async Task<ApplicationUser?> CurrentUserAsync()
    {
        var u = await users.GetUserAsync(User);
        return u is { IsDeleted: false } ? u : null;
    }

    private async Task<IActionResult> ForceLogoutAsync()
    {
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    // ---- Personal Information ----
    [HttpGet, Authorize]
    public async Task<IActionResult> Manage()
    {
        var user = await CurrentUserAsync();
        if (user is null) return await ForceLogoutAsync();

        return View(new ManageProfileViewModel
        {
            Email = user.Email ?? string.Empty,
            AvatarUrl = user.AvatarUrl,
            FullName = user.FullName,
            CampusId = user.CampusId,
            PhoneNumber = user.PhoneNumber
        });
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Manage(ManageProfileViewModel m)
    {
        var user = await CurrentUserAsync();
        if (user is null) return await ForceLogoutAsync();

        m.Email = user.Email ?? string.Empty;
        m.AvatarUrl = user.AvatarUrl;

        var campusId = m.CampusId?.Trim() ?? string.Empty;
        if (campusId.Length > 0 &&
            await users.Users.AnyAsync(u => u.Id != user.Id && u.CampusId == campusId))
            ModelState.AddModelError(nameof(m.CampusId), "Campus ID is already registered.");

        if (!TryNormalizePhone(m.PhoneNumber, out var phone))
            ModelState.AddModelError(nameof(m.PhoneNumber), "Enter a PH mobile number like 09123456789 or +639123456789.");

        string? newExt = null;
        if (m.Avatar is { Length: > 0 })
        {
            var ext = Path.GetExtension(m.Avatar.FileName).ToLowerInvariant();
            if (m.Avatar.Length > MaxAvatarBytes)
                ModelState.AddModelError(nameof(m.Avatar), "Photo must be 2 MB or smaller.");
            else if (!AvatarExts.Contains(ext))
                ModelState.AddModelError(nameof(m.Avatar), "Only .jpg, .png or .webp photos are allowed.");
            else
            {
                var head = new byte[12];
                int read;
                await using (var s = m.Avatar.OpenReadStream())
                    read = await s.ReadAsync(head.AsMemory(0, 12));

                newExt = DetectImageExt(head, read);
                if (newExt is null || newExt != ext)
                    ModelState.AddModelError(nameof(m.Avatar), "That file is not a valid image.");
            }
        }

        if (!ModelState.IsValid) return View(m);

        if (newExt is not null)
        {
            Directory.CreateDirectory(AvatarDir);
            var fileName = $"{Guid.NewGuid():N}{newExt}";
            await using (var fs = System.IO.File.Create(Path.Combine(AvatarDir, fileName)))
                await m.Avatar!.CopyToAsync(fs);

            DeleteAvatarFile(user.AvatarUrl);
            user.AvatarUrl = $"/uploads/avatars/{fileName}";
        }

        user.FullName = m.FullName.Trim();
        user.CampusId = campusId;
        user.PhoneNumber = phone;

        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            m.AvatarUrl = user.AvatarUrl;
            return View(m);
        }

        await signIn.RefreshSignInAsync(user);
        await audit.LogAsync(user.Id, "ProfileUpdated", "Profile details updated");
        TempData["Success"] = "Your profile has been updated.";
        return RedirectToAction(nameof(Manage));
    }

    // ---- Change / Set Password ----
    [HttpGet, Authorize]
    public async Task<IActionResult> ChangePassword()
    {
        var user = await CurrentUserAsync();
        if (user is null) return await ForceLogoutAsync();

        return View(new ChangePasswordViewModel { HasPassword = await users.HasPasswordAsync(user) });
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel m)
    {
        var user = await CurrentUserAsync();
        if (user is null) return await ForceLogoutAsync();

        var has = await users.HasPasswordAsync(user);
        m.HasPassword = has;

        if (has && string.IsNullOrWhiteSpace(m.CurrentPassword))
            ModelState.AddModelError(nameof(m.CurrentPassword), "Enter your current password.");

        if (!ModelState.IsValid) return View(m);

        IdentityResult result = has
            ? await users.ChangePasswordAsync(user, m.CurrentPassword!, m.NewPassword)
            : await users.AddPasswordAsync(user, m.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(m);
        }

        // New security stamp signs out other devices; refresh keeps this session alive.
        await signIn.RefreshSignInAsync(user);
        await audit.LogAsync(user.Id, has ? "PasswordChanged" : "PasswordSet");

        TempData["Success"] = has ? "Your password has been changed." : "Your password has been set.";
        return RedirectToAction(nameof(ChangePassword));
    }

    // ---- Security ----
    [HttpGet, Authorize]
    public async Task<IActionResult> Security()
    {
        var user = await CurrentUserAsync();
        if (user is null) return await ForceLogoutAsync();

        var lockout = identityOptions.Value.Lockout;
        var logins = await users.GetLoginsAsync(user);

        return View(new SecurityViewModel
        {
            Email = user.Email ?? string.Empty,
            EmailConfirmed = await users.IsEmailConfirmedAsync(user),
            HasPassword = await users.HasPasswordAsync(user),
            LockoutEnabled = await users.GetLockoutEnabledAsync(user),
            IsLockedOut = await users.IsLockedOutAsync(user),
            MaxFailedAttempts = lockout.MaxFailedAccessAttempts,
            LockoutMinutes = (int)lockout.DefaultLockoutTimeSpan.TotalMinutes,
            GoogleLinked = logins.Any(l => l.LoginProvider == "Google")
        });
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> SignOutAllDevices()
    {
        var user = await CurrentUserAsync();
        if (user is null) return await ForceLogoutAsync();

        await users.UpdateSecurityStampAsync(user);
        await audit.LogAsync(user.Id, "SignedOutAllDevices");
        await signIn.SignOutAsync();

        return RedirectToAction(nameof(Login));
    }

    // ---- Notifications ----
    [HttpGet, Authorize]
    public async Task<IActionResult> Notifications()
    {
        var user = await CurrentUserAsync();
        if (user is null) return await ForceLogoutAsync();

        return View(new NotificationsViewModel
        {
            NotifyOrderReady = user.NotifyOrderReady,
            NotifyPromotions = user.NotifyPromotions
        });
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Notifications(NotificationsViewModel m)
    {
        var user = await CurrentUserAsync();
        if (user is null) return await ForceLogoutAsync();

        user.NotifyOrderReady = m.NotifyOrderReady;
        user.NotifyPromotions = m.NotifyPromotions;

        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(m);
        }

        await audit.LogAsync(user.Id, "NotificationsUpdated");
        TempData["Success"] = "Notification settings saved.";
        return RedirectToAction(nameof(Notifications));
    }

    // ---- Activity ----
    private static readonly string[] CustomerVisibleActions =
    [
        "OrderPlaced", "OrderCancelled", "PromoApplied", "ReviewCreated",
        "ProfileUpdated", "PasswordChanged", "PasswordSet", "SignedOutAllDevices",
        "NotificationsUpdated", "ExternalLogin", "ExternalLoginLinked",
        "UserRegistered", "GoogleOAuthRegistered", "AccountDeleted"
    ];

    [HttpGet, Authorize]
    public async Task<IActionResult> Activity()
    {
        var user = await CurrentUserAsync();
        if (user is null) return await ForceLogoutAsync();

        var entries = await db.AuditLogs
            .AsNoTracking()
            .Where(a => a.UserId == user.Id && CustomerVisibleActions.Contains(a.Action))
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(50)
            .ToListAsync();

        return View(new ActivityViewModel { Entries = entries });
    }

    // ---- Delete Account (soft delete + PII anonymization) ----
    [HttpGet, Authorize]
    public async Task<IActionResult> DeleteAccount()
    {
        var user = await CurrentUserAsync();
        if (user is null) return await ForceLogoutAsync();

        return View(new DeleteAccountViewModel { HasPassword = await users.HasPasswordAsync(user) });
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> DeleteAccount(string? confirmPassword, string? confirmText)
    {
        var user = await CurrentUserAsync();
        if (user is null) return await ForceLogoutAsync();

        var has = await users.HasPasswordAsync(user);
        var vm = new DeleteAccountViewModel { HasPassword = has };

        var hasActiveOrders = await db.Orders.AnyAsync(o => o.UserId == user.Id &&
            (o.Status == OrderStatus.Placed || o.Status == OrderStatus.Preparing || o.Status == OrderStatus.Ready));
        if (hasActiveOrders)
        {
            ModelState.AddModelError("", "You still have active pickup orders. Wait until they are claimed or cancelled, then try again.");
            return View(vm);
        }

        var confirmed = has
            ? !string.IsNullOrEmpty(confirmPassword) && await users.CheckPasswordAsync(user, confirmPassword)
            : string.Equals(confirmText?.Trim(), "DELETE", StringComparison.Ordinal);
        if (!confirmed)
        {
            ModelState.AddModelError("", has ? "Incorrect password." : "Type DELETE exactly to confirm.");
            return View(vm);
        }

        var oldAvatar = user.AvatarUrl;
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var token = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

            foreach (var login in await users.GetLoginsAsync(user))
                await users.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey);

            user.FullName = "Deleted User";
            user.CampusId = $"DELETED-{token}";
            user.UserName = $"deleted-{token}@deleted.invalid";
            user.Email = user.UserName;
            user.EmailConfirmed = false;
            user.PhoneNumber = null;
            user.PhoneNumberConfirmed = false;
            user.AvatarUrl = null;
            user.PasswordHash = null;
            user.NotifyOrderReady = false;
            user.NotifyPromotions = false;
            user.IsDeleted = true;
            user.DeletedAtUtc = DateTime.UtcNow;
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;
            user.SecurityStamp = Guid.NewGuid().ToString();

            var result = await users.UpdateAsync(user);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

            audit.Add(user.Id, "AccountDeleted", "Personal data anonymized");
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            log.LogError(ex, "Account deletion failed for {UserId}", user.Id);
            ModelState.AddModelError("", "We couldn't delete your account. Please try again.");
            return View(vm);
        }

        DeleteAvatarFile(oldAvatar);
        await signIn.SignOutAsync();
        HttpContext.Session.Clear();

        return View("Info", new InfoViewModel("Account deleted",
            "Your personal data has been erased. Past orders are kept in anonymized form for one academic year."));
    }
}