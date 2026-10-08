using System.Text.Json;

namespace MzukuluQMS.Api.DTOs;

public sealed class QCFormProjectFieldDto
{
    public long QCFormProjectFieldID { get; init; }

    public long? ChecklistTemplateProjectFieldID { get; init; }

    public string FieldKey { get; init; } = string.Empty;

    public string FieldLabel { get; init; } = string.Empty;

    public string FieldType { get; init; } = string.Empty;

    public string? Placeholder { get; init; }

    public string? HelpText { get; init; }

    public bool IsRequired { get; init; }

    public JsonElement? Options { get; init; }

    public JsonElement? ValidationRules { get; init; }

    public int DisplayOrder { get; init; }

    public JsonElement? Value { get; init; }
}