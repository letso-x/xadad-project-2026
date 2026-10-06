using MzukuluQMS.Api.Data.Repositories;
using MzukuluQMS.Api.DTOs;
using MzukuluQMS.Api.Exceptions;
using Npgsql;

namespace MzukuluQMS.Api.Services;

public sealed class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepository;

    public ProjectService(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public Task<IReadOnlyList<ProjectDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _projectRepository.GetAllAsync(cancellationToken);
    }

    public Task<ProjectDto?> GetByIdAsync(
        long projectId,
        CancellationToken cancellationToken = default)
    {
        return _projectRepository.GetByIdAsync(
            projectId,
            cancellationToken);
    }

    public async Task<ProjectDto> CreateAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ClientID <= 0)
        {
            throw new ArgumentException(
                "ClientID must be greater than zero.",
                nameof(request.ClientID));
        }

        if (string.IsNullOrWhiteSpace(request.ProjectName))
        {
            throw new ArgumentException(
                "ProjectName is required.",
                nameof(request.ProjectName));
        }

        if (request.StartDate.HasValue &&
            request.EndDate.HasValue &&
            request.EndDate.Value < request.StartDate.Value)
        {
            throw new ArgumentException(
                "EndDate cannot be earlier than StartDate.");
        }

        var normalizedRequest = new CreateProjectRequest
        {
            ClientID = request.ClientID,
            ProjectNumber = string.IsNullOrWhiteSpace(request.ProjectNumber)
                ? null
                : request.ProjectNumber.Trim(),

            ProjectName = request.ProjectName.Trim(),

            Description = NormalizeOptional(request.Description),
            ContractOrderNumber = NormalizeOptional(
                request.ContractOrderNumber),

            EnclosureNumber = NormalizeOptional(
                request.EnclosureNumber),

            CabinetNumber = NormalizeOptional(
                request.CabinetNumber),

            SiteName = NormalizeOptional(
                request.SiteName),

            SiteLocation = NormalizeOptional(
                request.SiteLocation),

            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        try
        {
            return await _projectRepository.CreateAsync(
                normalizedRequest,
                cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new ConflictException(
                "A project with the specified project number already exists.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}