using Dapper;
using MzukuluQMS.Api.DTOs;

namespace MzukuluQMS.Api.Data.Repositories;

public sealed class ChecklistTemplateRepository
    : IChecklistTemplateRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ChecklistTemplateRepository(
        IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ChecklistTemplateDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                "ChecklistTemplateID",
                "TemplateNumber",
                "TemplateName",
                "Subtitle",
                "IsActive"
            FROM public."ChecklistTemplate"
            ORDER BY "TemplateName";
            """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            cancellationToken: cancellationToken);

        var templates =
            await connection.QueryAsync<ChecklistTemplateDto>(command);

        return templates.AsList();
    }
}