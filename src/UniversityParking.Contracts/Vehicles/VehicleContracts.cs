using System.Text.Json.Serialization;

namespace UniversityParking.Contracts.Vehicles;

public enum VehicleKind { CAR, MOTORCYCLE, BICYCLE, SCOOTER }
public enum VehicleAccountStatus { ACTIVE, INACTIVE }
public enum VehicleRegistrationState { NONE, ACTIVE, CANCELLED, EXPIRED }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateVehicleRequest(string Brand, string Model, string Color,string? Identifier=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CorrectVehicleIdentifierRequest(string Identifier, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TransferVehicleRequest(string NewOwnerIdentificationNumber, string Reason);
public sealed record VehicleCreatedResponse(Guid Id);
public sealed record VehicleRenewedResponse(Guid RegistrationId);
public sealed record VehicleResponse(Guid Id, string Type, string? Plate, string? FrameNumber, string Brand, string Model,
    string Color, string Status, Guid? CurrentOwnerId, string? CurrentOwnerFullName, string RegistrationState, bool IsInside, string? VerificationImagePreviewUrl);
public sealed record VehiclePhotoResponse(Guid Id, string Type, string OriginalFileName, string ContentType, long SizeBytes, string ContentUrl);
public sealed record VehicleDocumentResponse(Guid Id, string Type, string? DocumentNumber, string OriginalFileName,
    string ContentType, long SizeBytes, DateOnly? IssuedOn, DateOnly? ExpiresOn, string ContentUrl);
public sealed record VehicleVerificationImageResponse(Guid Id, string Type, string OriginalFileName, string ContentType, long SizeBytes, string ContentUrl);
public sealed record VehicleDetailResponse(VehicleResponse Vehicle, IReadOnlyList<VehiclePhotoResponse> Photos, IReadOnlyList<VehicleDocumentResponse> Documents,
    VehicleVerificationImageResponse? VerificationImage = null);
