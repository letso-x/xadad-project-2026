using MzukuluQMS.Api.DTOs;

namespace MzukuluQMS.Api.Data.Repositories;

public interface IProjectRepository
{
    Task<IReadOnlyList<ProjectDto>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<ProjectDto?> GetByIdAsync(
        long projectId,
        CancellationToken cancellationToken = default);

    Task<ProjectDto> CreateAsync(
    CreateProjectRequest request,
    CancellationToken cancellationToken = default);
}