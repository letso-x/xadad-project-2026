namespace MzukuluQMS.Api.Data.Repositories;

public sealed class QCFormFieldEvidenceRules
{
    public long QCFormFieldID { get; init; }

    public string FieldType { get; init; } = string.Empty;

    public bool RequiresPhoto { get; init; }

    public bool RequiresSignature { get; init; }
}