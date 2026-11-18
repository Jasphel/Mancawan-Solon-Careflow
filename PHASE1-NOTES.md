# CareFlow - Phase 1 (Project structure and entities)

Requires .NET 8 SDK. Open `CareFlow.sln` in Visual Studio / Rider, or run `dotnet run --project CareFlow`.

## Entities (Models/)
| Entity | Key fields | Relationships |
|---|---|---|
| Patient | Id (PK), FirstName, LastName, BirthDate, ContactNumber | 1 - * Booking |
| DiagnosticService | Id (PK), Name (unique), DailyCapacity | 1 - * BookingItem |
| Booking | Id (PK), PatientId (FK), BookingDate, CreatedAtUtc | * - 1 Patient, 1 - * BookingItem |
| BookingItem | Id (PK), BookingId (FK), DiagnosticServiceId (FK) | * - 1 Booking, * - 1 DiagnosticService |

Remaining capacity = DailyCapacity - COUNT(BookingItem for that service on that date), always read from the database.

## Left for Phase 2
- `Data/CareFlowDbContext` (Fluent API: unique indexes on Service.Name and (BookingId, DiagnosticServiceId), max lengths, FKs with Restrict delete)
- PostgreSQL connection string (not committed with credentials), DI container in App.axaml.cs
