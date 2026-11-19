
using CareFlow.Data;
using CareFlow.Models;
using CareFlow.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CareFlow.Tests;

public class BookingServiceTests
{
    private static async Task<(SqliteConnection Connection,
        CareFlowDbContext Context)> CreateContextAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<CareFlowDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new CareFlowDbContext(options);

        await context.Database.EnsureCreatedAsync();

        return (connection, context);
    }

    private static async Task<(Patient Patient,
        DiagnosticService Service)> SeedDataAsync(
        CareFlowDbContext context,
        int capacity = 10)
    {
        var patient = new Patient
        {
            FirstName = "Test",
            LastName = "Patient",
            BirthDate = new DateOnly(2000, 1, 1)
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

    private static BookingService CreateBookingService(
        CareFlowDbContext context)
    {
        var availabilityService = new AvailabilityService(context);

        return new BookingService(context, availabilityService);
    }

    private static DateOnly FutureDate =>
        DateOnly.FromDateTime(DateTime.Today.AddDays(1));

    [Fact]
    public async Task ConfirmBookingAsync_SavesValidBooking()
    {
        var (connection, context) = await CreateContextAsync();

        await using var dbConnection = connection;
        await using var dbContext = context;

        var (patient, service) = await SeedDataAsync(dbContext);

        var bookingService = CreateBookingService(dbContext);

        var result = await bookingService.ConfirmBookingAsync(
            patient.Id,
            FutureDate,
            new[] { service.Id });

        Assert.NotNull(result);
        Assert.Equal(patient.Id, result.PatientId);
        Assert.Equal(FutureDate, result.BookingDate);
        Assert.Single(result.Items);

        Assert.Equal(1, await dbContext.Bookings.CountAsync());
        Assert.Equal(1, await dbContext.BookingItems.CountAsync());
    }

    [Fact]
    public async Task ConfirmBookingAsync_RejectsNonexistentPatient()
    {
        var (connection, context) = await CreateContextAsync();

        await using var dbConnection = connection;
        await using var dbContext = context;

        var (_, service) = await SeedDataAsync(dbContext);

        var bookingService = CreateBookingService(dbContext);

        await Assert.ThrowsAsync<ArgumentException>(
            () => bookingService.ConfirmBookingAsync(
                999,
                FutureDate,
                new[] { service.Id }));

        Assert.Equal(0, await dbContext.Bookings.CountAsync());
    }

    [Fact]
    public async Task ConfirmBookingAsync_RejectsPastDate()
    {
        var (connection, context) = await CreateContextAsync();

        await using var dbConnection = connection;
        await using var dbContext = context;

        var (patient, service) = await SeedDataAsync(dbContext);

        var bookingService = CreateBookingService(dbContext);

        var pastDate = DateOnly.FromDateTime(
            DateTime.Today.AddDays(-1));

        await Assert.ThrowsAsync<ArgumentException>(
            () => bookingService.ConfirmBookingAsync(
                patient.Id,
                pastDate,
                new[] { service.Id }));
    }

    [Fact]
    public async Task ConfirmBookingAsync_RejectsZeroServices()
    {
        var (connection, context) = await CreateContextAsync();

        await using var dbConnection = connection;
        await using var dbContext = context;

        var (patient, _) = await SeedDataAsync(dbContext);

        var bookingService = CreateBookingService(dbContext);

        await Assert.ThrowsAsync<ArgumentException>(
            () => bookingService.ConfirmBookingAsync(
                patient.Id,
                FutureDate,
                Array.Empty<int>()));
    }

    [Fact]
    public async Task ConfirmBookingAsync_RejectsMoreThanThreeServices()
    {
        var (connection, context) = await CreateContextAsync();

        await using var dbConnection = connection;
        await using var dbContext = context;

        var (patient, _) = await SeedDataAsync(dbContext);

        var service2 = new DiagnosticService
        {
            Name = "Urinalysis",
            DailyCapacity = 10
        };

        var service3 = new DiagnosticService
        {
            Name = "Chest X-Ray",
            DailyCapacity = 10
        };

        var service4 = new DiagnosticService
        {
            Name = "ECG",
            DailyCapacity = 10
        };

        dbContext.DiagnosticServices.AddRange(
            service2, service3, service4);

        await dbContext.SaveChangesAsync();

        var serviceIds = await dbContext.DiagnosticServices
            .Select(s => s.Id)
            .ToArrayAsync();

        var bookingService = CreateBookingService(dbContext);

        await Assert.ThrowsAsync<ArgumentException>(
            () => bookingService.ConfirmBookingAsync(
                patient.Id,
                FutureDate,
                serviceIds));
    }

    [Fact]
    public async Task ConfirmBookingAsync_RejectsDuplicateServices()
    {
        var (connection, context) = await CreateContextAsync();

        await using var dbConnection = connection;
        await using var dbContext = context;

        var (patient, service) = await SeedDataAsync(dbContext);

        var bookingService = CreateBookingService(dbContext);

        await Assert.ThrowsAsync<ArgumentException>(
            () => bookingService.ConfirmBookingAsync(
                patient.Id,
                FutureDate,
                new[] { service.Id, service.Id }));
    }

    [Fact]
    public async Task ConfirmBookingAsync_RejectsNonexistentService()
    {
        var (connection, context) = await CreateContextAsync();

        await using var dbConnection = connection;
        await using var dbContext = context;

        var (patient, _) = await SeedDataAsync(dbContext);

        var bookingService = CreateBookingService(dbContext);

        await Assert.ThrowsAsync<ArgumentException>(
            () => bookingService.ConfirmBookingAsync(
                patient.Id,
                FutureDate,
                new[] { 999 }));

        Assert.Equal(0, await dbContext.Bookings.CountAsync());
    }

    [Fact]
    public async Task ConfirmBookingAsync_RejectsFullyBookedService()
    {
        var (connection, context) = await CreateContextAsync();

        await using var dbConnection = connection;
        await using var dbContext = context;

        var (patient, service) = await SeedDataAsync(
            dbContext, capacity: 1);

        // Fill the only available slot.
        dbContext.Bookings.Add(new Booking
        {
            PatientId = patient.Id,
            BookingDate = FutureDate,
            CreatedAtUtc = DateTime.UtcNow,
            Items = new List<BookingItem>
            {
                new BookingItem
                {
                    DiagnosticServiceId = service.Id
                }
            }
        });

        await dbContext.SaveChangesAsync();

        var bookingService = CreateBookingService(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => bookingService.ConfirmBookingAsync(
                patient.Id,
                FutureDate,
                new[] { service.Id }));

        // The rejected booking must not be saved.
        Assert.Equal(1, await dbContext.Bookings.CountAsync());
    }

    [Fact]
    public async Task ConfirmBookingAsync_DoesNotSaveBooking_WhenServiceDoesNotExist()
    {
        var (connection, context) = await CreateContextAsync();

        await using var dbConnection = connection;
        await using var dbContext = context;

        var (patient, _) = await SeedDataAsync(dbContext);

        var bookingService = CreateBookingService(dbContext);

        await Assert.ThrowsAsync<ArgumentException>(
            () => bookingService.ConfirmBookingAsync(
                patient.Id,
                FutureDate,
                new[] { 999 }));

        Assert.Equal(0, await dbContext.Bookings.CountAsync());
        Assert.Equal(0, await dbContext.BookingItems.CountAsync());
    }
}
