
namespace BrewStop.Models;

public static class AppRoles
{
    public const string Student = "Student";
    public const string Faculty = "Faculty";
    public const string Barista = "Barista";
    public const string Admin = "Admin";

    public static readonly string[] All = [Student, Faculty, Barista, Admin];
}