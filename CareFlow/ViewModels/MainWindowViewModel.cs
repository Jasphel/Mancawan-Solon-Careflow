using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CareFlow.Data;
using CareFlow.Models;
using CareFlow.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;

namespace CareFlow.ViewModels;

public sealed class ServiceSelectionItem : ObservableObject
{
    private bool _isSelected;

    public int DiagnosticServiceId { get; init; }
    public string ServiceName { get; init; } = string.Empty;
    public int DailyCapacity { get; init; }
    public int BookedCount { get; init; }
    public int RemainingCapacity { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string SummaryText =>
        $"{ServiceName} — {RemainingCapacity} remaining ({BookedCount}/{DailyCapacity} booked)";
}

public sealed class ConfirmedBookingSummary
{
    public int BookingId { get; init; }
    public string PatientName { get; init; } = string.Empty;
    public DateOnly BookingDate { get; init; }
    public string ServicesSummary { get; init; } = string.Empty;
}

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly CareFlowDbContext _dbContext;
    private readonly AvailabilityService _availabilityService;
    private readonly BookingService _bookingService;

    private DateTimeOffset? _selectedDate = new DateTimeOffset(DateTime.Today);
    private Patient? _selectedPatient;
    private ConfirmedBookingSummary? _selectedConfirmedBooking;
    private string _statusMessage = "Select a date to view availability or create a booking.";
    private bool _isBusy;

    public string Title => "CareFlow - Diagnostic Booking Management";

    public DateTimeOffset? SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value))
            {
                _ = RefreshAvailabilityAsync();
            }
        }
    }

    public Patient? SelectedPatient
    {
        get => _selectedPatient;
        set => SetProperty(ref _selectedPatient, value);
    }

    public ConfirmedBookingSummary? SelectedConfirmedBooking
    {
        get => _selectedConfirmedBooking;
        set => SetProperty(ref _selectedConfirmedBooking, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public ObservableCollection<Patient> Patients { get; } = [];
    public ObservableCollection<ServiceSelectionItem> ServiceItems { get; } = [];
    public ObservableCollection<ConfirmedBookingSummary> ConfirmedBookings { get; } = [];

    public MainWindowViewModel(
        CareFlowDbContext dbContext,
        AvailabilityService availabilityService,
        BookingService bookingService)
    {
        _dbContext = dbContext;
        _availabilityService = availabilityService;
        _bookingService = bookingService;
    }

    public async Task InitializeAsync()
    {
        await LoadPatientsAsync();
        await RefreshAvailabilityAsync();
        await LoadConfirmedBookingsAsync();
    }

    [RelayCommand]
    public async Task RefreshAvailabilityAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await RefreshAvailabilityCoreAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshAvailabilityCoreAsync()
    {
        try
        {
            DateOnly date = GetBookingDate();

            List<ServiceAvailability> availabilityList =
                await _availabilityService.GetAvailabilityAsync(date);

            HashSet<int> previouslySelectedIds = ServiceItems
                .Where(s => s.IsSelected)
                .Select(s => s.DiagnosticServiceId)
                .ToHashSet();

            ServiceItems.Clear();
            foreach (ServiceAvailability item in availabilityList)
            {
                ServiceItems.Add(new ServiceSelectionItem
                {
                    DiagnosticServiceId = item.DiagnosticServiceId,
                    ServiceName = item.ServiceName,
                    DailyCapacity = item.DailyCapacity,
                    BookedCount = item.BookedCount,
                    RemainingCapacity = item.RemainingCapacity,
                    IsSelected = previouslySelectedIds.Contains(item.DiagnosticServiceId)
                });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load availability: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ConfirmBookingAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;

            if (SelectedPatient is null)
            {
                StatusMessage = "Please select an existing patient before confirming a booking.";
                return;
            }

            DateOnly bookingDate = GetBookingDate();
            List<int> selectedServiceIds = ServiceItems
                .Where(s => s.IsSelected)
                .Select(s => s.DiagnosticServiceId)
                .ToList();

            Booking confirmed = await _bookingService.ConfirmBookingAsync(
                SelectedPatient.Id,
                bookingDate,
                selectedServiceIds);

            foreach (ServiceSelectionItem item in ServiceItems)
            {
                item.IsSelected = false;
            }

            StatusMessage =
                $"Booking #{confirmed.Id} confirmed for {SelectedPatient.FullName} on {bookingDate:yyyy-MM-dd}.";

            await RefreshAvailabilityCoreAsync();
            await LoadConfirmedBookingsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Booking rejected: {ex.Message}";
            await RefreshAvailabilityCoreAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task CancelSelectedBookingAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (SelectedConfirmedBooking is null)
        {
            StatusMessage = "Please select a confirmed booking to cancel.";
            return;
        }

        try
        {
            IsBusy = true;

            int bookingId = SelectedConfirmedBooking.BookingId;
            Booking? booking = await _dbContext.Bookings
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking is null)
            {
                StatusMessage = $"Booking #{bookingId} was not found.";
                await LoadConfirmedBookingsAsync();
                return;
            }

            _dbContext.Bookings.Remove(booking);
            await _dbContext.SaveChangesAsync();

            SelectedConfirmedBooking = null;
            StatusMessage = $"Booking #{bookingId} cancelled and removed.";

            await RefreshAvailabilityCoreAsync();
            await LoadConfirmedBookingsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not cancel booking: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task LoadConfirmedBookingsAsync()
    {
        try
        {
            var bookings = await _dbContext.Bookings
                .AsNoTracking()
                .Include(b => b.Patient)
                .Include(b => b.Items)
                    .ThenInclude(i => i.DiagnosticService)
                .OrderByDescending(b => b.BookingDate)
                .ThenByDescending(b => b.Id)
                .ToListAsync();

            ConfirmedBookings.Clear();
            foreach (var b in bookings)
            {
                string servicesText = string.Join(
                    ", ",
                    b.Items
                        .Select(i => i.DiagnosticService?.Name)
                        .Where(name => !string.IsNullOrWhiteSpace(name)));

                ConfirmedBookings.Add(new ConfirmedBookingSummary
                {
                    BookingId = b.Id,
                    PatientName = b.Patient?.FullName ?? $"Patient #{b.PatientId}",
                    BookingDate = b.BookingDate,
                    ServicesSummary = string.IsNullOrWhiteSpace(servicesText)
                        ? "No services listed"
                        : servicesText
                });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load confirmed bookings: {ex.Message}";
        }
    }

    private async Task LoadPatientsAsync()
    {
        var patients = await _dbContext.Patients
            .AsNoTracking()
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync();

        Patients.Clear();
        foreach (var patient in patients)
        {
            Patients.Add(patient);
        }

        if (SelectedPatient is null && Patients.Count > 0)
        {
            SelectedPatient = Patients[0];
        }
    }

    private DateOnly GetBookingDate()
    {
        DateTime dateTime = SelectedDate?.DateTime ?? DateTime.Today;
        return DateOnly.FromDateTime(dateTime);
    }
}