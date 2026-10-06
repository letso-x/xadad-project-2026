using Microsoft.AspNetCore.Mvc;
using MzukuluQMS.Api.Data;
using Npgsql;

namespace MzukuluQMS.Api.Controllers;

[ApiController]
[Route("api/health")]
public class DatabaseHealthController : ControllerBase
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DatabaseHealthController(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    [HttpGet("database")]
    public async Task<IActionResult> CheckDatabase(
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        await using var command =
            new NpgsqlCommand("SELECT 1;", connection);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return Ok(new
        {
            database = "connected",
            result
        });
    }
}