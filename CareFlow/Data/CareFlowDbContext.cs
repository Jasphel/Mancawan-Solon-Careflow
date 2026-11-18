
using CareFlow.Models;
using Microsoft.EntityFrameworkCore;

namespace CareFlow.Data;

public class CareFlowDbContext : DbContext
{
    public CareFlowDbContext(DbContextOptions<CareFlowDbContext> options)
        : base(options)
    {
    }

    // Database tables
    public DbSet<Patient> Patients => Set<Patient>();

    public DbSet<DiagnosticService> DiagnosticServices
        => Set<DiagnosticService>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<BookingItem> BookingItems => Set<BookingItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // PATIENT
        modelBuilder.Entity<Patient>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.FirstName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(p => p.LastName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(p => p.ContactNumber)
                .HasMaxLength(30);
        });

        // DIAGNOSTIC SERVICE
        modelBuilder.Entity<DiagnosticService>(entity =>
        {
            entity.HasKey(s => s.Id);

            entity.Property(s => s.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.HasIndex(s => s.Name)
                .IsUnique();

            entity.ToTable(t =>
                t.HasCheckConstraint(
                    "CK_DiagnosticServices_DailyCapacity",
                    "\"DailyCapacity\" > 0"));
        });

        // BOOKING
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(b => b.Id);

            entity.Property(b => b.CreatedAtUtc)
                .IsRequired();

            entity.HasOne(b => b.Patient)
                .WithMany(p => p.Bookings)
                .HasForeignKey(b => b.PatientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // BOOKING ITEM
        modelBuilder.Entity<BookingItem>(entity =>
        {
            entity.HasKey(bi => bi.Id);

            entity.HasIndex(bi => new
            {
                bi.BookingId,
                bi.DiagnosticServiceId
            }).IsUnique();

            entity.HasOne(bi => bi.Booking)
                .WithMany(b => b.Items)
                .HasForeignKey(bi => bi.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(bi => bi.DiagnosticService)
                .WithMany(s => s.BookingItems)
                .HasForeignKey(bi => bi.DiagnosticServiceId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
