using UniversityParking.Application.Common.Results;

namespace UniversityParking.Application.Common.Errors;

public static class AuthErrors
{
    public static Error InvalidCredentials { get; } = new("AUTH_INVALID_CREDENTIALS", "Credenciales inválidas.", ErrorType.Unauthorized);
    public static Error InvalidCurrentPassword { get; } = new("AUTH_INVALID_CREDENTIALS", "La contraseña actual no coincide.", ErrorType.Validation);
    public static Error UserInactive { get; } = new("AUTH_USER_INACTIVE", "El usuario está inactivo.", ErrorType.Unauthorized);
}

public static class UserErrors
{
    public static Error NotFound { get; } = new("USER_NOT_FOUND", "El usuario no existe.", ErrorType.NotFound);
    public static Error Inactive { get; } = new("USER_INACTIVE", "El usuario está inactivo.", ErrorType.Conflict);
    public static Error AlreadyExists { get; } = new("USER_ALREADY_EXISTS", "La identificación ya está registrada.", ErrorType.Conflict);
    public static Error CardCodeAlreadyExists { get; } = new("USER_CARD_CODE_ALREADY_EXISTS", "El código de carné ya está registrado.", ErrorType.Conflict);
    public static Error HasOpenParkingMovement { get; } = new("USER_HAS_OPEN_PARKING_MOVEMENT", "El usuario tiene un vehículo dentro.", ErrorType.Conflict);
    public static Error InvalidMemberTypeChange { get; } = new("INVALID_MEMBER_TYPE_CHANGE", "El tipo de miembro es incompatible con sus vehículos activos.", ErrorType.Conflict);
    public static Error RequiredRoleCannotBeRemoved { get; } = new("REQUIRED_ROLE_CANNOT_BE_REMOVED", "El rol USER es obligatorio.", ErrorType.Conflict);
}

public static class VehicleErrors
{
    public static Error NotFound { get; } = new("VEHICLE_NOT_FOUND", "El vehículo no existe.", ErrorType.NotFound);
    public static Error Inactive { get; } = new("VEHICLE_INACTIVE", "El vehículo está inactivo.", ErrorType.Conflict);
    public static Error IdentifierAlreadyExists { get; } = new("VEHICLE_IDENTIFIER_ALREADY_EXISTS", "El identificador del vehículo ya está registrado.", ErrorType.Conflict);
    public static Error HasOpenParkingMovement { get; } = new("VEHICLE_HAS_OPEN_PARKING_MOVEMENT", "El vehículo se encuentra dentro del parqueadero.", ErrorType.Conflict);
    public static Error StudentCannotRegisterCar { get; } = new("STUDENT_CANNOT_REGISTER_CAR", "Los estudiantes no pueden registrar carros.", ErrorType.Conflict);
    public static Error OwnershipNotFound { get; } = new("VEHICLE_OWNERSHIP_NOT_FOUND", "El vehículo no tiene propietario actual.", ErrorType.NotFound);
    public static Error NotOwnedByUser { get; } = new("VEHICLE_NOT_OWNED_BY_USER", "El vehículo no pertenece al usuario.", ErrorType.Forbidden);
    public static Error InvalidOwner { get; } = new("INVALID_VEHICLE_OWNER", "El propietario no es válido.", ErrorType.Conflict);
    public static Error AlreadyHasCurrentOwner { get; } = new("VEHICLE_ALREADY_HAS_CURRENT_OWNER", "El vehículo ya tiene un propietario actual.", ErrorType.Conflict);
    public static Error RegistrationRequired { get; } = new("VEHICLE_REGISTRATION_REQUIRED", "El vehículo requiere un registro vigente del periodo actual.", ErrorType.Conflict);
    public static Error RegistrationAlreadyExists { get; } = new("VEHICLE_REGISTRATION_ALREADY_EXISTS", "El registro del vehículo ya existe para este propietario y periodo.", ErrorType.Conflict);
    public static Error RegistrationCancelled { get; } = new("VEHICLE_REGISTRATION_CANCELLED", "El registro del vehículo está cancelado.", ErrorType.Conflict);
}

