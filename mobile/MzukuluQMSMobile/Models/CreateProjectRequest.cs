namespace MzukuluQMSMobile.Models;

public sealed class CreateProjectRequest
{
    public long ClientID { get; init; }

    public string? ProjectNumber { get; init; }

    public string ProjectName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? ContractOrderNumber { get; init; }

    public string? EnclosureNumber { get; init; }

    public string? CabinetNumber { get; init; }

    public string? SiteName { get; init; }

    public string? SiteLocation { get; init; }

    public DateTime? StartDate { get; init; }

    public DateTime? EndDate { get; init; }
}