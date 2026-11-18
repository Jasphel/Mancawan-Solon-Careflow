namespace CareFlow.Models;

/// <summary>
/// Join row: one service inside a booking. Each row consumes one unit of that
/// service's daily capacity on the booking's date (BR-04).
/// Unique (BookingId, DiagnosticServiceId) prevents duplicate services (BR-06).
/// </summary>
public class BookingItem
{
    public int Id { get; set; }                         // PK
    public int BookingId { get; set; }                  // FK -> Booking
    public int DiagnosticServiceId { get; set; }        // FK -> DiagnosticService

    public Booking Booking { get; set; } = null!;
    public DiagnosticService DiagnosticService { get; set; } = null!;
}
