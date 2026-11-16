using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
namespace MzukuluQMS.Api.Diagnostics;

public sealed class WorkflowTimingMiddleware(RequestDelegate next,ILogger<WorkflowTimingMiddleware> logger)
{
 public async Task InvokeAsync(HttpContext context)
 {
  if(!context.Request.Path.StartsWithSegments("/api/qc-forms")||context.Request.Method is not ("POST" or "PUT" or "PATCH" or "DELETE"))
  {await next(context);return;}
  var started=Stopwatch.GetTimestamp();
  try{await next(context);}
  finally
  {
   // Route templates omit IDs, query strings and all submitted content.
   var route=(context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText??"unmatched";
   logger.LogInformation("QC workflow {Method} {Route} returned {StatusCode} in {ElapsedMs:F1} ms; trace {TraceId}.",
    context.Request.Method,route,context.Response.StatusCode,Stopwatch.GetElapsedTime(started).TotalMilliseconds,context.TraceIdentifier);
  }
 }
}
