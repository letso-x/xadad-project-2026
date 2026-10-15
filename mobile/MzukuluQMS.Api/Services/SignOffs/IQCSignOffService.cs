using MzukuluQMS.Api.DTOs.SignOffs;

namespace MzukuluQMS.Api.Services.SignOffs;

public interface IQCSignOffService
{
    Task<QCSignOffDto> CreateAsync(
        long qcFormId,
        CreateQCSignOffRequest request,
        CancellationToken cancellationToken = default);
}