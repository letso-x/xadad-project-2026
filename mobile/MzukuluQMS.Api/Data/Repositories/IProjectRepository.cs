using MzukuluQMS.Api.DTOs;

namespace MzukuluQMS.Api.Data.Repositories;

public interface IProjectRepository
{
    Task<IReadOnlyList<ProjectDto>> GetAllAsync(
        CancellationToken cancellationToken = default);
}