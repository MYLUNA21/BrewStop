namespace BrewStop.Models;

public class AdminCustomerRow
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string CampusId { get; set; } = "";
    public string? Email { get; set; }
    public List<string> Roles { get; set; } = new();
    public bool EmailConfirmed { get; set; }
    public bool IsLocked { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }
    public bool IsFrequent { get; set; }
}

public class AdminCustomersViewModel
{
    public List<AdminCustomerRow> Rows { get; set; } = new();
    public string? Query { get; set; }
}

public class AdminCustomerDetailsViewModel
{
    public ApplicationUser User { get; set; } = null!;
    public List<string> Roles { get; set; } = new();
    public bool IsLocked { get; set; }
    public List<Order> Orders { get; set; } = new();
}