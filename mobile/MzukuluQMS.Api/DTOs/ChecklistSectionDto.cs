namespace MzukuluQMS.Api.DTOs;

public sealed class ChecklistSectionDto
{
    public long ChecklistSectionID { get; init; }

    public string SectionName { get; init; } = string.Empty;
    public string? Description { get; init; }

    public int DisplayOrder { get; init; }
    public bool IsRequired { get; init; }

    public IReadOnlyList<ChecklistFieldDto> Fields
    { get; init; } = [];
}