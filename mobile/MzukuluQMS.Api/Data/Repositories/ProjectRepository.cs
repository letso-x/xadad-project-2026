using Dapper;
using MzukuluQMS.Api.DTOs;

namespace MzukuluQMS.Api.Data.Repositories;

public sealed class ProjectRepository : IProjectRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ProjectRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ProjectDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                "ProjectID",
                "ClientID",
                "ProjectNumber",
                "ProjectName",
                "Description",
                "ContractOrderNumber",
                "EnclosureNumber",
                "CabinetNumber",
                "SiteName",
                "SiteLocation",
                "StartDate",
                "EndDate",
                "IsActive",
                "Status",
                "CreatedAt",
                "UpdatedAt"
            FROM public."Project"
            ORDER BY "CreatedAt" DESC;
            """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            cancellationToken: cancellationToken);

        var projects = await connection.QueryAsync<ProjectDto>(command);

        return projects.AsList();
    }

    public async Task<ProjectDto?> GetByIdAsync(
    long projectId,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            "ProjectID",
            "ClientID",
            "ProjectNumber",
            "ProjectName",
            "Description",
            "ContractOrderNumber",
            "EnclosureNumber",
            "CabinetNumber",
            "SiteName",
            "SiteLocation",
            "StartDate",
            "EndDate",
            "IsActive",
            "Status",
            "CreatedAt",
            "UpdatedAt"
        FROM public."Project"
        WHERE "ProjectID" = @ProjectID;
        """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new { ProjectID = projectId },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<ProjectDto>(command);
    }
    public async Task<ProjectDto> CreateAsync(
    CreateProjectRequest request,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        INSERT INTO public."Project"
        (
            "ClientID",
            "ProjectNumber",
            "ProjectName",
            "Description",
            "ContractOrderNumber",
            "EnclosureNumber",
            "CabinetNumber",
            "SiteName",
            "SiteLocation",
            "StartDate",
            "EndDate"
        )
        VALUES
        (
            @ClientID,
            @ProjectNumber,
            @ProjectName,
            @Description,
            @ContractOrderNumber,
            @EnclosureNumber,
            @CabinetNumber,
            @SiteName,
            @SiteLocation,
            @StartDate,
            @EndDate
        )
        RETURNING
            "ProjectID",
            "ClientID",
            "ProjectNumber",
            "ProjectName",
            "Description",
            "ContractOrderNumber",
            "EnclosureNumber",
            "CabinetNumber",
            "SiteName",
            "SiteLocation",
            "StartDate",
            "EndDate",
            "IsActive",
            "Status",
            "CreatedAt",
            "UpdatedAt";
        """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                request.ClientID,
                request.ProjectNumber,
                request.ProjectName,
                request.Description,
                request.ContractOrderNumber,
                request.EnclosureNumber,
                request.CabinetNumber,
                request.SiteName,
                request.SiteLocation,
                request.StartDate,
                request.EndDate
            },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleAsync<ProjectDto>(command);
    }
}