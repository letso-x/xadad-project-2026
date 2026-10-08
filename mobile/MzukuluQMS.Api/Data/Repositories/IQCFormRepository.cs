using MzukuluQMS.Api.DTOs;

namespace MzukuluQMS.Api.Data.Repositories;

public interface IQCFormRepository
{
    Task<long?> GetActiveTemplateVersionIdAsync(
        long checklistTemplateId,
        CancellationToken cancellationToken = default);

    Task<bool> ProjectExistsAsync(
        long projectId,
        CancellationToken cancellationToken = default);

    Task<QCFormDto> CreateAsync(
        long projectId,
        long checklistTemplateId,
        long checklistTemplateVersionId,
        Guid? startedByUserId,
        Guid? assignedToUserId,
        CancellationToken cancellationToken = default);

    Task<QCFormDetailsDto?> GetByIdAsync(
    long qcFormId,
    CancellationToken cancellationToken = default);

    Task<QCResponseValidationRow?> GetResponseValidationAsync(
    long qcFormId,
    long qcResponseId,
    CancellationToken cancellationToken = default);

    Task<QCProjectFieldValidationRow?> GetProjectFieldValidationAsync(
        long qcFormId,
        long qcFormProjectFieldId,
        CancellationToken cancellationToken = default);

    Task UpdateResponseAsync(
    long qcFormId,
    long qcResponseId,
    string valueJson,
    Guid? answeredByUserId,
    CancellationToken cancellationToken = default);

    Task UpdateProjectFieldAsync(
        long qcFormId,
        long qcFormProjectFieldId,
        string valueJson,
        CancellationToken cancellationToken = default);
}