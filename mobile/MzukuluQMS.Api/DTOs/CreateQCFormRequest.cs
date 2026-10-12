namespace MzukuluQMS.Api.DTOs;

public sealed class CreateQCFormRequest
{
    public long? ChecklistTemplateVersionID { get; init; }

    public long ProjectID { get; init; }
    public long ChecklistTemplateID { get; init; }

    public Guid? AssignedToUserID { get; init; }
}