using MzukuluQMS.Api.DTOs;

namespace MzukuluQMS.Api.Data.Repositories;

public interface IChecklistTemplateRepository
{
    Task<IReadOnlyList<ChecklistTemplateDto>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<ChecklistTemplateVersionDefinitionDto?> GetVersionDefinitionAsync(
        long checklistTemplateVersionId,
        CancellationToken cancellationToken = default);
}