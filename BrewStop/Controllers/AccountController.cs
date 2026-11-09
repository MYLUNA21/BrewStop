using BrewStop.Models;
using BrewStop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace BrewStop.Controllers;

public class AccountController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IEmailService email,
    ICaptchaService captcha,
    IAuditService audit, // NEW: Injected Audit Service
    IConfiguration config,
    ILogger<AccountController> log) : Controller
{
    // Register-
    [HttpGet, AllowAnonymous]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Register(RegisterViewModel m)
    {
        // SRS-FR-07: reject bots before touching the database.
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

        // NEW: Audit Log for Registration Role Assignment
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

    // Login / Logout
    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel m)
    {
        if (!ModelState.IsValid) return View(m);

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

    [HttpPost, Authorize]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

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