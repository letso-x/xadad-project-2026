using MzukuluQMSMobile.Models;

namespace MzukuluQMSMobile.Data.Remote;

public interface IQmsApiClient
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