public static class AcademicPeriodErrors
{
    public static Error NameAlreadyExists { get; } = new("ACADEMIC_PERIOD_ALREADY_EXISTS", "El nombre del periodo ya está registrado.", ErrorType.Conflict);
    public static Error InvalidState { get; } = new("ACADEMIC_PERIOD_NOT_ACTIVE", "El estado del periodo no permite esta operación.", ErrorType.Conflict);
    public static Error CurrentNotFound { get; } = new("ACADEMIC_PERIOD_NOT_ACTIVE", "No existe un periodo académico activo.", ErrorType.NotFound);
    public static Error NotFound { get; } = new("ACADEMIC_PERIOD_NOT_FOUND", "El periodo académico no existe.", ErrorType.NotFound);
    public static Error NotActive { get; } = new("ACADEMIC_PERIOD_NOT_ACTIVE", "No existe un periodo académico activo.", ErrorType.Conflict);
    public static Error AlreadyActive { get; } = new("ACADEMIC_PERIOD_ALREADY_ACTIVE", "El periodo académico ya está activo.", ErrorType.Conflict);
    public static Error AnotherActivePeriodExists { get; } = new("ACTIVE_ACADEMIC_PERIOD_ALREADY_EXISTS", "Ya existe otro periodo académico activo.", ErrorType.Conflict);
}

public static class ParkingErrors
{
    public static Error ZoneUnavailable { get; } = new("PARKING_ZONE_NOT_AVAILABLE", "No existe una zona activa compatible con el vehículo.", ErrorType.Conflict);
    public static Error LotAlreadyExists { get; } = new("PARKING_LOT_ALREADY_EXISTS", "Ya existe un parqueadero con este nombre y sede.", ErrorType.Conflict);
    public static Error LotNotFound { get; } = new("PARKING_LOT_NOT_FOUND", "El parqueadero no existe.", ErrorType.NotFound);
    public static Error LotInactive { get; } = new("PARKING_LOT_INACTIVE", "El parqueadero está inactivo.", ErrorType.Conflict);
    public static Error LotClosed { get; } = new("PARKING_LOT_CLOSED", "El parqueadero está cerrado para nuevos ingresos.", ErrorType.Conflict);
    public static Error LotHasOpenMovements { get; } = new("PARKING_LOT_HAS_OPEN_MOVEMENTS", "El parqueadero tiene vehículos dentro.", ErrorType.Conflict);
    public static Error VehicleAlreadyInside { get; } = new("VEHICLE_ALREADY_INSIDE", "El vehículo ya se encuentra dentro del parqueadero.", ErrorType.Conflict);
    public static Error UserAlreadyHasVehicleInside { get; } = new("USER_ALREADY_HAS_VEHICLE_INSIDE", "Este usuario ya tiene otro vehículo dentro.", ErrorType.Conflict);
    public static Error VehicleNotInside { get; } = new("VEHICLE_NOT_INSIDE", "El vehículo no tiene un movimiento abierto.", ErrorType.Conflict);
}

public static class IncidentErrors
{
    public static Error NotFound { get; } = new("INCIDENT_NOT_FOUND", "El incidente no existe.", ErrorType.NotFound);
    public static Error AlreadyResolved { get; } = new("INCIDENT_ALREADY_RESOLVED", "El incidente ya está resuelto.", ErrorType.Conflict);
    public static Error NotOpen { get; } = new("INCIDENT_NOT_OPEN", "El incidente no está abierto.", ErrorType.Conflict);
}

public static class NewsErrors
{
    public static Error NotFound { get; } = new("NEWS_NOT_FOUND", "La noticia no existe.", ErrorType.NotFound);
    public static Error InvalidState { get; } = new("INVALID_NEWS_STATE", "El estado de la noticia no permite esta operación.", ErrorType.Conflict);
}

public static class FileErrors
{
    public static Error TooLarge { get; } = new("FILE_TOO_LARGE", "El archivo supera el tamaño permitido.", ErrorType.Validation);
    public static Error TypeNotAllowed { get; } = new("FILE_TYPE_NOT_ALLOWED", "El tipo de archivo no está permitido.", ErrorType.Validation);
}

public static class CommonErrors
{
    public const string RateLimitExceededCode = "RATE_LIMIT_EXCEEDED";
    public static Error Forbidden { get; } = new("FORBIDDEN", "No tienes permisos para realizar esta acción.", ErrorType.Forbidden);
    public static Error Validation(IReadOnlyDictionary<string, IReadOnlyList<string>> errors) =>
        new("VALIDATION_ERROR", "Revisa los datos ingresados.", ErrorType.Validation, errors);
}
