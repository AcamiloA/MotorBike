using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Authorization;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.ParkingLots;
using UniversityParking.Application.Tests.Auth;
using UniversityParking.Domain.AcademicPeriods;
using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Users.ValueObjects;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Application.Tests.Administration;

internal sealed class AdministrationTestContext : IAcademicPeriodRepository, IParkingLotRepository, IParkingMovementRepository, IAuditLogRepository, IRequestContext
{
    public FakeClock Clock { get; } = new();
    public FakeUnitOfWork Work { get; } = new();
    public FakeCurrentUser Actor { get; } = new() { Roles = ["USER", "ADMIN"] };
    public FakeRoles Roles { get; } = new() { Codes = ["USER", "ADMIN"] };
    public FakeUsers Users { get; } = new();
    public AdministrationOperationContext Operation { get; }
    public List<AcademicPeriod> Periods { get; } = [];
    public List<ParkingLot> Lots { get; } = [];
    public List<ParkingZone> Zones { get; } = [];
    public List<AuditLog> Audits { get; } = [];
    public bool LotHasOpenMovement { get; set; }
    public string? IpAddress => "127.0.0.1";
    public string? TraceId => "administration-test";
    public AdministrationTestContext()
    {
        var user = new User(new IdentificationNumber("admin"), "Administrador", "ETITC", null, MemberType.STAFF,
            new CardCode("admin-card"), Clock.UtcNow.AddDays(-1));
        Users.Values.Add(user.Id, user);
        Actor.UserId = user.Id;
        Operation = new(Actor, Users, Roles, this, Clock, this);
    }
    Task<AcademicPeriod?> IAcademicPeriodRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Periods.FirstOrDefault(x => x.Id == id));
    Task<AcademicPeriod?> IAcademicPeriodRepository.GetByIdForUpdateAsync(Guid id, CancellationToken ct) => ((IAcademicPeriodRepository)this).GetByIdAsync(id, ct);
    public Task<AcademicPeriod?> GetActiveAsync(CancellationToken ct) => Task.FromResult(Periods.FirstOrDefault(x => x.Status == AcademicPeriodStatus.ACTIVE));
    public Task<AcademicPeriod?> GetActiveForShareAsync(CancellationToken ct) => GetActiveAsync(ct);
    public Task<bool> ExistsActiveAsync(CancellationToken ct) => Task.FromResult(Periods.Any(x => x.Status == AcademicPeriodStatus.ACTIVE));
    public Task<bool> ExistsByNameAsync(string name, CancellationToken ct) => Task.FromResult(Periods.Any(x => x.Name == name));
    public Task<IReadOnlyList<AcademicPeriod>> GetAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AcademicPeriod>>(Periods.OrderByDescending(x => x.StartsOn).ToArray());
    public Task AddAsync(AcademicPeriod period, CancellationToken ct) { Periods.Add(period); return Task.CompletedTask; }
    Task<ParkingLot?> IParkingLotRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Lots.FirstOrDefault(x => x.Id == id));
    Task<ParkingLot?> IParkingLotRepository.GetByIdForUpdateAsync(Guid id, CancellationToken ct) => ((IParkingLotRepository)this).GetByIdAsync(id, ct);
    public Task<bool> ExistsByNameAndCampusAsync(string name, string campus, Guid? excluded, CancellationToken ct) =>
        Task.FromResult(Lots.Any(x => x.Name == name && x.Campus == campus && x.Id != excluded));
    Task<IReadOnlyList<ParkingLot>> IParkingLotRepository.GetActiveAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ParkingLot>>(Lots.Where(x => x.Status == ParkingLotStatus.ACTIVE).ToArray());
    public Task<ParkingZone?> GetZoneForVehicleTypeAsync(Guid lotId, VehicleType type, CancellationToken ct) => Task.FromResult(Zones.FirstOrDefault(x => x.ParkingLotId == lotId && x.VehicleType == type));
    public Task AddAsync(ParkingLot lot, CancellationToken ct) { Lots.Add(lot); return Task.CompletedTask; }
    public Task AddZoneAsync(ParkingZone zone, CancellationToken ct) { Zones.Add(zone); return Task.CompletedTask; }
    public Task<PagedResult<ParkingLotView>> SearchAsync(ParkingLotStatus? status, PageRequest page, CancellationToken ct)
    {
        var selection = Lots.Where(x => !status.HasValue || x.Status == status.Value).ToArray();
        var items = selection.Skip((int)page.Offset).Take(page.PageSize).Select(x => new ParkingLotView(x.Id, x.Name, x.Campus,
            x.OpeningTime, x.ClosingTime, x.Status, Zones.Where(z => z.ParkingLotId == x.Id).Select(z => new ParkingZoneView(z.Id, z.Name, z.VehicleType, z.Status)).ToArray()));
        return Task.FromResult(new PagedResult<ParkingLotView>(items, page.Page, page.PageSize, selection.Length));
    }
    public Task<bool> ExistsOpenByParkingLotIdAsync(Guid id, CancellationToken ct) => Task.FromResult(LotHasOpenMovement);
    public Task<bool> ExistsOpenByVehicleIdAsync(Guid id, CancellationToken ct) => Task.FromResult(false);
    public Task<bool> ExistsOpenByUserIdAsync(Guid id, CancellationToken ct) => Task.FromResult(false);
    Task<ParkingMovement?> IParkingMovementRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<ParkingMovement?>(null);
    public Task<ParkingMovement?> GetOpenByVehicleIdAsync(Guid id, CancellationToken ct) => Task.FromResult<ParkingMovement?>(null);
    public Task<ParkingMovement?> GetOpenByUserIdAsync(Guid id, CancellationToken ct) => Task.FromResult<ParkingMovement?>(null);
    public Task AddAsync(ParkingMovement movement, CancellationToken ct) => Task.CompletedTask;
    public Task AddAsync(AuditLog audit, CancellationToken ct) { Audits.Add(audit); return Task.CompletedTask; }
}
