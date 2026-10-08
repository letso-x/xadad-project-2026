namespace MzukuluQMS.Api.Configuration;

public sealed class SupabaseStorageOptions
{
    public const string SectionName = "SupabaseStorage";

    public string BaseUrl { get; init; } = string.Empty;

    public string SecretKey { get; init; } = string.Empty;

    public string Bucket { get; init; } = string.Empty;
}