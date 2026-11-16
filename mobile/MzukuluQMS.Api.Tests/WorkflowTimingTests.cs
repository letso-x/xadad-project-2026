using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MzukuluQMS.Api.Diagnostics;
namespace MzukuluQMS.Api.Tests;

public sealed class WorkflowTimingTests
{
 sealed class Capture:ILogger<WorkflowTimingMiddleware>
 {
  public List<string> Messages=[];
  public IDisposable? BeginScope<TState>(TState state)where TState:notnull=>null;
  public bool IsEnabled(LogLevel level)=>true;
  public void Log<TState>(LogLevel level,EventId id,TState state,Exception? exception,Func<TState,Exception?,string> formatter)=>Messages.Add(formatter(state,exception));
 }
 [Fact]public async Task Logs_final_failure_status_and_elapsed_time_without_request_secrets()
 {
  var logger=new Capture();var context=new DefaultHttpContext();context.Request.Method="POST";context.Request.Path="/api/qc-forms/10/submit";context.Request.QueryString=new("?secret=do-not-log");context.Request.Headers.Authorization="Bearer do-not-log";
  await new WorkflowTimingMiddleware(c=>{c.Response.StatusCode=409;return Task.CompletedTask;},logger).InvokeAsync(context);
  var message=Assert.Single(logger.Messages);Assert.Contains("409",message);Assert.Contains("ms; trace",message);Assert.DoesNotContain("do-not-log",message);Assert.DoesNotContain("/10/",message);
 }
 [Fact]public async Task Does_not_instrument_non_workflow_reads()
 {
  var logger=new Capture();var context=new DefaultHttpContext();context.Request.Method="GET";context.Request.Path="/api/qc-forms/10";var called=false;
  await new WorkflowTimingMiddleware(_=>{called=true;return Task.CompletedTask;},logger).InvokeAsync(context);
  Assert.True(called);Assert.Empty(logger.Messages);
 }
}
