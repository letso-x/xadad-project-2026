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
            Npgsql.NpgsqlException {InnerException:System.Net.Sockets.SocketException} => StatusCodes.Status503ServiceUnavailable,
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
            _logger.LogError("Unhandled request failure ({ExceptionType}); cause {CauseType}; SQLSTATE {SqlState}; trace {TraceId}.",
                exception.GetType().Name,exception.GetBaseException().GetType().Name,(exception as Npgsql.PostgresException)?.SqlState??"none",httpContext.TraceIdentifier);
        }

        if(statusCode==StatusCodes.Status503ServiceUnavailable)_logger.LogWarning("Database connection unavailable ({ExceptionType}); trace {TraceId}.",exception.InnerException?.GetType().Name??exception.GetType().Name,httpContext.TraceIdentifier);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,

            Title = statusCode switch
            {
                StatusCodes.Status503ServiceUnavailable => "Database temporarily unavailable.",
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

            Detail = statusCode switch
            {
                StatusCodes.Status500InternalServerError => "An unexpected error occurred while processing the request.",
                StatusCodes.Status503ServiceUnavailable => "The database is temporarily unavailable. Check connectivity and retry after service is restored.",
                _ => exception.Message
            },

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
