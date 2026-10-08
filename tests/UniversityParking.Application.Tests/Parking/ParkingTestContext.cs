using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Authorization;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.Parking;
using UniversityParking.Application.ParkingLots;
using UniversityParking.Application.Tests.Auth;
using UniversityParking.Application.Tests.Vehicles;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;
using UniversityParking.Domain.Vehicles;
using UniversityParking.Domain.Vehicles.ValueObjects;

namespace UniversityParking.Application.Tests.Parking;

internal sealed class ParkingTestContext : IParkingLotRepository, IParkingMovementReadRepository, IParkingTimeZone
{
    public VehicleRegistrationTests.Store Store { get; } = new();
    public FakeClock Clock { get; } = new() { UtcNow = new(2026, 10, 7, 11, 0, 0, TimeSpan.Zero) };
    public User Target { get; }
    public Vehicle Vehicle { get; }
    public ParkingLot Lot { get; }
    public List<ParkingZone> Zones { get; } = [];
    public ParkingMovementFilter? LastFilter { get; private set; }
    public AdministrationOperationContext Operation { get; }
    public CheckInVehicleCommandHandler CheckIn { get; }
    public CheckOutVehicleCommandHandler CheckOut { get; }
    public CheckInVehicleCommand Request => new(Target.Id, Vehicle.Id, Lot.Id);
    public ParkingTestContext(VehicleType type = VehicleType.MOTORCYCLE)
    {
        Store.Roles = ["USER", "GUARD"];
        Store.ChangeMember(MemberType.STAFF);
        var before = Clock.UtcNow.AddDays(-1);
        Target = new User(new IdentificationNumber("target-id"), "Usuario", UniversityParking.Domain.Universities.UniversityIds.Etitc, "Ingeniería",
            type == VehicleType.CAR ? MemberType.TEACHER : MemberType.STUDENT, new CardCode("target-card"), before);
        Store.OtherUsers.Add(Target);
        Vehicle = new Vehicle(type, type == VehicleType.BICYCLE ? null : new VehiclePlate("ABC123"),
            type == VehicleType.BICYCLE ? new FrameNumber("FRAME123") : null, "Brand", "Model", "Black", before);
        Store.Vehicles.Add(Vehicle);
        Store.Ownerships.Add(new VehicleOwnership(Vehicle.Id, Target.Id, before, Store.User.Id));
        Store.Registrations.Add(new VehicleRegistration(Vehicle.Id, Target.Id, Store.Period!.Id, before));
        Lot = new ParkingLot("Principal", "Kennedy", new(6, 0), new(22, 0), before);
        foreach (var kind in Enum.GetValues<VehicleType>()) Zones.Add(new(Lot.Id, kind.ToString(), kind, before));
        Operation = new(Store, Store, Store, Store, Clock, Store);
        CheckIn = new(Operation, Store, Store, Store, Store, Store, Store, this, Store, this, this, Clock, Store);
        CheckOut = new(Operation, Store, Store, Store, Store, this, Clock, Store);
    }
    public Task<ParkingLot?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(id == Lot.Id ? Lot : null);
    public Task<ParkingLot?> GetByIdForUpdateAsync(Guid id, CancellationToken ct) => GetByIdAsync(id, ct);
    public Task<IReadOnlyList<ParkingLot>> GetActiveAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ParkingLot>>([Lot]);
    public Task<ParkingZone?> GetZoneForVehicleTypeAsync(Guid id, VehicleType type, CancellationToken ct) => Task.FromResult(Zones.FirstOrDefault(x => x.ParkingLotId == id && x.VehicleType == type));
    public Task<bool> ExistsByNameAndCampusAsync(string name, string campus, Guid? excluded, CancellationToken ct) => Task.FromResult(false);
    public Task AddAsync(ParkingLot lot, CancellationToken ct) => Task.CompletedTask;
    public Task AddZoneAsync(ParkingZone zone, CancellationToken ct) => Task.CompletedTask;
    public Task<PagedResult<ParkingLotView>> SearchAsync(ParkingLotStatus? status, PageRequest page, CancellationToken ct) => Task.FromResult(new PagedResult<ParkingLotView>([], page.Page, page.PageSize, 0));
    public TimeOnly GetLocalTime(DateTimeOffset utc) => TimeOnly.FromDateTime(utc.ToOffset(TimeSpan.FromHours(-5)).DateTime);
    public DateTimeOffset GetUtcStartOfDay(DateOnly day) => new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(-5)).ToUniversalTime();
    public Task<ParkingMovementView?> GetByIdAsync(Guid id, DateTimeOffset now, CancellationToken ct) =>
        Task.FromResult(Store.Movements.FirstOrDefault(x => x.Id == id) is { } movement ? View(movement, now) : null);
    private ParkingMovementView View(ParkingMovement movement, DateTimeOffset now) => new(movement.Id, movement.UserId, Target.FullName,
        movement.VehicleId, Vehicle.Type, Vehicle.Plate?.Value ?? Vehicle.FrameNumber!.Value, movement.ParkingLotId, Lot.Name,
        movement.ParkingZoneId, Zones.First(x => x.Id == movement.ParkingZoneId).Name, movement.CheckInAt, movement.CheckInGuardId,
        movement.CheckOutAt, movement.CheckOutGuardId, movement.Status, movement.GetDuration(now));
    public Task<PagedResult<ParkingMovementView>> SearchAsync(ParkingMovementFilter filter, DateTimeOffset now, CancellationToken ct)
    {
        LastFilter = filter;
        return Task.FromResult(new PagedResult<ParkingMovementView>([], filter.Page, filter.PageSize, 0));
    }
    public Task<VehiclesInsideView> GetInsideAsync(ParkingMovementFilter filter, DateTimeOffset now, CancellationToken ct) =>
        Task.FromResult(new VehiclesInsideView(new([], filter.Page, filter.PageSize, 0), new(0, 0, 0, 0)));
}
