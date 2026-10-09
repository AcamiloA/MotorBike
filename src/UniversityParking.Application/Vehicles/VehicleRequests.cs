using UniversityParking.Application.Common.Messaging;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.Files;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Application.Vehicles;

public enum RegistrationState { NONE, ACTIVE, CANCELLED, EXPIRED }
public sealed record PhotoUpload(VehiclePhotoType Type, UploadSource File);
public sealed record DocumentUpload(VehicleDocumentType Type, UploadSource File, string? DocumentNumber = null,
    DateOnly? IssuedOn = null, DateOnly? ExpiresOn = null);
public sealed record RegisterVehicleCommand(VehicleType Type, string? Plate, string? FrameNumber, string Brand,
    string Model, string Color, UploadSource? VerificationImage) : ICommand<Guid>;
public sealed record UpdateVehicleVerificationImageCommand(Guid VehicleId, UploadSource? VerificationImage) : ICommand;
public sealed record GetVehicleVerificationImageContentQuery(Guid VehicleId) : IQuery<FileContent>;
public sealed record RenewVehicleRegistrationCommand(Guid VehicleId, IReadOnlyList<DocumentUpload> Documents) : ICommand<Guid>;
public sealed record UpdateVehicleCommand(Guid VehicleId, string Brand, string Model, string Color,string? Identifier=null) : ICommand;
public sealed record ActivateVehicleCommand(Guid VehicleId) : ICommand;
public sealed record DeactivateVehicleCommand(Guid VehicleId) : ICommand;
public sealed record CorrectVehicleIdentifierCommand(Guid VehicleId, string Identifier, string Reason) : ICommand;
public sealed record TransferVehicleCommand(Guid VehicleId, string NewOwnerIdentificationNumber, string Reason) : ICommand;
public sealed record GetMyVehiclesQuery : IQuery<IReadOnlyList<VehicleView>>;
public sealed record GetVehicleByIdQuery(Guid VehicleId) : IQuery<VehicleDetail>;
public sealed record GetVehiclesQuery(string? Search = null, VehicleType? Type = null, VehicleStatus? Status = null,
    string? OwnerIdentificationNumber = null, RegistrationState? RegistrationState = null, int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<VehicleView>>;
public sealed record GetVehiclePhotoContentQuery(Guid VehicleId, Guid PhotoId) : IQuery<FileContent>;
public sealed record GetVehicleDocumentContentQuery(Guid VehicleId, Guid DocumentId) : IQuery<FileContent>;
public sealed record VehicleView(Guid Id, VehicleType Type, string? Plate, string? FrameNumber, string Brand, string Model,
    string Color, VehicleStatus Status, Guid? CurrentOwnerId, string? CurrentOwnerFullName, RegistrationState RegistrationState,
    bool IsInside, Guid? VerificationImageId, VehicleVerificationImageType? VerificationImageType = null);
public sealed record VehiclePhotoView(Guid Id, VehiclePhotoType Type, string OriginalFileName, string ContentType, long SizeBytes);
public sealed record VehicleDocumentView(Guid Id, VehicleDocumentType Type, string? DocumentNumber, string OriginalFileName,
    string ContentType, long SizeBytes, DateOnly? IssuedOn, DateOnly? ExpiresOn);
public sealed record VerificationImageView(Guid Id, VehicleVerificationImageType Type, string OriginalFileName, string ContentType, long SizeBytes);
public sealed record VehicleDetail(VehicleView Vehicle, IReadOnlyList<VehiclePhotoView> Photos, IReadOnlyList<VehicleDocumentView> Documents,
    VerificationImageView? VerificationImage = null);
public sealed record FileContent(Stream? Content, Uri? ReadUrl, string ContentType, string FileName);
