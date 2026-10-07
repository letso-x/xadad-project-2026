using MzukuluQMS.Api.Data.Repositories;
using MzukuluQMS.Api.DTOs;

namespace MzukuluQMS.Api.Services;

public sealed class ChecklistTemplateService
    : IChecklistTemplateService
{
    private readonly IChecklistTemplateRepository _repository;

    public ChecklistTemplateService(
        IChecklistTemplateRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<ChecklistTemplateDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }
}