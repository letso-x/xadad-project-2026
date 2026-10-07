namespace MzukuluQMS.Api.DTOs;

public sealed class ChecklistTemplateVersionDefinitionDto
{
    public long ChecklistTemplateVersionID { get; init; }
    public long ChecklistTemplateID { get; init; }

    public string? TemplateNumber { get; init; }
    public string TemplateName { get; init; } = string.Empty;
    public string? Subtitle { get; init; }
    public bool IsTemplateActive { get; init; }

    public string Revision { get; init; } = string.Empty;
    public DateTime? IssueDate { get; init; }
    public DateTime? EffectiveDate { get; init; }
    public string Status { get; init; } = string.Empty;

    public string? Scope { get; init; }
    public string? AcceptanceCriteria { get; init; }

    public IReadOnlyList<ChecklistTemplateProjectFieldDto> ProjectFields
    { get; init; } = [];

    public IReadOnlyList<ChecklistSectionDto> Sections
    { get; init; } = [];
}