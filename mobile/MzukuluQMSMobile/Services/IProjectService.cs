using MzukuluQMSMobile.Models;

namespace MzukuluQMSMobile.Services;

public interface IProjectService
{
    Task<IReadOnlyList<Project>> GetProjectsAsync(
        CancellationToken cancellationToken = default);

    Task<Project?> GetProjectAsync(
        long projectId,
        CancellationToken cancellationToken = default);

    Task<Project> CreateProjectAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default);
}