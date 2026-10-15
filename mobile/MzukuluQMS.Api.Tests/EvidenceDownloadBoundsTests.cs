using System.Net;
using Microsoft.Extensions.Options;
using MzukuluQMS.Api.Configuration;
using MzukuluQMS.Api.Services.Evidence;
namespace MzukuluQMS.Api.Tests;
public sealed class EvidenceDownloadBoundsTests
{
 [Theory][InlineData(true)][InlineData(false)]
 public async Task Oversized_private_download_is_rejected_with_or_without_content_length(bool lengthKnown)
 {
  var content=new OversizedContent(lengthKnown);using var client=new HttpClient(new Reply(content));
  var storage=new SupabaseEvidenceStorage(client,Options.Create(new SupabaseStorageOptions{BaseUrl="https://storage.invalid",SecretKey="sb_secret_synthetic_only",Bucket="private-test"}));
  await Assert.ThrowsAsync<InvalidOperationException>(()=>storage.DownloadAsync("private-test","synthetic.png"));
  Assert.Equal(!lengthKnown,content.Opened);
 }
 private sealed class Reply(HttpContent content):HttpMessageHandler
 {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=content});}
 private sealed class OversizedContent(bool known):HttpContent
 {
  public bool Opened{get;private set;}
  protected override bool TryComputeLength(out long length){length=20*1024*1024+1;return known;}
  protected override Task SerializeToStreamAsync(Stream stream,TransportContext? context)=>throw new NotSupportedException();
  protected override Task<Stream> CreateContentReadStreamAsync(){Opened=true;return Task.FromResult<Stream>(new MemoryStream(new byte[20*1024*1024+1]));}
 }
}
