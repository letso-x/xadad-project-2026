using Microsoft.AspNetCore.Http;

namespace MzukuluQMS.Api.DTOs.Evidence;

public sealed class UploadEvidenceRequest
{
    public long? QCFormFieldID { get; init; }

    public string EvidenceType { get; init; } = "Photo";

    public IFormFile File { get; init; } = default!;

    public decimal? Latitude { get; init; }

    public decimal? Longitude { get; init; }

    public DateTimeOffset? CapturedAt { get; init; }
}