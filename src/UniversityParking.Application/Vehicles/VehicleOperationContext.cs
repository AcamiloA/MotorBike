using System.Text.Json;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Results;
using UniversityParking.Domain.Auditing;
using UniversityParking.Domain.Users;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Application.Vehicles;

public sealed class VehicleOperationContext(ICurrentUser currentUser, IUserRepository users, IRoleRepository roles,
    IVehicleRepository vehicles, IVehicleOwnershipRepository ownerships, IClock clock, IAuditLogRepository audits, IRequestContext context)
{
    public Guid ActorId => currentUser.UserId!.Value;
    public DateTimeOffset UtcNow => clock.UtcNow;
    public async Task<Result<User>> ActorAsync(CancellationToken cancellationToken, bool locked = false)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } id) return Result<User>.Failure(AuthErrors.InvalidCredentials);
        var user = locked ? await users.GetByIdForUpdateAsync(id, cancellationToken) : await users.GetByIdAsync(id, cancellationToken);
        return user is null ? Result<User>.Failure(UserErrors.NotFound) : user.Status != UserStatus.ACTIVE ?
            Result<User>.Failure(UserErrors.Inactive) : Result<User>.Success(user);
    }
    public async Task<bool> HasRoleAsync(string role, CancellationToken cancellationToken) =>
        currentUser.IsInRole(role) && (await roles.GetCodesByUserIdAsync(ActorId, cancellationToken)).Contains(role);
    public async Task<Result<Vehicle>> AuthorizedVehicleAsync(Guid id, CancellationToken cancellationToken,
        bool adminOnly = false, bool ownerOnly = false, bool photoAccess = false, bool locked = false)
    {
        var actor = await ActorAsync(cancellationToken, locked);
        if (actor.IsFailure) return Result<Vehicle>.Failure(actor.Error!);
        var admin = !ownerOnly && await HasRoleAsync(RoleCodes.Admin, cancellationToken);
        if (adminOnly && !admin) return Result<Vehicle>.Failure(CommonErrors.Forbidden);
        var vehicle = locked ? await vehicles.GetByIdForUpdateAsync(id, cancellationToken) : await vehicles.GetByIdAsync(id, cancellationToken);
        if (vehicle is null) return Result<Vehicle>.Failure(VehicleErrors.NotFound);
        if (!admin && !(photoAccess && await HasRoleAsync(RoleCodes.Guard, cancellationToken)) &&
            await ownerships.GetCurrentByVehicleAndUserAsync(id, ActorId, cancellationToken) is null)
            return Result<Vehicle>.Failure(VehicleErrors.NotFound);
        return Result<Vehicle>.Success(vehicle);
    }
    public Task AuditAsync(string action, Guid vehicleId, object? oldValues, object? newValues, CancellationToken cancellationToken) =>
        audits.AddAsync(new AuditLog(ActorId, action, "Vehicle", vehicleId, UtcNow,
            oldValues is null ? null : JsonSerializer.Serialize(oldValues), newValues is null ? null : JsonSerializer.Serialize(newValues),
            context.IpAddress, context.TraceId), cancellationToken);
    public static Error? Eligibility(User owner, VehicleType type) => owner.Status != UserStatus.ACTIVE ? UserErrors.Inactive :
        owner.MemberType == MemberType.STUDENT && type == VehicleType.CAR ? VehicleErrors.StudentCannotRegisterCar : null;
    public static VehicleDocumentType[] RequiredDocuments(VehicleType type) => type == VehicleType.BICYCLE ?
        [VehicleDocumentType.OWNERSHIP_SUPPORT] : [VehicleDocumentType.VEHICLE_REGISTRATION, VehicleDocumentType.INSURANCE];
    public static Error MissingDocuments() => CommonErrors.Validation(new Dictionary<string, IReadOnlyList<string>>
        { ["Documents"] = ["Faltan documentos obligatorios para este tipo de vehículo."] });
}
