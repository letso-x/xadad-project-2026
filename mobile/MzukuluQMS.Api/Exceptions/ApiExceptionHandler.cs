using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MzukuluQMS.Api.Exceptions;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(
        ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationReplayException replay)
        {
            httpContext.Response.StatusCode=replay.Status;
            httpContext.Response.Headers.ETag="\""+replay.Revision+"\"";
            httpContext.Response.Headers["Idempotency-Replayed"]="true";
            if(replay.Status==201) httpContext.Response.Headers.Location="/api/qc-forms/"+replay.FormId;
            if(replay.Status!=204) {httpContext.Response.ContentType="application/json";await httpContext.Response.WriteAsync(replay.Json,cancellationToken);}
            return true;
        }
        var statusCode = exception switch
        {
            ForbiddenException => StatusCodes.Status403Forbidden,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            ArgumentException =>
                StatusCodes.Status400BadRequest,

            KeyNotFoundException =>
                StatusCodes.Status404NotFound,

            ConflictException =>
                StatusCodes.Status409Conflict,

            InvalidOperationException =>
                StatusCodes.Status409Conflict,

            _ =>
                StatusCodes.Status500InternalServerError
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "An unhandled exception occurred.");
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,

            Title = statusCode switch
            {
                StatusCodes.Status401Unauthorized => "Authentication required.",
                StatusCodes.Status403Forbidden => "Permission denied.",
                StatusCodes.Status400BadRequest =>
                    "Invalid request.",

                StatusCodes.Status404NotFound =>
                    "Resource not found.",

                StatusCodes.Status409Conflict =>
                    "Conflict.",

                _ =>
                    "An unexpected error occurred."
            },

            Detail =
                statusCode ==
                StatusCodes.Status500InternalServerError
                    ? "An unexpected error occurred while processing the request."
                    : exception.Message,

            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode =
            statusCode;

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            cancellationToken);

        return true;
    }
}