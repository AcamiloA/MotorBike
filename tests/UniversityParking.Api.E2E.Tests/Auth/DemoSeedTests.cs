using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Contracts.Auth;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Parking;
using UniversityParking.Contracts.Users;
using UniversityParking.Contracts.Vehicles;
using UniversityParking.Domain.AcademicPeriods;
using UniversityParking.Domain.News;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Infrastructure.Authentication;
using UniversityParking.Infrastructure.Persistence;
using UniversityParking.Infrastructure.Persistence.Seeding;
using UniversityParking.Infrastructure.Time;

namespace UniversityParking.Api.E2E.Tests.Auth;

[Collection("Authentication API")]
public sealed class DemoSeedTests(AuthApiFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program> factory = null!;
    private readonly SeedClock clock = new();
    private readonly List<HttpClient> clients = [];
    private static SeedOptions Demo => new() { Enabled = true, DemoEnabled = true, DemoPassword = AuthApiFixture.Password };
    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        factory = fixture.CreateFactory(services =>
        {
            services.AddSingleton<IClock>(clock);
            services.AddScoped<ITokenService>(provider => new JwtTokenService(provider.GetRequiredService<IOptions<JwtOptions>>(), new SystemClock()));
        });
    }
    public async Task DisposeAsync()
    {
        foreach (var client in clients) client.Dispose();
        await factory.DisposeAsync();
    }
    private async Task SeedAsync(SeedOptions? options = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DemoSeed>().RunAsync(options ?? Demo);
    }
    private async Task<HttpClient> LoginAsync(string identification, string password = AuthApiFixture.Password)
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        clients.Add(client);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(identification, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken);
        return client;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CatalogReferenceDataIsIndependentOfSeedAndRemainsStable(bool enabled, bool demo)
    {
        await using var before = fixture.CreateContext();
        var original = await before.Universities.OrderBy(x => x.Code)
            .Select(x => new { x.Id, x.Code, x.Name, x.IsActive, x.CreatedAt, x.UpdatedAt }).ToArrayAsync();
        Assert.Equal(3, original.Length);
        await SeedAsync(new() { Enabled = enabled, DemoEnabled = demo, DemoPassword = AuthApiFixture.Password });
        await SeedAsync(new() { Enabled = enabled, DemoEnabled = demo, DemoPassword = AuthApiFixture.Password });
        await using var after = fixture.CreateContext();
        Assert.Equal(original, await after.Universities.OrderBy(x => x.Code)
            .Select(x => new { x.Id, x.Code, x.Name, x.IsActive, x.CreatedAt, x.UpdatedAt }).ToArrayAsync());
        var references = await after.Users.Select(x => x.UniversityId).Distinct().ToArrayAsync();
        Assert.All(references, id => Assert.Contains(original, u => u.Id == id));
        if (demo) Assert.Equal(UniversityParking.Domain.Universities.UniversityIds.Etitc, Assert.Single(references));
    }
    [Fact]
    public async Task DisabledSeed_WritesNothing()
    {
        await SeedAsync(new());
        await using var db = fixture.CreateContext();
        Assert.Empty(await db.Roles.ToListAsync());
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.AcademicPeriods.ToListAsync());
    }
    [Fact]
    public async Task BaseSeed_ProducesRolesPeriodLotAndThreeZones_WithoutCredentials()
    {
        await SeedAsync(new() { Enabled = true });
        await SeedAsync(new() { Enabled = true });
        await using var db = fixture.CreateContext();
        Assert.Equal(3, await db.Roles.CountAsync());
        Assert.Equal(AcademicPeriodStatus.ACTIVE, (await db.AcademicPeriods.SingleAsync()).Status);
        Assert.Single(await db.ParkingLots.ToListAsync());
        Assert.Equal(3, await db.ParkingZones.CountAsync());
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.UserCredentials.ToListAsync());
    }
    [Fact]
    public async Task DemoSeed_ThreeExecutions_PreserveIdsHashesAndPrivateFiles()
    {
        await SeedAsync();
        await using var first = fixture.CreateContext();
        var hashes = await first.UserCredentials.OrderBy(x => x.UserId).Select(x => x.PasswordHash).ToArrayAsync();
        var keys = await first.VehicleDocuments.Select(x => x.StorageKey).ToArrayAsync();
        await SeedAsync();
        await SeedAsync();
        await using var db = fixture.CreateContext();
        Assert.Equal(5, await db.Users.CountAsync());
        Assert.Equal(5, await db.UserCredentials.CountAsync());
        Assert.Equal(7, await db.UserRoles.CountAsync());
        Assert.Equal(3, await db.Vehicles.CountAsync());
        Assert.Equal(3, await db.VehicleOwnerships.CountAsync());
        Assert.Equal(3, await db.VehicleRegistrations.CountAsync());
        Assert.Equal(3, await db.VehiclePhotos.CountAsync());
        Assert.Equal(5, await db.VehicleDocuments.CountAsync());
        Assert.Equal(hashes, await db.UserCredentials.OrderBy(x => x.UserId).Select(x => x.PasswordHash).ToArrayAsync());
        Assert.Equal(keys.Order(), (await db.VehicleDocuments.Select(x => x.StorageKey).ToArrayAsync()).Order());
        Assert.Equal(ParkingMovementStatus.CLOSED, (await db.ParkingMovements.SingleAsync()).Status);
        Assert.Equal(NewsStatus.PUBLISHED, (await db.NewsItems.SingleAsync()).Status);
        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        foreach (var key in keys)
        {
            await using var document = await storage.OpenReadAsync(key, default);
            var bytes = new byte[5];
            await document.ReadExactlyAsync(bytes);
            Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes));
        }
    }
    [Fact]
    public async Task AllActors_LoginAndUseTheirFlows_WithoutImplicitGuardPrivileges()
    {
        await SeedAsync();
        var actors = new Dictionary<int, HttpClient>();
        foreach (var account in DemoSeedData.Accounts)
        {
            var client = await LoginAsync(account.Identification);
            actors[account.Key] = client;
            var me = (await client.GetFromJsonAsync<UserProfileResponse>("/api/v1/users/me"))!;
            Assert.Equal(DemoSeedData.Id(account.Key), me.Id);
            Assert.Equal(account.Member.ToString(), me.MemberType);
            Assert.Contains("USER", me.Roles);
            Assert.Contains(account.Role, me.Roles);
            var vehicles = (await client.GetFromJsonAsync<VehicleResponse[]>("/api/v1/vehicles/me"))!;
            Assert.Equal(account.Key == 3 ? 2 : account.Key == 4 ? 1 : 0, vehicles.Length);
            if (account.Key == 3) Assert.DoesNotContain(vehicles, x => x.Type == "CAR");
            if (account.Role is not ("GUARD" or "ADMIN"))
                Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/parking/access/lookup", new ParkingAccessRequest(CardCode: "DEMO-STUDENT"))).StatusCode);
            if (account.Role != "GUARD")
                Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/parking/check-in",
                    new CheckInVehicleRequest(DemoSeedData.Id(3), DemoSeedData.Id(30), DemoSeedData.Id(20)))).StatusCode);
        }
        var student = actors[3];
        var detail = (await student.GetFromJsonAsync<VehicleDetailResponse>($"/api/v1/vehicles/{DemoSeedData.Id(30)}"))!;
        Assert.Single(detail.Photos);
        Assert.Equal(2, detail.Documents.Count);
        var documentResponse = await student.GetAsync(detail.Documents[0].ContentUrl);
        Assert.Equal(HttpStatusCode.OK, documentResponse.StatusCode);
        Assert.StartsWith("%PDF-", await documentResponse.Content.ReadAsStringAsync());
        var staff = actors[5];
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync(detail.Documents[0].ContentUrl)).StatusCode);
        var before = (await student.GetFromJsonAsync<PagedResponse<ParkingMovementResponse>>("/api/v1/parking/history/me"))!;
        Assert.Equal("CLOSED", Assert.Single(before.Items).Status);
        var guard = actors[2];
        var access = await guard.PostAsJsonAsync("/api/v1/parking/access/lookup", new ParkingAccessRequest(CardCode: "DEMO-STUDENT"));
        Assert.Equal(2, (await access.Content.ReadFromJsonAsync<ParkingAccessResponse>())!.EligibleVehicles.Count);
        var entry = await guard.PostAsJsonAsync("/api/v1/parking/check-in", new CheckInVehicleRequest(DemoSeedData.Id(3), DemoSeedData.Id(30), DemoSeedData.Id(20)));
        Assert.Equal(HttpStatusCode.Created, entry.StatusCode);
        Assert.Single((await guard.GetFromJsonAsync<VehiclesInsideResponse>("/api/v1/parking/inside"))!.Items);
        clock.UtcNow = clock.UtcNow.AddHours(1);
        var exit = await guard.PostAsJsonAsync("/api/v1/parking/check-out", new CheckOutVehicleRequest(DemoSeedData.Id(30)));
        Assert.Equal(HttpStatusCode.OK, exit.StatusCode);
        var movement = (await exit.Content.ReadFromJsonAsync<ParkingMovementResponse>())!;
        Assert.Equal("CLOSED", movement.Status);
        Assert.Equal(TimeSpan.FromHours(1), movement.Duration);
        var admin = actors[1];
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/dashboard/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync("/api/v1/news")).StatusCode);
    }
    [Fact]
    public async Task Rerun_DoesNotResetChangedPasswordRoleStatusOrTransfer()
    {
        await SeedAsync();
        await using (var db = fixture.CreateContext())
        {
            var credential = await db.UserCredentials.SingleAsync(x => x.UserId == DemoSeedData.Id(3));
            using var scope = factory.Services.CreateScope();
            credential.ChangePasswordHash(scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash("ChangedDemoPassword2"), clock.UtcNow);
            var edited = await db.Users.SingleAsync(x => x.Id == DemoSeedData.Id(3));
            edited.Update("Nombre conservado", new Guid("a1100000-0000-4000-8000-000000000002"),
                "Carrera conservada", edited.MemberType, edited.CardCode, clock.UtcNow);
            (await db.Vehicles.FindAsync(DemoSeedData.Id(30)))!.Deactivate(clock.UtcNow);
            var guardRole = await db.Roles.SingleAsync(x => x.Code == "GUARD");
            db.UserRoles.Remove(await db.UserRoles.SingleAsync(x => x.UserId == DemoSeedData.Id(2) && x.RoleId == guardRole.Id));
            (await db.VehicleOwnerships.SingleAsync(x => x.VehicleId == DemoSeedData.Id(31) && x.EndAt == null)).Close(clock.UtcNow, "Transferencia de prueba");
            db.VehicleOwnerships.Add(new(DemoSeedData.Id(31), DemoSeedData.Id(4), clock.UtcNow, DemoSeedData.Id(1), "Transferencia de prueba"));
            (await db.VehicleRegistrations.SingleAsync(x => x.VehicleId == DemoSeedData.Id(31))).Cancel(clock.UtcNow, DemoSeedData.Id(1), "Transferencia de prueba");
            await db.SaveChangesAsync();
        }
        await SeedAsync();
        await using var after = fixture.CreateContext();
        var preserved = await after.Users.SingleAsync(x => x.Id == DemoSeedData.Id(3));
        Assert.Equal("Nombre conservado", preserved.FullName);
        Assert.Equal("Carrera conservada", preserved.Career);
        Assert.Equal(new Guid("a1100000-0000-4000-8000-000000000002"), preserved.UniversityId);
        Assert.Equal(VehicleStatus.INACTIVE, (await after.Vehicles.FindAsync(DemoSeedData.Id(30)))!.Status);
        Assert.Equal(6, await after.UserRoles.CountAsync());
        Assert.Equal(DemoSeedData.Id(4), (await after.VehicleOwnerships.SingleAsync(x => x.VehicleId == DemoSeedData.Id(31) && x.EndAt == null)).UserId);
        Assert.Equal(VehicleRegistrationStatus.CANCELLED, (await after.VehicleRegistrations.SingleAsync(x => x.VehicleId == DemoSeedData.Id(31))).Status);
        using var student = await LoginAsync("900000003", "ChangedDemoPassword2");
    }
    [Fact]
    public async Task ExistingActivePeriod_IsNotClosedOrReplaced()
    {
        await using (var db = fixture.CreateContext())
        {
            var current = new AcademicPeriod("Existente", new(2026, 1, 1), new(2026, 12, 31), clock.UtcNow.AddDays(-30));
            current.Activate();
            db.Add(current);
            await db.SaveChangesAsync();
        }
        await SeedAsync();
        await using var after = fixture.CreateContext();
        Assert.Equal("Existente", (await after.AcademicPeriods.SingleAsync(x => x.Status == AcademicPeriodStatus.ACTIVE)).Name);
        Assert.Equal(AcademicPeriodStatus.PLANNED, (await after.AcademicPeriods.SingleAsync(x => x.Name == "2026-2")).Status);
        Assert.Empty(await after.VehicleRegistrations.ToListAsync());
        Assert.Empty(await after.ParkingMovements.ToListAsync());
    }
    [Fact]
    public async Task Outside2026Period_DoesNotCreateActiveRegistrationsOrHistory()
    {
        clock.UtcNow = new DateTimeOffset(2027, 1, 15, 15, 0, 0, TimeSpan.Zero);
        await SeedAsync();
        await using var db = fixture.CreateContext();
        Assert.Equal(AcademicPeriodStatus.PLANNED, (await db.AcademicPeriods.SingleAsync()).Status);
        Assert.Empty(await db.VehicleRegistrations.ToListAsync());
        Assert.Empty(await db.ParkingMovements.ToListAsync());
    }
    [Theory]
    [InlineData(null)]
    [InlineData("short")]
    [InlineData("lowercaseonly1")]
    public async Task MissingOrWeakPassword_RejectsBeforeWriting(string? password)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => SeedAsync(new() { Enabled = true, DemoEnabled = true, DemoPassword = password }));
        await using var db = fixture.CreateContext();
        Assert.Empty(await db.Roles.ToListAsync());
        Assert.Empty(await db.Users.ToListAsync());
    }
    [Fact]
    public void ProductionDemo_RequiresExplicitOptIn()
    {
        Assert.Throws<InvalidOperationException>(() => Demo.Validate(production: true));
        var allowed = Demo;
        allowed.AllowDemoInProduction = true;
        allowed.Validate(production: true);
    }
    [Fact]
    public async Task ReservedIdentificationCollision_RollsBackWithoutTakingOverAccount()
    {
        await using (var db = fixture.CreateContext())
        {
            db.Users.Add(new User(new IdentificationNumber("900000001"), "Cuenta existente", UniversityParking.Domain.Universities.UniversityIds.Etitc, null,
                MemberType.STAFF, new CardCode("EXISTENTE"), clock.UtcNow.AddDays(-10)));
            await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<DbUpdateException>(() => SeedAsync());
        await using var after = fixture.CreateContext();
        Assert.Equal("Cuenta existente", (await after.Users.SingleAsync()).FullName);
        Assert.Empty(await after.UserCredentials.ToListAsync());
        Assert.Empty(await after.UserRoles.ToListAsync());
        Assert.Empty(await after.Roles.ToListAsync());
    }
    [Fact]
    public async Task ConcurrentSeeds_AreSerializedWithoutDuplicateAccountsOrAssets()
    {
        await Task.WhenAll(SeedAsync(), SeedAsync());
        await using var db = fixture.CreateContext();
        Assert.Equal(5, await db.Users.CountAsync());
        Assert.Equal(3, await db.Vehicles.CountAsync());
        Assert.Equal(5, await db.VehicleDocuments.CountAsync());
        Assert.Single(await db.ParkingMovements.ToListAsync());
    }
    [Fact]
    public async Task StartupConfiguration_RunsSeedBeforeServingHealth()
    {
        await using var startup = fixture.CreateFactory(services => services.AddSingleton<IClock>(clock),
            new Dictionary<string, string?>
            {
                ["Seed:Enabled"] = "true", ["Seed:DemoEnabled"] = "true",
                ["Seed:AllowDemoInProduction"] = "true", ["Seed:DemoPassword"] = AuthApiFixture.Password
            });
        using var client = startup.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        await using var db = fixture.CreateContext();
        Assert.Equal(5, await db.Users.CountAsync());
        Assert.Equal(3, await db.Vehicles.CountAsync());
    }
    [Fact]
    public async Task StorageFailure_RollsBackDatabaseAndRemovesUploadedAssets()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = new FailSecondUpload(scope.ServiceProvider.GetRequiredService<IFileStorage>());
        var seed = new DemoSeed(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>(), storage,
            clock, scope.ServiceProvider.GetRequiredService<IParkingTimeZone>(),
            scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DemoSeed>>());
        await Assert.ThrowsAsync<IOException>(() => seed.RunAsync(Demo));
        await using var after = fixture.CreateContext();
        Assert.Empty(await after.Users.ToListAsync());
        Assert.Empty(await after.Roles.ToListAsync());
        Assert.Empty(await after.Vehicles.ToListAsync());
        Assert.NotNull(storage.FirstKey);
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await using var stream = await storage.OpenReadAsync(storage.FirstKey!, default);
        });
    }
    private sealed class FailSecondUpload(IFileStorage inner) : IFileStorage
    {
        public string? FirstKey { get; private set; }
        public Task<StoredFile> UploadAsync(FileUpload upload, CancellationToken token)
        {
            if (FirstKey is not null) throw new IOException("Fallo de storage simulado.");
            FirstKey = upload.StorageKey;
            return inner.UploadAsync(upload, token);
        }
        public Task<Stream> OpenReadAsync(string key, CancellationToken token) => inner.OpenReadAsync(key, token);
        public Task DeleteAsync(string key, CancellationToken token) => inner.DeleteAsync(key, token);
        public Task<Uri?> GetReadUrlAsync(string key, CancellationToken token) => inner.GetReadUrlAsync(key, token);
    }
    private sealed class SeedClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 7, 15, 0, 0, TimeSpan.Zero);
    }
}
