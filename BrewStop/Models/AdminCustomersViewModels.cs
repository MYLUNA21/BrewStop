namespace BrewStop.Models;

public class AdminCustomerRow
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? CampusId { get; set; }
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
    public string Filter { get; set; } = "all";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, PageSize)));
    public Dictionary<string, int> Counts { get; set; } = new();
}

public class AdminCustomerDetailsViewModel
{
    public ApplicationUser User { get; set; } = null!;
    public List<string> Roles { get; set; } = new();
    public bool IsLocked { get; set; }
    public List<Order> Orders { get; set; } = new();
}