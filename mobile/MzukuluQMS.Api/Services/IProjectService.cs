using MzukuluQMS.Api.DTOs;

namespace MzukuluQMS.Api.Services;

public interface IProjectService
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