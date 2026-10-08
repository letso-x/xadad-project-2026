using MzukuluQMS.Api.DTOs.Evidence;

namespace MzukuluQMS.Api.Data.Repositories;

public interface IEvidenceRepository
{
    Task<bool> QCFormExistsAsync(
        long qcFormId,
        CancellationToken cancellationToken = default);

    Task<bool> QCFormFieldBelongsToFormAsync(
        long qcFormId,
        long qcFormFieldId,
        CancellationToken cancellationToken = default);

    Task<EvidenceDto> CreateAsync(
        long qcFormId,
        long? qcFormFieldId,
        string evidenceType,
        string storageBucket,
        string storagePath,
        string fileName,
        string? contentType,
        long? fileSizeBytes,
        decimal? latitude,
        decimal? longitude,
        DateTimeOffset? capturedAt,
        Guid uploadedByUserId,
        CancellationToken cancellationToken = default);
}