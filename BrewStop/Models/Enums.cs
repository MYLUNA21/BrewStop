namespace BrewStop.Models;

public enum ItemCategory
{
    Coffee = 1,
    NonCoffee = 2,
    Tea = 3,
    Pastry = 4,
    Snack = 5
}

public enum OrderStatus
{
    Pending = 0,
    Preparing = 1,
    Ready = 2,
    Completed = 3,
    Cancelled = 4
}

public enum PaymentMethod
{
    CashOnPickup = 0,
    GCash = 1,
    Maya = 2
}

public enum CustomizationType
{
    Size = 1,
    Temperature = 2,
    Milk = 3,
    Sweetness = 4,
    AddOn = 5
}

/// <summary>Entities stamped automatically by <c>BrewStopDbContext.SaveChanges</c>.</summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
