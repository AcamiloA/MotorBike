using Microsoft.EntityFrameworkCore;
using UniversityParking.Application.Common.Abstractions;
using UniversityParking.Domain.Vehicles;

namespace UniversityParking.Infrastructure.Persistence.Repositories;

public sealed class VehicleEvidenceRepository(AppDbContext context) : IVehicleEvidenceRepository
{
    public Task<VehicleVerificationImage?> GetVerificationImageAsync(Guid vehicleId, CancellationToken cancellationToken) =>
        context.VehicleVerificationImages.SingleOrDefaultAsync(x => x.VehicleId == vehicleId, cancellationToken);
    public async Task AddVerificationImageAsync(VehicleVerificationImage image, CancellationToken cancellationToken) =>
        await context.VehicleVerificationImages.AddAsync(image, cancellationToken);
    public async Task<IReadOnlyList<VehiclePhoto>> GetPhotosAsync(Guid vehicleId, CancellationToken cancellationToken) =>
        await context.VehiclePhotos.AsNoTracking().Where(x => x.VehicleId == vehicleId).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<VehicleDocument>> GetDocumentsAsync(Guid vehicleId, CancellationToken cancellationToken) =>
        await context.VehicleDocuments.AsNoTracking().Where(x => x.VehicleId == vehicleId).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
    public Task<VehiclePhoto?> GetPhotoAsync(Guid id, CancellationToken cancellationToken) => context.VehiclePhotos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<VehicleDocument?> GetDocumentAsync(Guid id, CancellationToken cancellationToken) => context.VehicleDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public async Task AddPhotoAsync(VehiclePhoto photo, CancellationToken cancellationToken) => await context.VehiclePhotos.AddAsync(photo, cancellationToken);
    public async Task AddDocumentAsync(VehicleDocument document, CancellationToken cancellationToken) => await context.VehicleDocuments.AddAsync(document, cancellationToken);
}
