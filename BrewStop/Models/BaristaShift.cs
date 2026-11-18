using System.ComponentModel.DataAnnotations;

namespace BrewStop.Models;

public enum ShiftStatus { Scheduled, InProgress, Completed, Missed }

public class BaristaShift
{
    public int Id { get; set; }

    [Required]
    public string BaristaUserId { get; set; } = string.Empty;
    public ApplicationUser BaristaUser { get; set; } = null!;

    public DateTime ScheduledStart { get; set; }   // UTC
    public DateTime ScheduledEnd { get; set; }     // UTC

    public DateTime? ClockInAt { get; set; }       // UTC
    public DateTime? ClockOutAt { get; set; }      // UTC

    public decimal? TotalHours { get; set; }
    public ShiftStatus ShiftStatus { get; set; } = ShiftStatus.Scheduled;
}