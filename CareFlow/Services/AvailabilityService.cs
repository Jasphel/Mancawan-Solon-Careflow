
using CareFlow.Data;
using Microsoft.EntityFrameworkCore;

namespace CareFlow.Services;

// Information returned for each diagnostic service.
public sealed record ServiceAvailability(
    int DiagnosticServiceId,
    string ServiceName,
    int DailyCapacity,
    int BookedCount,
    int RemainingCapacity
);

public class AvailabilityService
{
    private readonly CareFlowDbContext _dbContext;

    public AvailabilityService(CareFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // Retrieves all diagnostic services and their availability
    // for the selected date.
    public async Task<List<ServiceAvailability>> GetAvailabilityAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var services = await _dbContext.DiagnosticServices
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.DailyCapacity
            })
            .ToListAsync(cancellationToken);

        var bookedCounts = await _dbContext.BookingItems
            .Where(item => item.Booking.BookingDate == date)
            .GroupBy(item => item.DiagnosticServiceId)
            .Select(group => new
            {
                DiagnosticServiceId = group.Key,
                BookedCount = group.Count()
            })
            .ToDictionaryAsync(
                x => x.DiagnosticServiceId,
                x => x.BookedCount,
                cancellationToken);

        return services.Select(service =>
        {
            bookedCounts.TryGetValue(
                service.Id,
                out int bookedCount);

            int remainingCapacity = Math.Max(
                0,
                service.DailyCapacity - bookedCount);

            return new ServiceAvailability(
                service.Id,
                service.Name,
                service.DailyCapacity,
                bookedCount,
                remainingCapacity
            );
        }).ToList();
    }

    // Retrieves the remaining capacity of one service.
    public async Task<int> GetRemainingCapacityAsync(
        int diagnosticServiceId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var service = await _dbContext.DiagnosticServices
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == diagnosticServiceId,
                cancellationToken);

        if (service == null)
        {
            throw new KeyNotFoundException(
                $"Diagnostic service with ID {diagnosticServiceId} was not found.");
        }

        int bookedCount = await _dbContext.BookingItems
            .CountAsync(
                item =>
                    item.DiagnosticServiceId == diagnosticServiceId &&
                    item.Booking.BookingDate == date,
                cancellationToken);

        return Math.Max(
            0,
            service.DailyCapacity - bookedCount);
    }

    // Checks whether a service still has available capacity.
    public async Task<bool> IsAvailableAsync(
        int diagnosticServiceId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        int remainingCapacity = await GetRemainingCapacityAsync(
            diagnosticServiceId,
            date,
            cancellationToken);

        return remainingCapacity > 0;
    }
}
