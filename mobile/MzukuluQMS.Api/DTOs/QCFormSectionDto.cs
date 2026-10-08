namespace MzukuluQMS.Api.DTOs;

public sealed class QCFormSectionDto
{
    public long QCFormSectionID { get; init; }

    public long? ChecklistSectionID { get; init; }

    public string SectionName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int DisplayOrder { get; init; }

    public IReadOnlyList<QCFormFieldDto> Fields
    { get; init; } = [];
}