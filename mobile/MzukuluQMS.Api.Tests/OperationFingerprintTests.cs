using Microsoft.AspNetCore.Http;
using System.Text;
using MzukuluQMS.Api.Security;
namespace MzukuluQMS.Api.Tests;
public sealed class OperationFingerprintTests
{
 private static async Task<string> Fingerprint(string body,string route="/api/qc-forms/101/submit")
 {
  var context=new DefaultHttpContext();context.Request.Method="POST";context.Request.Path=route;context.Request.Headers["Idempotency-Key"]=Guid.NewGuid().ToString();context.Request.Headers.IfMatch="\"4\"";context.Request.Body=new MemoryStream(Encoding.UTF8.GetBytes(body));
  await new OperationFingerprintMiddleware(_=>Task.CompletedTask).InvokeAsync(context);
  Assert.Equal(0,context.Request.Body.Position);return (string)context.Items["QmsOperationFingerprint"]!;
 }
 [Fact]public async Task Identical_bytes_are_stable_and_changed_request_or_resource_differs()
 {Assert.Equal(await Fingerprint("{}"),await Fingerprint("{}"));Assert.NotEqual(await Fingerprint("{}"),await Fingerprint("{\"reason\":\"changed\"}"));Assert.NotEqual(await Fingerprint("{}"),await Fingerprint("{}","/api/qc-forms/102/submit"));}
 [Fact]public async Task Invalid_key_is_rejected()
 {var context=new DefaultHttpContext();context.Request.Method="POST";context.Request.Path="/api/qc-forms";context.Request.Headers["Idempotency-Key"]="invalid";await Assert.ThrowsAsync<ArgumentException>(()=>new OperationFingerprintMiddleware(_=>Task.CompletedTask).InvokeAsync(context));}
}
