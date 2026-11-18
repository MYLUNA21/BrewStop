using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Claims;

namespace BrewStop.Controllers;

public class AccountController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IEmailService email,
    ICaptchaService captcha,
    IAuditService audit,
    IConfiguration config,
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

        var domain = config["Campus:AllowedEmailDomain"];
        if (!string.IsNullOrWhiteSpace(domain) &&
            !m.Email.EndsWith("@" + domain, StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(m.Email), $"Use your campus email (@{domain}).");

        if (m.Role is not (AppRoles.Student or AppRoles.Faculty))
            ModelState.AddModelError(nameof(m.Role), "Invalid role.");

        if (await users.Users.AnyAsync(u => u.CampusId == m.CampusId.Trim()))
            ModelState.AddModelError(nameof(m.CampusId), "Campus ID is already registered.");

        if (!ModelState.IsValid) return View(m);

        var user = new ApplicationUser
        {
            UserName = m.Email,
            Email = m.Email,
            FullName = m.FullName.Trim(),
            CampusId = m.CampusId.Trim()
        };

        var result = await users.CreateAsync(user, m.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(m);
        }

        await users.AddToRoleAsync(user, m.Role);
        await audit.LogAsync(user.Id, "RoleAssigned", $"Role={m.Role}; Source=Registration");

        await SendConfirmationAsync(user);
        log.LogInformation("User registered {UserId} as {Role}", user.Id, m.Role);

        return View("Info", new InfoViewModel("Check your email",
            "We sent a verification link. Verify your email before signing in."));
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

        var result = await signIn.PasswordSignInAsync(m.Email, m.Password,
            isPersistent: false, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            log.LogInformation("Login success for {Email}", m.Email);
            return LocalRedirect(Url.IsLocalUrl(m.ReturnUrl) ? m.ReturnUrl! : "/");
        }

        if (result.IsLockedOut)
        {
            log.LogWarning("Account locked out: {Email}", m.Email);
            ModelState.AddModelError("", "Account locked for 15 minutes after 5 failed attempts.");
        }
        else if (result.IsNotAllowed)
        {
            ModelState.AddModelError("", "Verify your email before signing in.");
            ViewBag.NeedsVerification = true;
        }
        else
        {
            log.LogWarning("Failed login for {Email}", m.Email);
            ModelState.AddModelError("", "Invalid email or password.");
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

    // My Account
    [HttpGet, Authorize]
    public async Task<IActionResult> Manage()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return RedirectToAction(nameof(Login));
        return View(await BuildManageAsync(user));
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Profile")] ProfileInput input)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return RedirectToAction(nameof(Login));

        var fullName = (input.FullName ?? string.Empty).Trim();
        var campusId = (input.CampusId ?? string.Empty).Trim();

        if (campusId.StartsWith("GOOGLE-", StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError("Profile.CampusId", "Please enter your real Campus ID.");
        else if (campusId.Length > 0 &&
                 await users.Users.AnyAsync(u => u.Id != user.Id && u.CampusId == campusId, HttpContext.RequestAborted))
            ModelState.AddModelError("Profile.CampusId", "This Campus ID is already registered to another account.");

        if (!ModelState.IsValid)
            return View(nameof(Manage), await BuildManageAsync(user, input));

        user.FullName = fullName;
        user.CampusId = campusId;

        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(nameof(Manage), await BuildManageAsync(user, input));
        }

        await signIn.RefreshSignInAsync(user);
        await audit.LogAsync(user.Id, "ProfileUpdated", "Fields=FullName,CampusId");
        TempData["Success"] = "Profile updated.";
        return RedirectToAction(nameof(Manage));
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword([Bind(Prefix = "Password")] ChangePasswordInput input)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return RedirectToAction(nameof(Login));

        var hasPassword = await users.HasPasswordAsync(user);
        if (hasPassword && string.IsNullOrEmpty(input.CurrentPassword))
            ModelState.AddModelError("Password.CurrentPassword", "Enter your current password.");

        if (!ModelState.IsValid)
            return View(nameof(Manage), await BuildManageAsync(user));

        var result = hasPassword
            ? await users.ChangePasswordAsync(user, input.CurrentPassword!, input.NewPassword)
            : await users.AddPasswordAsync(user, input.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(hasPassword && e.Code == "PasswordMismatch"
                    ? "Password.CurrentPassword" : "Password.NewPassword", e.Description);
            return View(nameof(Manage), await BuildManageAsync(user));
        }

        await signIn.RefreshSignInAsync(user);
        await audit.LogAsync(user.Id, hasPassword ? "PasswordChanged" : "PasswordSet", "Source=Manage");
        TempData["Success"] = hasPassword ? "Password changed." : "Password set. You can now also sign in with email.";
        return RedirectToAction(nameof(Manage));
    }

    private async Task<ManageViewModel> BuildManageAsync(ApplicationUser user, ProfileInput? profile = null) => new()
    {
        Email = user.Email ?? user.UserName ?? string.Empty,
        HasPassword = await users.HasPasswordAsync(user),
        IsCustomer = User.IsInRole(AppRoles.Student) || User.IsInRole(AppRoles.Faculty),
        Profile = profile ?? new ProfileInput
        {
            FullName = user.FullName,
            CampusId = user.CampusId.StartsWith("GOOGLE-", StringComparison.OrdinalIgnoreCase) ? string.Empty : user.CampusId
        }
    };

    [HttpGet, AllowAnonymous]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    // Password reset
    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel m)
    {
        if (!ModelState.IsValid) return View(m);

        var user = await users.FindByEmailAsync(m.Email);

        if (user is null)
        {
            log.LogWarning("Password reset requested for unknown email {Email}", m.Email);
        }
        else if (!await users.IsEmailConfirmedAsync(user))
        {
            log.LogWarning("Password reset skipped: email {Email} is not confirmed yet", m.Email);
        }
        else
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var link = Url.Action(nameof(ResetPassword), "Account",
                new { email = user.Email, token }, Request.Scheme)!;
            await email.SendAsync(user.Email!, "Reset your BrewStop password",
                $"<p>Reset your password: <a href=\"{link}\">click here</a>.</p>");
        }

        return View("Info", new InfoViewModel("Check your email",
            "If that account exists, a reset link has been sent."));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (email is null || token is null) return BadRequest();
        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel m)
    {
        if (!ModelState.IsValid) return View(m);

        var user = await users.FindByEmailAsync(m.Email);
        if (user is not null)
        {
            var result = await users.ResetPasswordAsync(user, m.Token, m.Password);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
                return View(m);
            }
        }

        return View("Info", new InfoViewModel("Password updated", "You can now sign in with your new password."));
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
}