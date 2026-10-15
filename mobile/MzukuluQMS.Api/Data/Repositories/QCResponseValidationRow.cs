namespace MzukuluQMS.Api.Data.Repositories;

public sealed class QCResponseValidationRow
{
    public long QCFormID { get; init; }

    public long QCResponseID { get; init; }

    public long QCFormFieldID { get; init; }

    public string FieldType { get; init; } = string.Empty;

    public bool IsRequired { get; init; }

    public bool RequiresPhoto { get; init; }

    public bool RequiresSignature { get; init; }

    public string? OptionsJson { get; init; }

    public string? ValidationRulesJson { get; init; }
}