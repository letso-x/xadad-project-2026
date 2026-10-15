namespace MzukuluQMS.Api.Configuration;

public sealed class SupabaseAuthOptions
{
    public const string SectionName = "SupabaseAuth";

    public string MetadataAddress { get; init; } = string.Empty;

    public string Audience { get; init; } = "authenticated";
}