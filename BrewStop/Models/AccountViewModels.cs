using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace BrewStop.Models;

public record InfoViewModel(string Title, string Message);

public class RegisterViewModel
{
    [Required, StringLength(150), Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(30, MinimumLength = 3), Display(Name = "Username")]
    [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Use letters, numbers, dots, dashes or underscores only.")]
    public string UserName { get; set; } = string.Empty;

    // Auto-generated server-side; no longer posted by the form.
    [StringLength(30), Display(Name = "Campus ID")]
    public string? CampusId { get; set; }

    [EmailAddress, StringLength(256), Display(Name = "Email")]
    public string? Email { get; set; }

    [Required]
    public string Role { get; set; } = AppRoles.Student;

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password)), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class LoginViewModel
{
    [Required, StringLength(256), Display(Name = "Username or email")]
    public string UserNameOrEmail { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class ForgotPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\d{6}$", ErrorMessage = "Enter the 6-digit code."), Display(Name = "6-digit code")]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password), Display(Name = "New password")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password)), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

// ===================== Account Settings Hub =====================

public class ManageProfileViewModel
{
    public string Email { get; set; } = string.Empty;      // display only
    public string? AvatarUrl { get; set; }                 // display only

    [Required, StringLength(150), Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(30), Display(Name = "Campus ID")]
    public string CampusId { get; set; } = string.Empty;

    [StringLength(20), Display(Name = "Mobile number")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Profile photo")]
    public IFormFile? Avatar { get; set; }
}

public class ChangePasswordViewModel
{
    public bool HasPassword { get; set; }

    [DataType(DataType.Password), Display(Name = "Current password")]
    public string? CurrentPassword { get; set; }

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password), Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(NewPassword)), Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class SecurityViewModel
{
    public string Email { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public bool HasPassword { get; set; }
    public bool LockoutEnabled { get; set; }
    public bool IsLockedOut { get; set; }
    public int MaxFailedAttempts { get; set; }
    public int LockoutMinutes { get; set; }
    public bool GoogleLinked { get; set; }
}

public class NotificationsViewModel
{
    [Display(Name = "Notify me when my order is ready")]
    public bool NotifyOrderReady { get; set; }

    [Display(Name = "Send me promotions and vouchers")]
    public bool NotifyPromotions { get; set; }
}

public class ActivityViewModel
{
    public List<AuditLog> Entries { get; set; } = new();
    public string Category { get; set; } = "all";
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
}

public class DeleteAccountViewModel
{
    public bool HasPassword { get; set; }
}

public class CompleteCampusIdViewModel
{
    [Required, StringLength(30), Display(Name = "Campus ID")]
    public string CampusId { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}