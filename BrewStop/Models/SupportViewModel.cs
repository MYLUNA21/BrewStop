using System.ComponentModel.DataAnnotations;

namespace BrewStop.Models;

public class SupportViewModel
{
    [Required, StringLength(100), Display(Name = "Your name")]
    [RegularExpression(@"^[^<>]*$", ErrorMessage = "Name contains characters that are not allowed.")]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string Topic { get; set; } = "Order issue";

    [Required, StringLength(1000, MinimumLength = 10, ErrorMessage = "Message must be 10–1000 characters.")]
    [RegularExpression(@"^[^<>\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]*$", ErrorMessage = "Message contains characters that are not allowed.")]
    public string Message { get; set; } = string.Empty;
}