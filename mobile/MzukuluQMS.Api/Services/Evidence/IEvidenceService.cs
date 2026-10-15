using MzukuluQMS.Api.DTOs.Evidence;

namespace MzukuluQMS.Api.Services.Evidence;

public interface IEvidenceService
{
    Task<EvidenceDto> UploadAsync(
        long qcFormId,
        UploadEvidenceRequest request,
        CancellationToken cancellationToken = default);
}