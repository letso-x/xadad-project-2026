using MzukuluQMS.Api.DTOs;

namespace MzukuluQMS.Api.Services;

public interface IChecklistTemplateService
{
    Task<IReadOnlyList<ChecklistTemplateDto>> GetAllAsync(
        CancellationToken cancellationToken = default);
}