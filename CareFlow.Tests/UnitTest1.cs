
using CareFlow.Data;
using CareFlow.Models;
using CareFlow.Services;
using Microsoft.EntityFrameworkCore;

namespace CareFlow.Tests;

public class AvailabilityServiceTests
{
    private static CareFlowDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CareFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CareFlowDbContext(options);
    }

    private static async Task<(Patient Patient, DiagnosticService Service)>
        SeedBaseAsync(CareFlowDbContext context, int capacity)
    {
        var patient = new Patient
        {
            FirstName = "Test",
            LastName = "Patient",
            BirthDate = new DateOnly(1995, 1, 1)
        };

        var service = new DiagnosticService
        {
            Name = "CBC",
            DailyCapacity = capacity
        };

        context.Patients.Add(patient);
        context.DiagnosticServices.Add(service);

        await context.SaveChangesAsync();

        return (patient, service);
    }

    private static async Task AddBookingsAsync(
        CareFlowDbContext context,
        Patient patient,
        DiagnosticService service,
        DateOnly date,
        int count)
    {
        for (int i = 0; i < count; i++)
        {
            context.Bookings.Add(new Booking
            {
                PatientId = patient.Id,
                BookingDate = date,
                CreatedAtUtc = DateTime.UtcNow,
                Items = new List<BookingItem>
                {
                    new BookingItem
                    {
                        DiagnosticServiceId = service.Id
                    }
                }
            });
        }

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetRemainingCapacity_ReturnsCorrectCount()
    {
        await using var context = CreateContext();

        var (patient, service) = await SeedBaseAsync(context, 20);
        var date = new DateOnly(2026, 12, 1);

        await AddBookingsAsync(
            context, patient, service, date, 12);

        var availabilityService = new AvailabilityService(context);

        var result = await availabilityService.GetRemainingCapacityAsync(
            service.Id, date);

        Assert.Equal(8, result);
    }

    [Fact]
    public async Task IsAvailable_ReturnsFalse_WhenCapacityIsFull()
    {
        await using var context = CreateContext();

        var (patient, service) = await SeedBaseAsync(context, 5);
        var date = new DateOnly(2026, 12, 1);

        await AddBookingsAsync(
            context, patient, service, date, 5);

        var availabilityService = new AvailabilityService(context);

        var result = await availabilityService.IsAvailableAsync(
            service.Id, date);

        Assert.False(result);
    }

    [Fact]
    public async Task IsAvailable_ReturnsTrue_WhenSlotsRemain()
    {
        await using var context = CreateContext();

        var (patient, service) = await SeedBaseAsync(context, 10);
        var date = new DateOnly(2026, 12, 1);

        await AddBookingsAsync(
            context, patient, service, date, 3);

        var availabilityService = new AvailabilityService(context);

        var result = await availabilityService.IsAvailableAsync(
            service.Id, date);

        Assert.True(result);
    }

    [Fact]
    public async Task GetAvailability_ReturnsCorrectServiceInformation()
    {
        await using var context = CreateContext();

        var (patient, service) = await SeedBaseAsync(context, 20);
        var date = new DateOnly(2026, 12, 1);

        await AddBookingsAsync(
            context, patient, service, date, 12);

        var availabilityService = new AvailabilityService(context);

        var result = await availabilityService.GetAvailabilityAsync(date);

        var item = Assert.Single(result);

        Assert.Equal(service.Id, item.DiagnosticServiceId);
        Assert.Equal("CBC", item.ServiceName);
        Assert.Equal(20, item.DailyCapacity);
        Assert.Equal(12, item.BookedCount);
        Assert.Equal(8, item.RemainingCapacity);
    }

    [Fact]
    public async Task GetRemainingCapacity_Throws_WhenServiceDoesNotExist()
    {
        await using var context = CreateContext();

        var availabilityService = new AvailabilityService(context);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => availabilityService.GetRemainingCapacityAsync(
                999, new DateOnly(2026, 12, 1)));
    }
}
