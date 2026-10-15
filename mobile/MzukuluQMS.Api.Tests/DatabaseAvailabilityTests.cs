using System.Net.Sockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MzukuluQMS.Api.Exceptions;
using Npgsql;
namespace MzukuluQMS.Api.Tests;
public sealed class DatabaseAvailabilityTests
{
 [Fact] public async Task DnsFailureReturnsSanitisedServiceUnavailable()
 {
  var context=new DefaultHttpContext{RequestServices=new ServiceCollection().AddOptions().BuildServiceProvider()};context.Response.Body=new MemoryStream();
  await new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance).TryHandleAsync(context,new NpgsqlException("private host detail",new SocketException((int)SocketError.HostNotFound)),default);
  Assert.Equal(503,context.Response.StatusCode);context.Response.Body.Position=0;var body=await new StreamReader(context.Response.Body).ReadToEndAsync();
  Assert.Contains("temporarily unavailable",body);Assert.DoesNotContain("private host detail",body);
 }
 [Fact] public async Task SqlSchemaErrorRemainsUnexpectedServerError()
 {
  var context=new DefaultHttpContext{RequestServices=new ServiceCollection().AddOptions().BuildServiceProvider()};context.Response.Body=new MemoryStream();
  await new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance).TryHandleAsync(context,new PostgresException("private SQL detail","ERROR","ERROR","42703"),default);
  Assert.Equal(500,context.Response.StatusCode);context.Response.Body.Position=0;Assert.DoesNotContain("private SQL detail",await new StreamReader(context.Response.Body).ReadToEndAsync());
 }
}
