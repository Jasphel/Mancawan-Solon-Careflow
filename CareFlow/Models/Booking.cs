namespace CareFlow.Models;

/// <summary>
/// One confirmed booking: one patient, one date, 1-3 distinct services (BR-02, BR-06).
/// Confirmed bookings are permanent (no cancellation/rescheduling).
/// </summary>
public class Booking
{
    public int Id { get; set; }                         // PK
    public int PatientId { get; set; }                  // FK -> Patient (BR-01)
    public DateOnly BookingDate { get; set; }           // date of the diagnostic services
    public DateTime CreatedAtUtc { get; set; }          // when staff confirmed it

    public Patient Patient { get; set; } = null!;
    public ICollection<BookingItem> Items { get; set; } = new List<BookingItem>();
}
