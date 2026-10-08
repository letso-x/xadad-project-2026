namespace MzukuluQMS.Api.DTOs;

public sealed class QCFormDetailsDto
{
    public long QCFormID { get; init; }

    public long ProjectID { get; init; }

    public long ChecklistTemplateID { get; init; }

    public long ChecklistTemplateVersionID { get; init; }

    public string FormNumber { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public Guid? StartedByUserID { get; init; }

    public Guid? AssignedToUserID { get; init; }

    public DateTime? StartedAt { get; init; }

    public DateTime? SubmittedAt { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }

    public IReadOnlyList<QCFormProjectFieldDto> ProjectFields
    { get; init; } = [];

    public IReadOnlyList<QCFormSectionDto> Sections
    { get; init; } = [];
}