namespace BrewStop.Models;

public record AdminSalesRow(string Label, int Orders, decimal Gross, decimal Discounts, decimal Net);
public record AdminProductRow(string Name, int Units, decimal Revenue);

public class AdminReportsViewModel
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<AdminSalesRow> Daily { get; set; } = new();
    public List<AdminSalesRow> Monthly { get; set; } = new();
    public List<AdminProductRow> MostPopular { get; set; } = new();
    public List<AdminProductRow> LeastPopular { get; set; } = new();
    public List<Order> Transactions { get; set; } = new();

    public static string ItemsSummary(Order o) =>
        string.Join(", ", o.Items.Select(i => $"{i.Quantity}× {i.ItemName}{(i.SizeName is null ? "" : $" ({i.SizeName})")}"));
}