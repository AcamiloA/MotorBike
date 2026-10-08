using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.AcademicPeriods;
using UniversityParking.Domain.Common;
using UniversityParking.Domain.News;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Vehicles.ValueObjects;

namespace UniversityParking.Infrastructure.Persistence.Seeding;

public sealed class SeedOptions
{
    public bool Enabled { get; set; }
    public bool DemoEnabled { get; set; }
    public bool AllowDemoInProduction { get; set; }
    public string? DemoPassword { get; set; }

    public void Validate(bool production)
    {
        if (!Enabled || !DemoEnabled) return;
        if (production && !AllowDemoInProduction)
            throw new InvalidOperationException("Seed demo requiere Seed:AllowDemoInProduction explícito en Production.");
        var value = DemoPassword;
        if (string.IsNullOrWhiteSpace(value) || value.Length < 8 || !value.Any(char.IsUpper) ||
            !value.Any(char.IsLower) || !value.Any(char.IsDigit))
            throw new InvalidOperationException("Seed:DemoPassword requiere 8 caracteres, mayúscula, minúscula y número.");
    }
}

public static class DemoSeedData
{
    public static Guid Id(int number) => Guid.Parse("21de0000-0000-4000-8000-" + number.ToString("000000000000", CultureInfo.InvariantCulture));
    public static readonly (int Key, string Identification, string Card, MemberType Member, string Role)[] Accounts =
    [
        (1, "900000001", "DEMO-ADMIN", MemberType.STAFF, "ADMIN"),
        (2, "900000002", "DEMO-GUARD", MemberType.STAFF, "GUARD"),
        (3, "900000003", "DEMO-STUDENT", MemberType.STUDENT, "USER"),
        (4, "900000004", "DEMO-TEACHER", MemberType.TEACHER, "USER"),
        (5, "900000005", "DEMO-STAFF", MemberType.STAFF, "USER")
    ];
}

