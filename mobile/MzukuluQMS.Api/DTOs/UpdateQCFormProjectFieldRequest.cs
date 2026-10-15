using System.Text.Json;

namespace MzukuluQMS.Api.DTOs;

public sealed class UpdateQCFormProjectFieldRequest
{
    public JsonElement Value { get; init; }
}