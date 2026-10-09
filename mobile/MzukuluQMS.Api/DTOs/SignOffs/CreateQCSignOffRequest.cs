namespace MzukuluQMS.Api.DTOs.SignOffs;

public sealed class CreateQCSignOffRequest
{
    public string SignOffType { get; init; } = string.Empty;

    public long SignatureEvidenceID { get; init; }

    public string? Comments { get; init; }
}