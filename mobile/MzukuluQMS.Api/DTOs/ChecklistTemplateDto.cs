namespace MzukuluQMS.Api.DTOs;

public sealed class ChecklistTemplateDto
{
    public long? ActiveVersionID { get; init; }
    public string? Revision { get; init; }
    public long ChecklistTemplateID { get; init; }
    public string? TemplateNumber { get; init; }
    public string TemplateName { get; init; } = string.Empty;
    public string? Subtitle { get; init; }
    public bool IsActive { get; init; }
}