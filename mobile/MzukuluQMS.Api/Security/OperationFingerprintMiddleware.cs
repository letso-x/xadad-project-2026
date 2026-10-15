using System.Security.Cryptography;
using System.Text;
namespace MzukuluQMS.Api.Security;
public sealed class OperationFingerprintMiddleware(RequestDelegate next)
{
 public async Task InvokeAsync(HttpContext context)
 {
  var request=context.Request;
  if(request.Path.StartsWithSegments("/api/qc-forms") && request.Headers.ContainsKey("Idempotency-Key") && request.Method is "POST" or "PUT")
  {
   if(!Guid.TryParse(request.Headers["Idempotency-Key"],out var key)) throw new ArgumentException("Idempotency-Key must be a UUID.");
   request.EnableBuffering();
   using var digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
   digest.AppendData(Encoding.UTF8.GetBytes(request.Method+"\n"+request.Path+"\n"+request.Headers.IfMatch+"\n"));
   var buffer=new byte[8192]; int count; long length=0;
   try {
    while((count=await request.Body.ReadAsync(buffer,context.RequestAborted))>0)
    {
     length+=count; if(length>21*1024*1024) throw new ArgumentException("Operation exceeds request size limit.");
     digest.AppendData(buffer,0,count);
    }
   } finally {request.Body.Position=0;}
   context.Items["QmsOperationKey"]=key;
   context.Items["QmsOperationFingerprint"]=Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
  }
  await next(context);
 }
}
