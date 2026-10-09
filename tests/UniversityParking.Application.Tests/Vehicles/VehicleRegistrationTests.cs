using Microsoft.Extensions.Logging.Abstractions;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.Files;
using UniversityParking.Application.Vehicles;
using UniversityParking.Domain.AcademicPeriods;
using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Vehicles.ValueObjects;

namespace UniversityParking.Application.Tests.Vehicles;

public sealed partial class VehicleRegistrationTests
{
    private readonly Store store = new();
    private RegisterVehicleCommandHandler Handler => new(Context, store, store, store, store, store, store,
        new FileUploadValidator(), store, NullLogger<UploadedFileBatch>.Instance);
    private VehicleOperationContext Context => new(store, store, store, store, store, store, store, store);
    private static RegisterVehicleCommand Request(VehicleType type = VehicleType.MOTORCYCLE) => new(type,
        type == VehicleType.BICYCLE ? null : " abc-123 ", type == VehicleType.BICYCLE ? " frame 0001 " : null,
        "Brand", "Model", "Black", Source("image.png", "image/png", FileValidationTests.Png));
    private static UploadSource Source(string name, string mime, byte[] content) => new(name, mime, content.Length, () => new MemoryStream(content));
    [Theory]
    [InlineData(MemberType.STUDENT, VehicleType.MOTORCYCLE)]
    [InlineData(MemberType.STUDENT, VehicleType.BICYCLE)]
    [InlineData(MemberType.TEACHER, VehicleType.CAR)]
    [InlineData(MemberType.STAFF, VehicleType.CAR)]
    public async Task RegisterVehicle_AllowsCompatibleTypeAndCreatesOwnershipRegistrationAndMetadata(MemberType member, VehicleType type)
    {
        store.ChangeMember(member);
        var result = await Handler.Handle(Request(type), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(store.User.Id, Assert.Single(store.Ownerships).UserId);
        Assert.Equal(store.Period!.Id, Assert.Single(store.Registrations).AcademicPeriodId);
        Assert.Single(store.VerificationImages); Assert.Empty(store.Photos);
        Assert.Empty(store.Documents); Assert.Equal(VehicleVerificationImage.ForVehicle(type), store.VerificationImages.Single().Type);
        Assert.Equal(1, store.Commits);
        Assert.Single(store.Audits, x => x.Action == "VEHICLE_REGISTERED");
        Assert.Single(store.Audits, x => x.Action == "VEHICLE_REGISTRATION_CREATED");
    }
    [Fact]
    public async Task RegisterVehicle_RejectsStudentCarBeforeStorageOrSave()
    {
        var result = await Handler.Handle(Request(VehicleType.CAR), default);
        Assert.Equal("STUDENT_CANNOT_REGISTER_CAR", result.Error!.Code);
        Assert.Empty(store.Vehicles);
        Assert.Empty(store.Keys);
        Assert.Equal(0, store.Saves);
    }
    [Theory]
    [InlineData(VehicleType.MOTORCYCLE)]
    [InlineData(VehicleType.BICYCLE)]
    public async Task RegisterVehicle_RejectsNormalizedDuplicateIdentifier(VehicleType type)
    {
        store.Vehicles.Add(new Vehicle(type, type == VehicleType.BICYCLE ? null : new VehiclePlate("ABC123"),
            type == VehicleType.BICYCLE ? new FrameNumber("FRAME0001") : null, "Brand", "Model", "Color", store.UtcNow));
        Assert.Equal("VEHICLE_IDENTIFIER_ALREADY_EXISTS", (await Handler.Handle(Request(type), default)).Error!.Code);
        Assert.Empty(store.Keys);
    }
    [Fact]
    public async Task RegisterVehicle_RequiresActivePeriod()
    {
        store.Period = null;
        Assert.Equal("ACADEMIC_PERIOD_NOT_ACTIVE", (await Handler.Handle(Request(), default)).Error!.Code);
        Assert.Empty(store.Keys);
    }
    [Fact]
    public async Task RegisterVehicle_RequiresVerificationImage()
    {
        Assert.Equal("VALIDATION_ERROR", (await Handler.Handle(Request() with { VerificationImage = null }, default)).Error!.Code);
        Assert.Empty(store.Keys);
    }
    [Theory]
    [InlineData(VehicleType.MOTORCYCLE)]
    [InlineData(VehicleType.BICYCLE)]
    public async Task RegisterVehicle_RejectsPdfAsVerificationForType(VehicleType type)
    {
        Assert.Equal("FILE_TYPE_NOT_ALLOWED", (await Handler.Handle(Request(type) with { VerificationImage = Source("document.pdf", "application/pdf", "%PDF-1.7"u8.ToArray()) }, default)).Error!.Code);
        Assert.Empty(store.Keys);
    }
    [Fact]
    public async Task RegisterVehicle_CompensatesFilesOnDatabaseFailure()
    {
        store.FailSave = true;
        await Assert.ThrowsAsync<IOException>(() => Handler.Handle(Request(), default));
        Assert.Empty(store.Keys);
        Assert.Equal(0, store.Commits);
    }
    [Fact]
    public async Task RenewVehicle_RejectsDuplicateTriplet()
    {
        var id = (await Handler.Handle(Request(), default)).Value;
        var renewal = new RenewVehicleRegistrationCommandHandler(Context, store, store, store, store, new FileUploadValidator(), store, NullLogger<UploadedFileBatch>.Instance);
        Assert.Equal("VEHICLE_REGISTRATION_ALREADY_EXISTS", (await renewal.Handle(new(id, []), default)).Error!.Code);
        Assert.Single(store.Registrations);
    }
    [Fact]
    public async Task RenewVehicle_RejectsCancelledTriplet()
    {
        var id = (await Handler.Handle(Request(), default)).Value;
        store.Registrations.Single().Cancel(store.UtcNow, store.User.Id, "Transfer");
        var renewal = new RenewVehicleRegistrationCommandHandler(Context, store, store, store, store, new FileUploadValidator(), store, NullLogger<UploadedFileBatch>.Instance);
        Assert.Equal("VEHICLE_REGISTRATION_CANCELLED", (await renewal.Handle(new(id, []), default)).Error!.Code);
    }
    [Fact]
    public async Task RenewVehicle_UsesExistingVerificationForNewPeriod()
    {
        var id = (await Handler.Handle(Request(), default)).Value;
        store.Period!.Close();
        store.Period = new("2027-1", new(2027, 1, 1), new(2027, 6, 30), store.UtcNow);
        store.Period.Activate();
        var renewal = new RenewVehicleRegistrationCommandHandler(Context, store, store, store, store, new FileUploadValidator(), store, NullLogger<UploadedFileBatch>.Instance);
        Assert.True((await renewal.Handle(new(id, []), default)).IsSuccess);
        Assert.Equal(2, store.Registrations.Count);
        Assert.Single(store.Vehicles);
        Assert.Single(store.Ownerships);
        Assert.Single(store.Keys);
    }
    [Fact]
    public async Task UnauthenticatedActorCannotRegister()
    {
        store.IsAuthenticated = false;
        Assert.Equal("AUTH_INVALID_CREDENTIALS", (await Handler.Handle(Request(), default)).Error!.Code);
        Assert.Empty(store.Vehicles);
    }
    private RenewVehicleRegistrationCommandHandler Renewal => new(Context, store, store, store, store, new FileUploadValidator(), store, NullLogger<UploadedFileBatch>.Instance);
    [Theory]
    [InlineData("nonowner", "VEHICLE_NOT_FOUND")]
    [InlineData("user-inactive", "USER_INACTIVE")]
    [InlineData("vehicle-inactive", "VEHICLE_INACTIVE")]
    [InlineData("no-period", "ACADEMIC_PERIOD_NOT_ACTIVE")]
    public async Task RenewalChecksOwnershipUserVehicleAndPeriod(string condition, string code)
    {
        var id = (await Handler.Handle(Request(), default)).Value;
        if (condition == "nonowner") store.Ownerships.Single().Close(store.UtcNow, "Transferred");
        if (condition == "user-inactive") store.User.Deactivate(store.UtcNow);
        if (condition == "vehicle-inactive") store.Vehicles.Single().Deactivate(store.UtcNow);
        if (condition == "no-period") store.Period = null;
        Assert.Equal(code, (await Renewal.Handle(new(id, []), default)).Error!.Code);
        Assert.Single(store.Registrations);
    }
    private User NewOwner(MemberType member = UniversityParking.Domain.Users.MemberType.STUDENT)
    {
        var owner = new User(new("new-owner"), "New owner", UniversityParking.Domain.Universities.UniversityIds.Etitc, "Ingeniería", member, new("new-card"), store.UtcNow);
        store.OtherUsers.Add(owner);
        return owner;
    }
    private TransferVehicleCommandHandler Transfer => new(Context, store, store, store, store, store, store);
    [Fact]
    public async Task TransferClosesOldOwnershipCreatesNewAndCancelsCurrentRegistration()
    {
        var id = (await Handler.Handle(Request(), default)).Value;
        store.Roles = ["USER", "ADMIN"];
        var next = NewOwner();
        var result = await Transfer.Handle(new(id, next.IdentificationNumber.Value, "Transfer"), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, store.Ownerships.Count);
        Assert.Equal(store.Ownerships[0].EndAt, store.Ownerships[1].StartAt);
        Assert.Equal(next.Id, store.Ownerships[1].UserId);
        Assert.Equal(VehicleRegistrationStatus.CANCELLED, store.Registrations.Single().Status);
        Assert.Equal("OWNERSHIP_TRANSFERRED", store.Registrations.Single().CancelReason);
        var cancellationAudit = Assert.Single(store.Audits, x => x.Action == "VEHICLE_REGISTRATION_CANCELLED");
        Assert.Equal(id, cancellationAudit.EntityId);
        Assert.DoesNotContain("password", cancellationAudit.NewValues!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("VEHICLE_TRANSFERRED", store.Audits.Last().Action);
    }
    [Fact]
    public async Task TransferRequiresAdmin()
    {
        var id = (await Handler.Handle(Request(), default)).Value;
        Assert.Equal("FORBIDDEN", (await Transfer.Handle(new(id, NewOwner().IdentificationNumber.Value, "Transfer"), default)).Error!.Code);
        Assert.Null(store.Ownerships.Single().EndAt);
    }
    [Fact]
    public async Task TransferRejectsStudentAsNewCarOwner()
    {
        store.ChangeMember(UniversityParking.Domain.Users.MemberType.TEACHER);
        var id = (await Handler.Handle(Request(VehicleType.CAR), default)).Value;
        store.Roles = ["USER", "ADMIN"];
        Assert.Equal("STUDENT_CANNOT_REGISTER_CAR", (await Transfer.Handle(new(id, NewOwner().IdentificationNumber.Value, "Transfer"), default)).Error!.Code);
        Assert.Null(store.Ownerships.Single().EndAt);
    }
    [Fact]
    public async Task TransferRejectsVehicleInside()
    {
        var id = (await Handler.Handle(Request(), default)).Value;
        store.Roles = ["USER", "ADMIN"];
        store.VehicleInside = true;
        Assert.Equal("VEHICLE_HAS_OPEN_PARKING_MOVEMENT", (await Transfer.Handle(new(id, NewOwner().IdentificationNumber.Value, "Transfer"), default)).Error!.Code);
        Assert.Null(store.Ownerships.Single().EndAt);
    }
    internal sealed class Store : IUserRepository, IRoleRepository, IVehicleRepository, IVehicleOwnershipRepository,
        IVehicleRegistrationRepository, IVehicleEvidenceRepository, IAcademicPeriodRepository, IAuditLogRepository,
        IUnitOfWork, IFileStorage, ICurrentUser, IClock, IRequestContext, IParkingMovementRepository
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
        public User User { get; private set; }
        public AcademicPeriod? Period { get; set; }
        public List<Vehicle> Vehicles { get; } = [];
        public List<VehicleOwnership> Ownerships { get; } = [];
        public List<VehicleRegistration> Registrations { get; } = [];
        public List<VehiclePhoto> Photos { get; } = [];
        public List<VehicleVerificationImage> VerificationImages { get; } = [];
        public Task<VehicleVerificationImage?> GetVerificationImageAsync(Guid id, CancellationToken ct) => Task.FromResult(VerificationImages.SingleOrDefault(x => x.VehicleId == id));
        public Task AddVerificationImageAsync(VehicleVerificationImage image, CancellationToken ct) { VerificationImages.Add(image); return Task.CompletedTask; }
        public List<VehicleDocument> Documents { get; } = [];
        public List<AuditLog> Audits { get; } = [];
        public HashSet<string> Keys { get; } = [];
        public bool IsAuthenticated { get; set; } = true;
        public Guid? UserId => User.Id;
        public MemberType? MemberType => User.MemberType;
        public IReadOnlyCollection<string> Roles { get; set; } = ["USER"];
        public List<User> OtherUsers { get; } = [];
        public bool VehicleInside { get; set; }
        public List<UniversityParking.Domain.Parking.ParkingMovement> Movements { get; } = [];
        public bool IsInRole(string role) => Roles.Contains(role);
        public string? IpAddress => "127.0.0.1";
        public string? TraceId => "test-trace";
        public int Saves { get; private set; }
        public int Commits { get; private set; }
        public bool FailSave { get; set; }
        public Store()
        {
            User = new(new("id"), "Student", UniversityParking.Domain.Universities.UniversityIds.Etitc, "Ingeniería", Domain.Users.MemberType.STUDENT, new("card"), UtcNow);
            Period = new("2026-2", new(2026, 7, 1), new(2026, 12, 31), UtcNow);
            Period.Activate();
        }
        public void ChangeMember(MemberType member) => User.Update(User.FullName, User.UniversityId, "Ingeniería", member, User.CardCode, UtcNow);
        Task<User?> IUserRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(id == User.Id ? User : OtherUsers.FirstOrDefault(x => x.Id == id));
        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken ct) => ((IUserRepository)this).GetByIdAsync(id, ct);
        public Task<User?> GetByIdentificationNumberAsync(IdentificationNumber number, CancellationToken ct) => Task.FromResult(number == User.IdentificationNumber ? User : OtherUsers.FirstOrDefault(x => x.IdentificationNumber == number));
        public Task<User?> GetByCardCodeAsync(CardCode code, CancellationToken ct) => Task.FromResult(code == User.CardCode ? User : OtherUsers.FirstOrDefault(x => x.CardCode == code));
        public Task<bool> ExistsByIdentificationNumberAsync(IdentificationNumber number, CancellationToken ct) => Task.FromResult(number == User.IdentificationNumber);
        public Task<bool> ExistsByCardCodeAsync(CardCode code, CancellationToken ct) => Task.FromResult(code == User.CardCode);
        public Task AddAsync(User user, CancellationToken ct) { User = user; return Task.CompletedTask; }
        public Task<PagedResult<Application.Users.UserProfile>> SearchAsync(Application.Users.GetUsersQuery query, CancellationToken ct) =>
            Task.FromResult(new PagedResult<Application.Users.UserProfile>([], query.Page, query.PageSize, 0));
        public Task<IReadOnlyCollection<string>> GetCodesByUserIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Roles);
        public Task<Role?> GetByCodeAsync(string code, CancellationToken ct) => Task.FromResult<Role?>(new Role(code));
        public Task AssignAsync(UserRole assignment, CancellationToken ct) => Task.CompletedTask;
        public Task RemoveAsync(Guid userId, Guid roleId, CancellationToken ct) => Task.CompletedTask;
        Task<Vehicle?> IVehicleRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Vehicles.FirstOrDefault(x => x.Id == id));
        Task<Vehicle?> IVehicleRepository.GetByIdForUpdateAsync(Guid id, CancellationToken ct) => ((IVehicleRepository)this).GetByIdAsync(id, ct);
        public Task<Vehicle?> GetByPlateAsync(VehiclePlate plate, CancellationToken ct) => Task.FromResult(Vehicles.FirstOrDefault(x => x.Plate == plate));
        public Task<Vehicle?> GetByFrameNumberAsync(FrameNumber frame, CancellationToken ct) => Task.FromResult(Vehicles.FirstOrDefault(x => x.FrameNumber == frame));
        public Task<bool> ExistsByPlateAsync(VehiclePlate plate, CancellationToken ct) => Task.FromResult(Vehicles.Any(x => x.Plate == plate));
        public Task<bool> ExistsByFrameNumberAsync(FrameNumber frame, CancellationToken ct) => Task.FromResult(Vehicles.Any(x => x.FrameNumber == frame));
        public Task<bool> HasActiveCarOwnedByUserAsync(Guid id, CancellationToken ct) => Task.FromResult(false);
        public Task AddAsync(Vehicle vehicle, CancellationToken ct) { Vehicles.Add(vehicle); return Task.CompletedTask; }
        public Task<VehicleView?> GetViewAsync(Guid id, CancellationToken ct) => Task.FromResult<VehicleView?>(null);
        public Task<IReadOnlyList<VehicleView>> GetByCurrentOwnerAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<VehicleView>>(Vehicles
            .Where(v => Ownerships.Any(o => o.VehicleId == v.Id && o.UserId == id && o.EndAt == null))
            .Select(v => new VehicleView(v.Id, v.Type, v.Plate?.Value, v.FrameNumber?.Value, v.Brand, v.Model, v.Color, v.Status, id, null,
                Registrations.Any(r => r.VehicleId == v.Id && r.UserId == id && r.AcademicPeriodId == Period?.Id && r.Status == VehicleRegistrationStatus.ACTIVE) ? RegistrationState.ACTIVE : RegistrationState.NONE,
                Movements.Any(m => m.VehicleId == v.Id && m.Status == UniversityParking.Domain.Parking.ParkingMovementStatus.OPEN), null)).ToArray());
        public Task<PagedResult<VehicleView>> SearchAsync(GetVehiclesQuery query, CancellationToken ct) => Task.FromResult(new PagedResult<VehicleView>([], query.Page, query.PageSize, 0));
        public Task<VehicleOwnership?> GetCurrentByVehicleIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Ownerships.FirstOrDefault(x => x.VehicleId == id && x.EndAt == null));
        public Task<VehicleOwnership?> GetCurrentByVehicleAndUserAsync(Guid vehicleId, Guid userId, CancellationToken ct) => Task.FromResult(Ownerships.FirstOrDefault(x => x.VehicleId == vehicleId && x.UserId == userId && x.EndAt == null));
        public Task<IReadOnlyList<VehicleOwnership>> GetByUserIdAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<VehicleOwnership>>(Ownerships.Where(x => x.UserId == id).ToArray());
        public Task AddAsync(VehicleOwnership ownership, CancellationToken ct) { Ownerships.Add(ownership); return Task.CompletedTask; }
        public Task<VehicleRegistration?> GetByVehicleUserAndPeriodAsync(Guid vehicleId, Guid userId, Guid periodId, CancellationToken ct) =>
            Task.FromResult(Registrations.FirstOrDefault(x => x.VehicleId == vehicleId && x.UserId == userId && x.AcademicPeriodId == periodId));
        public async Task<VehicleRegistration?> GetActiveForVehicleUserAndPeriodAsync(Guid vehicleId, Guid userId, Guid periodId, CancellationToken ct) =>
            (await GetByVehicleUserAndPeriodAsync(vehicleId, userId, periodId, ct)) is { Status: VehicleRegistrationStatus.ACTIVE } value ? value : null;
        public async Task<bool> ExistsForVehicleUserAndPeriodAsync(Guid vehicleId, Guid userId, Guid periodId, CancellationToken ct) =>
            await GetByVehicleUserAndPeriodAsync(vehicleId, userId, periodId, ct) is not null;
        public Task AddAsync(VehicleRegistration registration, CancellationToken ct) { Registrations.Add(registration); return Task.CompletedTask; }
        public Task<IReadOnlyList<VehiclePhoto>> GetPhotosAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<VehiclePhoto>>(Photos.Where(x => x.VehicleId == id).ToArray());
        public Task<IReadOnlyList<VehicleDocument>> GetDocumentsAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<VehicleDocument>>(Documents.Where(x => x.VehicleId == id).ToArray());
        public Task<VehiclePhoto?> GetPhotoAsync(Guid id, CancellationToken ct) => Task.FromResult(Photos.FirstOrDefault(x => x.Id == id));
        public Task<VehicleDocument?> GetDocumentAsync(Guid id, CancellationToken ct) => Task.FromResult(Documents.FirstOrDefault(x => x.Id == id));
        public Task AddPhotoAsync(VehiclePhoto photo, CancellationToken ct) { Photos.Add(photo); return Task.CompletedTask; }
        public Task AddDocumentAsync(VehicleDocument document, CancellationToken ct) { Documents.Add(document); return Task.CompletedTask; }
        Task<AcademicPeriod?> IAcademicPeriodRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Period?.Id == id ? Period : null);
        public Task<AcademicPeriod?> GetActiveAsync(CancellationToken ct) => Task.FromResult(Period);
        Task<AcademicPeriod?> IAcademicPeriodRepository.GetByIdForUpdateAsync(Guid id, CancellationToken ct) => Task.FromResult(Period?.Id == id ? Period : null);
        public Task<IReadOnlyList<AcademicPeriod>> GetAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AcademicPeriod>>(Period is null ? [] : [Period]);
        public Task<bool> ExistsByNameAsync(string name, CancellationToken ct) => Task.FromResult(Period?.Name == name);
        public Task<AcademicPeriod?> GetActiveForShareAsync(CancellationToken ct) => Task.FromResult(Period);
        public Task<bool> ExistsActiveAsync(CancellationToken ct) => Task.FromResult(Period is not null);
        public Task AddAsync(AcademicPeriod period, CancellationToken ct) { Period = period; return Task.CompletedTask; }
        public Task AddAsync(AuditLog audit, CancellationToken ct) { Audits.Add(audit); return Task.CompletedTask; }
        public Task<int> SaveChangesAsync(CancellationToken ct) { Saves++; if (FailSave) throw new IOException("Database failure"); return Task.FromResult(1); }
        public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken ct) => Task.FromResult<IApplicationTransaction>(new Transaction(this));
        public Task<StoredFile> UploadAsync(FileUpload upload, CancellationToken ct) { Keys.Add(upload.StorageKey); return Task.FromResult(new StoredFile(upload.StorageKey, upload.ContentType, upload.SizeBytes)); }
        public Task DeleteAsync(string key, CancellationToken ct) { Keys.Remove(key); return Task.CompletedTask; }
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct) => Task.FromResult<Stream>(new MemoryStream());
        public Task<Uri?> GetReadUrlAsync(string key, CancellationToken ct) => Task.FromResult<Uri?>(null);
        public Task<UniversityParking.Domain.Parking.ParkingMovement?> GetOpenByVehicleIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Movements.FirstOrDefault(x => x.VehicleId == id && x.Status == UniversityParking.Domain.Parking.ParkingMovementStatus.OPEN));
        public Task<UniversityParking.Domain.Parking.ParkingMovement?> GetOpenByUserIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Movements.FirstOrDefault(x => x.UserId == id && x.Status == UniversityParking.Domain.Parking.ParkingMovementStatus.OPEN));
        Task<UniversityParking.Domain.Parking.ParkingMovement?> IParkingMovementRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Movements.FirstOrDefault(x => x.Id == id));
        public Task<bool> ExistsOpenByVehicleIdAsync(Guid id, CancellationToken ct) => Task.FromResult(VehicleInside || Movements.Any(x => x.VehicleId == id && x.Status == UniversityParking.Domain.Parking.ParkingMovementStatus.OPEN));
        public Task<bool> ExistsOpenByUserIdAsync(Guid id, CancellationToken ct) => Task.FromResult(VehicleInside || Movements.Any(x => x.UserId == id && x.Status == UniversityParking.Domain.Parking.ParkingMovementStatus.OPEN));
        public Task<bool> ExistsOpenByParkingLotIdAsync(Guid id, CancellationToken ct) => Task.FromResult(VehicleInside);
        public Task AddAsync(UniversityParking.Domain.Parking.ParkingMovement movement, CancellationToken ct) { Movements.Add(movement); return Task.CompletedTask; }
        private sealed class Transaction(Store store) : IApplicationTransaction
        {
            public Task CommitAsync(CancellationToken ct) { store.Commits++; return Task.CompletedTask; }
            public Task RollbackAsync(CancellationToken ct) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
