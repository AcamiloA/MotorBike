using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Application.Common.Abstractions;

public interface IVehicleEvidenceRepository
{
    Task<IReadOnlyList<VehiclePhoto>> GetPhotosAsync(Guid vehicleId, CancellationToken cancellationToken);
    Task<IReadOnlyList<VehicleDocument>> GetDocumentsAsync(Guid vehicleId, CancellationToken cancellationToken);
    Task<VehiclePhoto?> GetPhotoAsync(Guid id, CancellationToken cancellationToken);
    Task<VehicleDocument?> GetDocumentAsync(Guid id, CancellationToken cancellationToken);
    Task AddPhotoAsync(VehiclePhoto photo, CancellationToken cancellationToken);
    Task AddDocumentAsync(VehicleDocument document, CancellationToken cancellationToken);
}
