using MediatR;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Application.Common.Errors;
using UniversityParking.Application.Common.Pagination;
using UniversityParking.Application.Common.Results;
using UniversityParking.Domain.Users;

namespace UniversityParking.Application.Vehicles;

public sealed class GetMyVehiclesQueryHandler(VehicleOperationContext operation, IVehicleRepository vehicles)
    : IRequestHandler<GetMyVehiclesQuery, Result<IReadOnlyList<VehicleView>>>
{
    public async Task<Result<IReadOnlyList<VehicleView>>> Handle(GetMyVehiclesQuery request, CancellationToken cancellationToken)
    {
        var actor = await operation.ActorAsync(cancellationToken);
        if (actor.IsFailure) return Result<IReadOnlyList<VehicleView>>.Failure(actor.Error!);
        return Result<IReadOnlyList<VehicleView>>.Success(await vehicles.GetByCurrentOwnerAsync(operation.ActorId, cancellationToken));
    }
}
public sealed class GetVehicleByIdQueryHandler(VehicleOperationContext operation, IVehicleRepository vehicles, IVehicleEvidenceRepository evidence)
    : IRequestHandler<GetVehicleByIdQuery, Result<VehicleDetail>>
{
    public async Task<Result<VehicleDetail>> Handle(GetVehicleByIdQuery request, CancellationToken cancellationToken)
    {
        var access = await operation.AuthorizedVehicleAsync(request.VehicleId, cancellationToken);
        if (access.IsFailure) return Result<VehicleDetail>.Failure(access.Error!);
        var vehicle = (await vehicles.GetViewAsync(request.VehicleId, cancellationToken))!;
        var photos = await evidence.GetPhotosAsync(request.VehicleId, cancellationToken);
        var documents = await evidence.GetDocumentsAsync(request.VehicleId, cancellationToken);
        var image = await evidence.GetVerificationImageAsync(request.VehicleId, cancellationToken);
        return Result<VehicleDetail>.Success(new(vehicle,
            photos.Select(x => new VehiclePhotoView(x.Id, x.Type, x.OriginalFileName, x.ContentType, x.SizeBytes)).ToArray(),
            documents.Select(x => new VehicleDocumentView(x.Id, x.Type, x.DocumentNumber, x.OriginalFileName,
                x.ContentType, x.SizeBytes, x.IssuedOn, x.ExpiresOn)).ToArray(),
            image is null ? null : new VerificationImageView(image.Id, image.Type, image.OriginalFileName, image.ContentType, image.SizeBytes)));
    }
}
public sealed class GetVehiclesQueryHandler(VehicleOperationContext operation, IVehicleRepository vehicles)
    : IRequestHandler<GetVehiclesQuery, Result<PagedResult<VehicleView>>>
{
    public async Task<Result<PagedResult<VehicleView>>> Handle(GetVehiclesQuery request, CancellationToken cancellationToken)
    {
        var actor = await operation.ActorAsync(cancellationToken);
        if (actor.IsFailure) return Result<PagedResult<VehicleView>>.Failure(actor.Error!);
        if (!await operation.HasRoleAsync(RoleCodes.Admin, cancellationToken)) return Result<PagedResult<VehicleView>>.Failure(CommonErrors.Forbidden);
        return Result<PagedResult<VehicleView>>.Success(await vehicles.SearchAsync(request, cancellationToken));
    }
}
public sealed class GetVehiclePhotoContentQueryHandler(VehicleOperationContext operation, IVehicleEvidenceRepository evidence, IFileStorage storage)
    : IRequestHandler<GetVehiclePhotoContentQuery, Result<FileContent>>
{
    public async Task<Result<FileContent>> Handle(GetVehiclePhotoContentQuery request, CancellationToken cancellationToken)
    {
        var access = await operation.AuthorizedVehicleAsync(request.VehicleId, cancellationToken, photoAccess: true);
        if (access.IsFailure) return Result<FileContent>.Failure(access.Error!);
        var file = await evidence.GetPhotoAsync(request.PhotoId, cancellationToken);
        if (file is null || file.VehicleId != request.VehicleId) return Result<FileContent>.Failure(VehicleErrors.NotFound);
        return await VehicleContent.ReadAsync(storage, file.StorageKey, file.ContentType, file.OriginalFileName, cancellationToken);
    }
}
public sealed class GetVehicleDocumentContentQueryHandler(VehicleOperationContext operation, IVehicleEvidenceRepository evidence, IFileStorage storage)
    : IRequestHandler<GetVehicleDocumentContentQuery, Result<FileContent>>
{
    public async Task<Result<FileContent>> Handle(GetVehicleDocumentContentQuery request, CancellationToken cancellationToken)
    {
        var access = await operation.AuthorizedVehicleAsync(request.VehicleId, cancellationToken);
        if (access.IsFailure) return Result<FileContent>.Failure(access.Error!);
        var file = await evidence.GetDocumentAsync(request.DocumentId, cancellationToken);
        if (file is null || file.VehicleId != request.VehicleId) return Result<FileContent>.Failure(VehicleErrors.NotFound);
        return await VehicleContent.ReadAsync(storage, file.StorageKey, file.ContentType, file.OriginalFileName, cancellationToken);
    }
}
internal static class VehicleContent
{
    public static async Task<Result<FileContent>> ReadAsync(IFileStorage storage, string key, string mime, string name, CancellationToken cancellationToken)
    {
        var url = await storage.GetReadUrlAsync(key, cancellationToken);
        try
        {
            return Result<FileContent>.Success(new(url is null ? await storage.OpenReadAsync(key, cancellationToken) : null, url, mime, name));
        }
        catch (FileNotFoundException) { return Result<FileContent>.Failure(VehicleErrors.NotFound); }
        catch (DirectoryNotFoundException) { return Result<FileContent>.Failure(VehicleErrors.NotFound); }
    }
}
