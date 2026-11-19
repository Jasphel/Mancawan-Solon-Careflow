
using CareFlow.Data;
using CareFlow.Models;
using Microsoft.EntityFrameworkCore;

namespace CareFlow.Services;

public class BookingService
{
    private readonly CareFlowDbContext _dbContext;
    private readonly AvailabilityService _availabilityService;

    public BookingService(
        CareFlowDbContext dbContext,
        AvailabilityService availabilityService)
    {
        _dbContext = dbContext;
        _availabilityService = availabilityService;
    }

    public async Task<Booking> ConfirmBookingAsync(
        int patientId,
        DateOnly bookingDate,
        IReadOnlyCollection<int> diagnosticServiceIds,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate the booking date.
        if (bookingDate < DateOnly.FromDateTime(DateTime.Today))
        {
            throw new ArgumentException(
                "Booking date cannot be in the past.",
                nameof(bookingDate));
        }

        // 2. Validate the number of diagnostic services.
        if (diagnosticServiceIds == null ||
            diagnosticServiceIds.Count < 1 ||
            diagnosticServiceIds.Count > 3)
        {
            throw new ArgumentException(
                "A booking must contain between 1 and 3 diagnostic services.");
        }

        // 3. Prevent duplicate diagnostic services.
        if (diagnosticServiceIds.Distinct().Count()
            != diagnosticServiceIds.Count)
        {
            throw new ArgumentException(
                "Duplicate diagnostic services are not allowed.");
        }

        // 4. Start a database transaction.
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            // 5. Check whether the patient exists.
            bool patientExists = await _dbContext.Patients
                .AnyAsync(
                    p => p.Id == patientId,
                    cancellationToken);

            if (!patientExists)
            {
                throw new ArgumentException(
                    $"Patient with ID {patientId} does not exist.");
            }

            // 6. Check whether all requested services exist.
            var existingServiceIds = await _dbContext.DiagnosticServices
                .Where(s => diagnosticServiceIds.Contains(s.Id))
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            if (existingServiceIds.Count != diagnosticServiceIds.Count)
            {
                throw new ArgumentException(
                    "One or more diagnostic services do not exist.");
            }

            // 7. Recheck availability using the existing AvailabilityService.
            foreach (int serviceId in diagnosticServiceIds)
            {
                bool isAvailable =
                    await _availabilityService.IsAvailableAsync(
                        serviceId,
                        bookingDate,
                        cancellationToken);

                if (!isAvailable)
                {
                    throw new InvalidOperationException(
                        $"Diagnostic service ID {serviceId} " +
                        "has no remaining capacity for the selected date.");
                }
            }

            // 8. Create the booking.
            var booking = new Booking
            {
                PatientId = patientId,
                BookingDate = bookingDate,
                CreatedAtUtc = DateTime.UtcNow
            };

            // 9. Create the booking items.
            foreach (int serviceId in diagnosticServiceIds)
            {
                booking.Items.Add(new BookingItem
                {
                    DiagnosticServiceId = serviceId
                });
            }

            // 10. Add the booking and its items to the database.
            await _dbContext.Bookings.AddAsync(
                booking,
                cancellationToken);

            // 11. Save all changes.
            await _dbContext.SaveChangesAsync(cancellationToken);

            // 12. Commit the transaction.
            await transaction.CommitAsync(cancellationToken);

            return booking;
        }
        catch
        {
            // 13. Roll back if anything fails.
            await transaction.RollbackAsync(
                CancellationToken.None);

            throw;
        }
    }
}
