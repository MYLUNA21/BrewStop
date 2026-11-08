using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace BrewStop.Models;

public class ApplicationUser : IdentityUser
{
    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string CampusId { get; set; } = string.Empty;
}