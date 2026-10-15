using MzukuluQMS.Api.DTOs.SignOffs;

namespace MzukuluQMS.Api.Data.Repositories;

public interface IQCSignOffRepository
{
    Task<bool> QCFormExistsAsync(
        long qcFormId,
        CancellationToken cancellationToken = default);

    Task<bool> SignatureEvidenceIsValidAsync(
        long qcFormId,
        long signatureEvidenceId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<QCSignOffDto> CreateAsync(
    long qcFormId,
    Guid userId,
    string signOffType,
    long signatureEvidenceId,
    string? comments,
    DateTimeOffset signedAt,
    string contentHash,
    CancellationToken cancellationToken = default);
}