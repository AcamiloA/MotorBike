using Microsoft.EntityFrameworkCore;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.Reporting;
using UniversityParking.Domain.AcademicPeriods;
using UniversityParking.Domain.Incidents;
using UniversityParking.Domain.Parking;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Infrastructure.Persistence.Repositories;

public sealed class ReportingRepository(AppDbContext context) : IReportingRepository
{
    private Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> Snapshot(CancellationToken token) =>
        context.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, token);
    private static async Task<PagedResult<T>> PageAsync<T>(IQueryable<T> query, int page, int size, CancellationToken token)
    {
        var count = await query.LongCountAsync(token); var offset = ((long)page - 1) * size;
        var items = offset >= count ? [] : await query.Skip(checked((int)offset)).Take(size).ToListAsync(token);
        return new(items, page, size, count);
    }
    public async Task<PagedResult<AuditView>> GetAuditAsync(AuditFilter filter, CancellationToken token)
    {
        await using var tx = await Snapshot(token);
        var query = context.AuditLogs.AsNoTracking();
        if (filter.ActorUserId.HasValue) query = query.Where(x => x.ActorUserId == filter.ActorUserId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Action)) query = query.Where(x => x.Action == filter.Action);
        if (!string.IsNullOrWhiteSpace(filter.EntityType)) query = query.Where(x => x.EntityType == filter.EntityType);
        if (filter.EntityId.HasValue) query = query.Where(x => x.EntityId == filter.EntityId.Value);
        if (filter.From.HasValue) query = query.Where(x => x.CreatedAt >= filter.From.Value);
        if (filter.Until.HasValue) query = query.Where(x => x.CreatedAt < filter.Until.Value);
        var result = await PageAsync(query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Select(x =>
            new AuditView(x.Id, x.ActorUserId, x.Action, x.EntityType, x.EntityId, x.OldValues, x.NewValues, x.IpAddress, x.TraceId, x.CreatedAt)), filter.Page, filter.PageSize, token);
        await tx.CommitAsync(token); return result;
    }
    public async Task<AdminDashboardView> GetAdminDashboardAsync(UtcRange today, CancellationToken token)
    {
        await using var tx = await Snapshot(token);
        var result = new AdminDashboardView(await context.Users.LongCountAsync(x => x.Status == UserStatus.ACTIVE, token),
            await context.Vehicles.LongCountAsync(x => x.Status == VehicleStatus.ACTIVE, token),
            await context.ParkingMovements.LongCountAsync(x => x.Status == ParkingMovementStatus.OPEN, token),
            await context.ParkingMovements.LongCountAsync(x => x.CheckInAt >= today.From && (!today.Until.HasValue || x.CheckInAt < today.Until.Value), token),
            await context.ParkingMovements.LongCountAsync(x => x.CheckOutAt >= today.From && (!today.Until.HasValue || x.CheckOutAt < today.Until.Value), token),
            await context.Incidents.LongCountAsync(x => x.Status == IncidentStatus.OPEN, token),
            await context.AcademicPeriods.AsNoTracking().Where(x => x.Status == AcademicPeriodStatus.ACTIVE).Select(x => new NamedReference(x.Id, x.Name)).SingleOrDefaultAsync(token));
        await tx.CommitAsync(token); return result;
    }
    public async Task<GuardDashboardView?> GetGuardDashboardAsync(Guid lotId, UtcRange today, CancellationToken token)
    {
        await using var tx = await Snapshot(token);
        var lot = await context.ParkingLots.AsNoTracking().Where(x => x.Id == lotId).Select(x => new NamedReference(x.Id, x.Name)).SingleOrDefaultAsync(token);
        if (lot is null) return null;
        var movements = context.ParkingMovements.Where(x => x.ParkingLotId == lotId);
        var result = new GuardDashboardView(lot, await movements.LongCountAsync(x => x.Status == ParkingMovementStatus.OPEN, token),
            await movements.LongCountAsync(x => x.CheckInAt >= today.From && (!today.Until.HasValue || x.CheckInAt < today.Until.Value), token),
            await movements.LongCountAsync(x => x.CheckOutAt >= today.From && (!today.Until.HasValue || x.CheckOutAt < today.Until.Value), token),
            await context.Incidents.LongCountAsync(x => x.ParkingLotId == lotId && x.Status == IncidentStatus.OPEN, token));
        await tx.CommitAsync(token); return result;
    }
    public async Task<IReadOnlyList<DailyAccessView>> GetDailyAccessAsync(UtcRange range, Guid? lotId, CancellationToken token) =>
        await context.Database.SqlQuery<DailyAccessView>($"""
            SELECT (event_at AT TIME ZONE 'America/Bogota')::date AS "Date", SUM(ins)::bigint AS "CheckIns", SUM(outs)::bigint AS "CheckOuts"
            FROM (
                SELECT check_in_at AS event_at, 1 AS ins, 0 AS outs FROM parking_movements
                WHERE check_in_at >= {range.From} AND ({range.Until}::timestamptz IS NULL OR check_in_at < {range.Until}) AND ({lotId}::uuid IS NULL OR parking_lot_id = {lotId})
                UNION ALL
                SELECT check_out_at, 0, 1 FROM parking_movements
                WHERE check_out_at >= {range.From} AND ({range.Until}::timestamptz IS NULL OR check_out_at < {range.Until}) AND ({lotId}::uuid IS NULL OR parking_lot_id = {lotId})
            ) events GROUP BY "Date" ORDER BY "Date"
            """).ToListAsync(token);
    public async Task<IReadOnlyList<AccessGroupView>> GetByVehicleTypeAsync(UtcRange range, Guid? lotId, CancellationToken token) =>
        await context.Database.SqlQuery<AccessGroupView>($"""
            SELECT v.type AS "Type", SUM(CASE WHEN p.check_in_at >= {range.From} AND ({range.Until}::timestamptz IS NULL OR p.check_in_at < {range.Until}) THEN 1 ELSE 0 END)::bigint AS "CheckIns",
            SUM(CASE WHEN p.check_out_at >= {range.From} AND ({range.Until}::timestamptz IS NULL OR p.check_out_at < {range.Until}) THEN 1 ELSE 0 END)::bigint AS "CheckOuts"
            FROM parking_movements p JOIN vehicles v ON v.id = p.vehicle_id
            WHERE ({lotId}::uuid IS NULL OR p.parking_lot_id = {lotId}) AND
            ((p.check_in_at >= {range.From} AND ({range.Until}::timestamptz IS NULL OR p.check_in_at < {range.Until})) OR (p.check_out_at >= {range.From} AND ({range.Until}::timestamptz IS NULL OR p.check_out_at < {range.Until})))
            GROUP BY v.type ORDER BY v.type
            """).ToListAsync(token);
    public async Task<IReadOnlyList<AccessGroupView>> GetByMemberTypeAsync(UtcRange range, Guid? lotId, CancellationToken token) =>
        await context.Database.SqlQuery<AccessGroupView>($"""
            SELECT u.member_type AS "Type", SUM(CASE WHEN p.check_in_at >= {range.From} AND ({range.Until}::timestamptz IS NULL OR p.check_in_at < {range.Until}) THEN 1 ELSE 0 END)::bigint AS "CheckIns",
            SUM(CASE WHEN p.check_out_at >= {range.From} AND ({range.Until}::timestamptz IS NULL OR p.check_out_at < {range.Until}) THEN 1 ELSE 0 END)::bigint AS "CheckOuts"
            FROM parking_movements p JOIN users u ON u.id = p.user_id
            WHERE ({lotId}::uuid IS NULL OR p.parking_lot_id = {lotId}) AND
            ((p.check_in_at >= {range.From} AND ({range.Until}::timestamptz IS NULL OR p.check_in_at < {range.Until})) OR (p.check_out_at >= {range.From} AND ({range.Until}::timestamptz IS NULL OR p.check_out_at < {range.Until})))
            GROUP BY u.member_type ORDER BY u.member_type
            """).ToListAsync(token);
    private IQueryable<MovementHistoryView> Movements(Guid? vehicleId, Guid? userId) => context.ParkingMovements.AsNoTracking()
        .Where(x => (!vehicleId.HasValue || x.VehicleId == vehicleId.Value) && (!userId.HasValue || x.UserId == userId.Value))
        .OrderByDescending(x => x.CheckInAt).ThenBy(x => x.Id).Select(x => new MovementHistoryView(x.Id, x.UserId, x.VehicleId, x.ParkingLotId,
            x.ParkingZoneId, x.CheckInAt, x.CheckInGuardId, x.CheckOutAt, x.CheckOutGuardId, x.Status.ToString()));
    private IQueryable<IncidentHistoryView> Incidents(Guid? vehicleId, Guid? userId) => context.Incidents.AsNoTracking()
        .Where(x => (!vehicleId.HasValue || x.VehicleId == vehicleId.Value || context.ParkingMovements.Any(m => m.Id == x.ParkingMovementId && m.VehicleId == vehicleId.Value)) &&
            (!userId.HasValue || x.UserId == userId.Value || context.ParkingMovements.Any(m => m.Id == x.ParkingMovementId && m.UserId == userId.Value)))
        .OrderByDescending(x => x.OccurredAt).ThenBy(x => x.Id).Select(x => new IncidentHistoryView(x.Id, x.UserId, x.VehicleId, x.ParkingMovementId,
            x.ParkingLotId, x.Type.ToString(), x.Status.ToString(), x.Description, x.ReportedBy, x.OccurredAt));
    private IQueryable<OwnershipHistoryView> Ownerships(Guid? vehicleId, Guid? userId) => context.VehicleOwnerships.AsNoTracking()
        .Where(x => (!vehicleId.HasValue || x.VehicleId == vehicleId.Value) && (!userId.HasValue || x.UserId == userId.Value))
        .OrderByDescending(x => x.StartAt).ThenBy(x => x.Id).Select(x => new OwnershipHistoryView(x.Id, x.VehicleId, x.UserId, x.StartAt, x.EndAt, x.TransferReason));
    public async Task<VehicleHistoryView?> GetVehicleHistoryAsync(Guid id, int page, int size, CancellationToken token)
    {
        await using var tx = await Snapshot(token);
        if (!await context.Vehicles.AnyAsync(x => x.Id == id, token)) return null;
        var registrations = context.VehicleRegistrations.AsNoTracking().Where(x => x.VehicleId == id).OrderByDescending(x => x.RegisteredAt).ThenBy(x => x.Id)
            .Select(x => new RegistrationHistoryView(x.Id, x.VehicleId, x.UserId, x.AcademicPeriodId, x.Status.ToString(), x.RegisteredAt, x.CancelledAt));
        var result = new VehicleHistoryView(id, await PageAsync(Ownerships(id, null), page, size, token), await PageAsync(registrations, page, size, token),
            await PageAsync(Movements(id, null), page, size, token), await PageAsync(Incidents(id, null), page, size, token));
        await tx.CommitAsync(token); return result;
    }
    public async Task<UserHistoryView?> GetUserHistoryAsync(Guid id, int page, int size, CancellationToken token)
    {
        await using var tx = await Snapshot(token);
        if (!await context.Users.AnyAsync(x => x.Id == id, token)) return null;
        var result = new UserHistoryView(id, await PageAsync(Ownerships(null, id), page, size, token), await PageAsync(Movements(null, id), page, size, token), await PageAsync(Incidents(null, id), page, size, token));
        await tx.CommitAsync(token); return result;
    }
    public async Task<GuardActivityView?> GetGuardActivityAsync(Guid id, UtcRange range, CancellationToken token)
    {
        await using var tx = await Snapshot(token);
        if (!await context.Users.AnyAsync(x => x.Id == id, token)) return null;
        var result = new GuardActivityView(id,
            await context.ParkingMovements.LongCountAsync(x => x.CheckInGuardId == id && x.CheckInAt >= range.From && (!range.Until.HasValue || x.CheckInAt < range.Until.Value), token),
            await context.ParkingMovements.LongCountAsync(x => x.CheckOutGuardId == id && x.CheckOutAt >= range.From && (!range.Until.HasValue || x.CheckOutAt < range.Until.Value), token),
            await context.Incidents.LongCountAsync(x => x.ReportedBy == id && x.CreatedAt >= range.From && (!range.Until.HasValue || x.CreatedAt < range.Until.Value), token));
        await tx.CommitAsync(token); return result;
    }
}
