namespace MzukuluQMS.Api.DTOs.SignOffs;

public sealed class QCSignOffDto
{
    public long QCSignOffID { get; init; }

    public long QCFormID { get; init; }

    public Guid? UserID { get; init; }

    public string SignOffType { get; init; } = string.Empty;

    public long? SignatureEvidenceID { get; init; }

    public DateTimeOffset? SignedAt { get; init; }

    public string? Comments { get; init; }

    public string? ContentHash { get; init; }
}