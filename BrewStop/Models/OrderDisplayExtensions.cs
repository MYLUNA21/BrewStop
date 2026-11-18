namespace BrewStop.Models;

public static class OrderDisplayExtensions
{
    
    public static string Label(this PaymentStatus s) => s switch
    {
        PaymentStatus.Unpaid => "Pending",
        PaymentStatus.Paid => "Paid",
        PaymentStatus.Refunded => "Refunded",
        _ => s.ToString()
    };
}