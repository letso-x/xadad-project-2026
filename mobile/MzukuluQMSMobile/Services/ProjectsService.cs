using MzukuluQMSMobile.Data.Remote;
using MzukuluQMSMobile.Models;

namespace MzukuluQMSMobile.Services;

public sealed class ProjectService : IProjectService
{
    private readonly IQmsApiClient _apiClient;

    public ProjectService(IQmsApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<IReadOnlyList<Project>> GetProjectsAsync(
        CancellationToken cancellationToken = default)
    {
        return _apiClient.GetProjectsAsync(cancellationToken);
    }

    public Task<Project?> GetProjectAsync(
        long projectId,
        CancellationToken cancellationToken = default)
    {
        return _apiClient.GetProjectAsync(
            projectId,
            cancellationToken);
    }

    public Task<Project> CreateProjectAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        return _apiClient.CreateProjectAsync(
            request,
            cancellationToken);
    }
}