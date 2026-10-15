using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MzukuluQMS.Api.Data.Repositories;
namespace MzukuluQMS.Api.Services.SignOffs;

public sealed class QCFormContentHashService(IQCFormRepository repository) : IQCFormContentHashService
{
    public async Task<string> GenerateHashAsync(long qcFormId, CancellationToken cancellationToken = default) =>
        HashCanonicalContent(await repository.GetSignableContentAsync(qcFormId, cancellationToken));

    // Version 2: ordinal object keys, stable SQL array order, JSON primitive types/nulls,
    // normalised decimal numbers. Workflow state/times are not signed content.
    // Existing version-1 hashes are never rewritten or interpreted as version 2.
    public static string HashCanonicalContent(string content)
    {
        using var document = JsonDocument.Parse(content);
        if (!document.RootElement.TryGetProperty("qcForm", out var form) || form.ValueKind == JsonValueKind.Null)
            throw new KeyNotFoundException("QC form was not found.");
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, document.RootElement, false);
        var canonical = Encoding.UTF8.GetBytes("mzukulu-qc-content-v2\n").Concat(stream.ToArray()).ToArray();
        return Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant();
    }
    private static void Write(Utf8JsonWriter writer, JsonElement value, bool form)
    {
        switch(value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (form && property.Name is "status" or "submittedAt") continue;
                    writer.WritePropertyName(property.Name);
                    Write(writer, property.Value, property.Name == "qcForm");
                }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach(var item in value.EnumerateArray()) Write(writer,item,false); writer.WriteEndArray(); break;
            case JsonValueKind.Number:
                if (value.TryGetDecimal(out var number)) writer.WriteRawValue(number.ToString("G29", System.Globalization.CultureInfo.InvariantCulture));
                else writer.WriteRawValue(value.GetRawText());
                break;
            default: value.WriteTo(writer); break;
        }
    }
}
