namespace CareFlow.Models;

/// <summary>An existing patient (seeded with fictional data; no registration module needed).</summary>
public class Patient
{
    public int Id { get; set; }                         // PK
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly BirthDate { get; set; }
    public string? ContactNumber { get; set; }

    public string FullName => $"{LastName}, {FirstName}";

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
