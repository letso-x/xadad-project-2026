namespace MzukuluQMSMobile.Data.Remote;

public sealed class ApiProblemDetails
{
    public string? Title { get; init; }

    public int? Status { get; init; }

    public string? Detail { get; init; }

    public string? Instance { get; init; }
}