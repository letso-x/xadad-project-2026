using Microsoft.AspNetCore.Mvc;
using MzukuluQMS.Api.DTOs;
using MzukuluQMS.Api.Services;

namespace MzukuluQMS.Api.Controllers;

[ApiController]
[Route("api/clients")]
public sealed class ClientsController : ControllerBase
{
    private readonly IClientService _service;

    public ClientsController(IClientService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyList<ClientDto>),
        StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClientDto>>> GetClients(
        CancellationToken cancellationToken)
    {
        var clients =
            await _service.GetAllAsync(
                cancellationToken);

        return Ok(clients);
    }

    [HttpGet("{clientId:long}")]
    [ProducesResponseType(
        typeof(ClientDto),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClientDto>> GetClient(
        long clientId,
        CancellationToken cancellationToken)
    {
        var client =
            await _service.GetByIdAsync(
                clientId,
                cancellationToken);

        if (client is null)
        {
            return NotFound();
        }

        return Ok(client);
    }

    [HttpPost]
    [ProducesResponseType(
    typeof(ClientDto),
    StatusCodes.Status201Created)]
    [ProducesResponseType(
    StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClientDto>> CreateClient(
    [FromBody] CreateClientRequest request,
    CancellationToken cancellationToken)
    {
        var client =
            await _service.CreateAsync(
                request,
                cancellationToken);

        return CreatedAtAction(
            nameof(GetClient),
            new { clientId = client.ClientID },
            client);
    }

    [HttpPut("{clientId:long}")]
    [ProducesResponseType(
    typeof(ClientDto),
    StatusCodes.Status200OK)]
    [ProducesResponseType(
    StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
    StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientDto>> UpdateClient(
    long clientId,
    [FromBody] UpdateClientRequest request,
    CancellationToken cancellationToken)
    {
        var client =
            await _service.UpdateAsync(
                clientId,
                request,
                cancellationToken);

        if (client is null)
        {
            return NotFound();
        }

        return Ok(client);
    }
}