public sealed class DemoSeed(AppDbContext db, IPasswordHasher passwords, IFileStorage storage,
    IClock clock, IParkingTimeZone timeZone, ILogger<DemoSeed> logger)
{
    public async Task RunAsync(SeedOptions options, CancellationToken token = default)
    {
        if (!options.Enabled) return;
        // Validate before any write, even when called outside API startup.
        options.Validate(production: false);
        var uploaded = new List<string>();
        var commitAttempted = false;
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        try
        {
            // Serializes seeds across API replicas, without locking unrelated requests globally.
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(210026)", token);
            var now = clock.UtcNow;
            var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,
                TimeZoneInfo.FindSystemTimeZoneById("America/Bogota")).DateTime);
            var start = timeZone.GetUtcStartOfDay(new DateOnly(2026, 7, 1));
            var created = now < start ? now : start;
            foreach (var code in new[] { "USER", "GUARD", "ADMIN" })
                if (!await db.Roles.AnyAsync(x => x.Code == code, token)) db.Roles.Add(new Role(code));

            var period = await db.AcademicPeriods.SingleOrDefaultAsync(x => x.Name == "2026-2", token);
            if (period is null)
            {
                period = Add(10, new AcademicPeriod("2026-2", new(2026, 7, 1), new(2026, 12, 31), created));
                if (localToday >= period.StartsOn && localToday <= period.EndsOn &&
                    !await db.AcademicPeriods.AnyAsync(x => x.Status == AcademicPeriodStatus.ACTIVE, token)) period.Activate();
            }
            var lot = await FindOrAddAsync(20, () => new ParkingLot("Parqueadero Demo", "ETITC Demo", new(6, 0), new(22, 0), created), token);
            var zones = new Dictionary<VehicleType, ParkingZone>();
            foreach (var type in Enum.GetValues<VehicleType>())
                zones[type] = await FindOrAddAsync(21 + (int)type, () => new ParkingZone(lot.Id,
                    type == VehicleType.CAR ? "Carros" : type == VehicleType.MOTORCYCLE ? "Motos" : "Bicicletas", type, created), token);
            await db.SaveChangesAsync(token);

            if (options.DemoEnabled)
            {
                var roles = await db.Roles.ToDictionaryAsync(x => x.Code, token);
                foreach (var account in DemoSeedData.Accounts)
                {
                    // Existing reserved entities retain passwords, roles and profile edits.
                    if (await db.Users.FindAsync([DemoSeedData.Id(account.Key)], token) is not null) continue;
                    var user = Add(account.Key, new User(new IdentificationNumber(account.Identification),
                        "Demo " + account.Card[5..], "ETITC", account.Member == MemberType.STUDENT ? "Ingeniería de Sistemas" : null,
                        account.Member, new CardCode(account.Card), created));
                    db.UserCredentials.Add(new(user.Id, passwords.Hash(options.DemoPassword!), created));
                    foreach (var code in new[] { "USER", account.Role }.Distinct()) db.UserRoles.Add(new(user.Id, roles[code].Id));
                }
                await db.SaveChangesAsync(token);
                var admin = await db.Users.FindAsync([DemoSeedData.Id(1)], token) ?? throw new InvalidOperationException("Administrador demo ausente.");
                var guard = await db.Users.FindAsync([DemoSeedData.Id(2)], token) ?? throw new InvalidOperationException("Celador demo ausente.");
                var student = await db.Users.FindAsync([DemoSeedData.Id(3)], token) ?? throw new InvalidOperationException("Estudiante demo ausente.");
                var teacher = await db.Users.FindAsync([DemoSeedData.Id(4)], token) ?? throw new InvalidOperationException("Docente demo ausente.");
                foreach (var item in new[] { (Key: 30, Type: VehicleType.MOTORCYCLE, Owner: student, Identifier: "DEM21M"),
                    (Key: 31, Type: VehicleType.BICYCLE, Owner: student, Identifier: "DEMO-BICI-2026"),
                    (Key: 32, Type: VehicleType.CAR, Owner: teacher, Identifier: "DEM021") })
                {
                    if (await db.Vehicles.FindAsync([DemoSeedData.Id(item.Key)], token) is not null) continue;
                    if (item.Owner.Status != UserStatus.ACTIVE || (item.Type == VehicleType.CAR && item.Owner.MemberType == MemberType.STUDENT))
                        throw new InvalidOperationException("El propietario demo actual no permite crear ese vehículo; se conserva su perfil.");
                    var vehicle = Add(item.Key, new Vehicle(item.Type,
                        item.Type == VehicleType.BICYCLE ? null : new VehiclePlate(item.Identifier),
                        item.Type == VehicleType.BICYCLE ? new FrameNumber(item.Identifier) : null,
                        "Demo", "Modelo de demostración", "Negro", created));
                    db.VehicleOwnerships.Add(new(vehicle.Id, item.Owner.Id, created, admin.Id));
                    if (period.Status == AcademicPeriodStatus.ACTIVE && localToday >= period.StartsOn && localToday <= period.EndsOn)
                        db.VehicleRegistrations.Add(new(vehicle.Id, item.Owner.Id, period.Id, created));
                    var photo = DemoSeedAssets.Photo;
                    var photoKey = await UploadAsync(photo, "image/png", uploaded, token);
                    db.VehiclePhotos.Add(new(vehicle.Id, VehiclePhotoType.GENERAL, photoKey, "imagen-demo.png", "image/png", photo.Length, created));
                    foreach (var type in item.Type == VehicleType.BICYCLE ? new[] { VehicleDocumentType.OWNERSHIP_SUPPORT } :
                        new[] { VehicleDocumentType.VEHICLE_REGISTRATION, VehicleDocumentType.INSURANCE })
                    {
                        var pdf = DemoSeedAssets.Document;
                        var key = await UploadAsync(pdf, "application/pdf", uploaded, token);
                        db.VehicleDocuments.Add(new(vehicle.Id, type, key, "soporte-demo.pdf", "application/pdf", pdf.Length, created,
                            "DEMO-SIN-VALIDEZ", period.StartsOn, period.EndsOn));
                    }
                }
                await db.SaveChangesAsync(token);
                var historyDay = localToday.AddDays(-1);
                var historyEntry = timeZone.GetUtcStartOfDay(historyDay).AddHours(8);
                var motorcycle = await db.Vehicles.FindAsync([DemoSeedData.Id(30)], token);
                var guardCanAct = guard.Status == UserStatus.ACTIVE && await db.UserRoles.AnyAsync(x => x.UserId == guard.Id && x.RoleId == roles["GUARD"].Id, token);
                if (motorcycle is not null && motorcycle.Status == VehicleStatus.ACTIVE && student.Status == UserStatus.ACTIVE &&
                    guardCanAct && lot.Status == ParkingLotStatus.ACTIVE && zones[VehicleType.MOTORCYCLE].Status == ParkingZoneStatus.ACTIVE &&
                    period.Status == AcademicPeriodStatus.ACTIVE && historyDay >= period.StartsOn && historyDay <= period.EndsOn &&
                    motorcycle.CreatedAt <= historyEntry && student.CreatedAt <= historyEntry && guard.CreatedAt <= historyEntry &&
                    lot.CreatedAt <= historyEntry && zones[VehicleType.MOTORCYCLE].CreatedAt <= historyEntry &&
                    await db.VehicleOwnerships.AnyAsync(x => x.VehicleId == motorcycle.Id && x.UserId == student.Id && x.StartAt <= historyEntry && x.EndAt == null, token) &&
                    await db.VehicleRegistrations.AnyAsync(x => x.VehicleId == motorcycle.Id && x.UserId == student.Id && x.AcademicPeriodId == period.Id && x.RegisteredAt <= historyEntry && x.Status == VehicleRegistrationStatus.ACTIVE, token) &&
                    !await db.ParkingMovements.AnyAsync(x => x.VehicleId == motorcycle.Id, token))
                {
                    var entry = historyEntry;
                    if (entry >= created && lot.AllowsEntryAt(new TimeOnly(8, 0)))
                    {
                        var movement = Add(40, new ParkingMovement(student.Id, motorcycle.Id, lot.Id, zones[VehicleType.MOTORCYCLE].Id, entry, guard.Id));
                        movement.Close(entry.AddHours(2), guard.Id);
                    }
                }
                if (await db.NewsItems.FindAsync([DemoSeedData.Id(50)], token) is null && admin.Status == UserStatus.ACTIVE &&
                    await db.UserRoles.AnyAsync(x => x.UserId == admin.Id && x.RoleId == roles["ADMIN"].Id, token))
                {
                    var news = Add(50, new NewsItem("Bienvenidos a MOTOBIKE PARK — DEMO",
                        "Datos ficticios para practicar los flujos de usuario, portería y administración. Los soportes demo no tienen validez legal.", admin.Id, created));
                    news.Publish(now);
                }
                await db.SaveChangesAsync(token);
            }
            commitAttempted = true;
            await transaction.CommitAsync(token);
            logger.LogInformation("Seed completado. Datos demo habilitados: {DemoEnabled}.", options.DemoEnabled);
        }
        catch
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch (Exception error) { logger.LogWarning("No se pudo confirmar el rollback del seed. ExceptionType {ExceptionType}", error.GetType().Name); }
            // A lost commit acknowledgment can mean the transaction already committed.
            // Preserve assets in that case; deleting them could break committed references.
            if (commitAttempted)
                logger.LogError("Confirmación de commit del seed incierta; se conservan archivos para verificar PostgreSQL y storage.");
            else
                foreach (var key in uploaded)
                    try { await storage.DeleteAsync(key, CancellationToken.None); }
                    catch (Exception error) { logger.LogWarning("No se pudo limpiar un archivo temporal del seed. ExceptionType {ExceptionType}", error.GetType().Name); }
            throw;
        }
    }

    private T Add<T>(int key, T value) where T : Entity
    {
        db.Entry(value).Property(x => x.Id).CurrentValue = DemoSeedData.Id(key);
        db.Add(value);
        return value;
    }
    private async Task<T> FindOrAddAsync<T>(int key, Func<T> create, CancellationToken token) where T : Entity =>
        await db.Set<T>().FindAsync([DemoSeedData.Id(key)], token) ?? Add(key, create());
    private async Task<string> UploadAsync(byte[] content, string contentType, List<string> uploaded, CancellationToken token)
    {
        var key = "seed/demo/" + Guid.NewGuid().ToString("N");
        await using var stream = new MemoryStream(content, writable: false);
        await storage.UploadAsync(new(key, stream, contentType, content.Length), token);
        uploaded.Add(key);
        return key;
    }
}
