namespace MzukuluQMS.Api.DTOs;

public sealed class ProjectDto
{
    public long ProjectID { get; init; }
    public long ClientID { get; init; }

    public string ProjectNumber { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;

    public string? Description { get; init; }
    public string? ContractOrderNumber { get; init; }

    public string? EnclosureNumber { get; init; }
    public string? CabinetNumber { get; init; }

    public string? SiteName { get; init; }
    public string? SiteLocation { get; init; }

    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }

    public bool IsActive { get; init; }

    public string? Status { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}