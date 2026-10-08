using MzukuluQMS.Api.DTOs;

namespace MzukuluQMS.Api.Services;

public interface IQCFormService
{
    Task<QCFormDto> CreateAsync(
        CreateQCFormRequest request,
        CancellationToken cancellationToken = default);

    Task<QCFormDetailsDto?> GetByIdAsync(
        long qcFormId,
        CancellationToken cancellationToken = default);

    Task UpdateResponseAsync(
    long qcFormId,
    long qcResponseId,
    UpdateQCResponseRequest request,
    CancellationToken cancellationToken = default);

    Task UpdateProjectFieldAsync(
        long qcFormId,
        long qcFormProjectFieldId,
        UpdateQCFormProjectFieldRequest request,
        CancellationToken cancellationToken = default);
}