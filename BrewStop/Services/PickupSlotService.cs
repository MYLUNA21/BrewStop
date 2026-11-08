using System.Globalization;
using Microsoft.Extensions.Options;

namespace BrewStop.Services;

public class ShopOptions
{
    public string OpenTime { get; set; } = "08:00";
    public string CloseTime { get; set; } = "17:00";
    public int SlotMinutes { get; set; } = 10;
    public int LeadMinutes { get; set; } = 10;
    public string TimeZone { get; set; } = "Asia/Manila";
}

/// <param name="Utc">Slot start in UTC (what gets stored).</param>
public record PickupSlot(DateTime Utc, string Label)
{
    public string Value => Utc.ToString("yyyy-MM-dd'T'HH:mm'Z'", CultureInfo.InvariantCulture);
}

public interface IPickupSlotService
{
    IReadOnlyList<PickupSlot> GetAvailableSlots();
    bool IsValid(DateTime utc);
    DateTime ToLocal(DateTime utc);
}

public class PickupSlotService(IOptions<ShopOptions> options) : IPickupSlotService
{
    private readonly ShopOptions _o = options.Value;
    private readonly TimeZoneInfo _tz = ResolveZone(options.Value.TimeZone);

    private static TimeZoneInfo ResolveZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.CreateCustomTimeZone("PHT", TimeSpan.FromHours(8), "PHT", "PHT"); }
    }

    public DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _tz);

    public IReadOnlyList<PickupSlot> GetAvailableSlots()
    {
        var step = Math.Max(1, _o.SlotMinutes);
        var nowLocal = ToLocal(DateTime.UtcNow);
        var today = nowLocal.Date;

        var open = today + TimeOnly.Parse(_o.OpenTime, CultureInfo.InvariantCulture).ToTimeSpan();
        var close = today + TimeOnly.Parse(_o.CloseTime, CultureInfo.InvariantCulture).ToTimeSpan();

        var earliest = nowLocal.AddMinutes(_o.LeadMinutes);
        var start = earliest > open ? earliest : open;

        var minutes = (int)Math.Ceiling((start - today).TotalMinutes / step) * step;
        var t = today.AddMinutes(minutes);

        var slots = new List<PickupSlot>();
        while (t < close)
        {
            var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(t, DateTimeKind.Unspecified), _tz);
            slots.Add(new PickupSlot(utc, t.ToString("h:mm tt", CultureInfo.InvariantCulture)));
            t = t.AddMinutes(step);
        }
        return slots;
    }

    public bool IsValid(DateTime utc) => GetAvailableSlots().Any(s => s.Utc == utc);
}