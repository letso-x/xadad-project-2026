namespace MzukuluQMS.Api.DTOs.Evidence;

public sealed class EvidenceDto
{
    public long EvidenceID { get; init; }

    public long QCFormID { get; init; }

    public long? QCFormFieldID { get; init; }

    public string EvidenceType { get; init; } = string.Empty;

    public string StorageBucket { get; init; } = string.Empty;

    public string StoragePath { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string? ContentType { get; init; }

    public long? FileSizeBytes { get; init; }

    public decimal? Latitude { get; init; }

    public decimal? Longitude { get; init; }

    public DateTimeOffset? CapturedAt { get; init; }

    public Guid UploadedByUserID { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}