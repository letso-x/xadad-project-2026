using MzukuluQMS.Api.DTOs;

namespace MzukuluQMS.Api.Services;

public interface IChecklistTemplateService
{
    Task<IReadOnlyList<ChecklistTemplateDto>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<ChecklistTemplateVersionDefinitionDto?> GetVersionDefinitionAsync(
        long checklistTemplateVersionId,
        CancellationToken cancellationToken = default);
}