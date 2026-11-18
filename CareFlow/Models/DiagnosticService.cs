namespace CareFlow.Models;

/// <summary>A diagnostic service (e.g. CBC, Urinalysis, Chest X-Ray, ECG) with a daily capacity.</summary>
public class DiagnosticService
{
    public int Id { get; set; }                         // PK
    public string Name { get; set; } = string.Empty;    // unique
    public int DailyCapacity { get; set; }              // max bookings per day (BR-05)

    public ICollection<BookingItem> BookingItems { get; set; } = new List<BookingItem>();
}
