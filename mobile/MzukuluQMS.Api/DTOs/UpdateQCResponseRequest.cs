using System.Text.Json;

namespace MzukuluQMS.Api.DTOs;

public sealed class UpdateQCResponseRequest
{
    public JsonElement Value { get; init; }
}