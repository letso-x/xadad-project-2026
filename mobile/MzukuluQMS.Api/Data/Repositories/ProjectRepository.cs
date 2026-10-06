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
}