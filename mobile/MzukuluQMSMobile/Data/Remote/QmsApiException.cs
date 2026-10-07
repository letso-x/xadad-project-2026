using System.Net;

namespace MzukuluQMSMobile.Data.Remote;

public sealed class QmsApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public string? Title { get; }

    public string? Detail { get; }

    public QmsApiException(
        HttpStatusCode statusCode,
        string? title,
        string? detail)
        : base(detail ?? title ?? "The API request failed.")
    {
        StatusCode = statusCode;
        Title = title;
        Detail = detail;
    }
}