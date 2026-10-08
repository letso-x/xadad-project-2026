namespace MzukuluQMS.Api.Services.Evidence;

public sealed class EvidenceStorageResult
{
    public string Bucket { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;
}