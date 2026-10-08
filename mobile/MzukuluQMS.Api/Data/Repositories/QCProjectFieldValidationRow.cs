namespace MzukuluQMS.Api.Data.Repositories;

public sealed class QCProjectFieldValidationRow
{
    public long QCFormID { get; init; }

    public long QCFormProjectFieldID { get; init; }

    public string FieldType { get; init; } = string.Empty;

    public bool IsRequired { get; init; }

    public string? OptionsJson { get; init; }

    public string? ValidationRulesJson { get; init; }
}