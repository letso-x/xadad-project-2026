using System.Text.Json;

namespace MzukuluQMS.Api.DTOs;

public sealed class QCFormFieldDto
{
    public long QCFormFieldID { get; init; }

    public long? ChecklistFieldID { get; init; }

    public string FieldKey { get; init; } = string.Empty;

    public string FieldLabel { get; init; } = string.Empty;

    public string FieldType { get; init; } = string.Empty;

    public string? Placeholder { get; init; }

    public string? HelpText { get; init; }

    public bool IsRequired { get; init; }

    public bool RequiresPhoto { get; init; }

    public bool RequiresSignature { get; init; }

    public JsonElement? Options { get; init; }

    public JsonElement? ValidationRules { get; init; }

    public int DisplayOrder { get; init; }

    public long QCResponseID { get; init; }

    public JsonElement? Value { get; init; }

    public bool IsAnswered { get; init; }

    public Guid? AnsweredByUserID { get; init; }

    public DateTime? AnsweredAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}