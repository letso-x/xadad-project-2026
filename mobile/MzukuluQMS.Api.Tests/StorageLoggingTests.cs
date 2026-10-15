using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MzukuluQMS.Api.Services.Evidence;
namespace MzukuluQMS.Api.Tests;
public sealed class StorageLoggingTests
{
 [Theory][InlineData("test-server-secret",true)][InlineData("sb_secret_synthetic_test_only",false)]
 public async Task Typed_storage_client_uses_key_format_without_logging_private_paths_or_keys(string key,bool bearerExpected)
 {
  var logs=new Capture();await using var app=new App(logs,key,bearerExpected);await using var scope=app.Services.CreateAsyncScope();
  var storage=scope.ServiceProvider.GetRequiredService<IEvidenceStorage>();using var bytes=new MemoryStream(new byte[]{1,2,3});
  var result=await storage.UploadAsync(bytes,"synthetic.png","image/png",123,null);
  await storage.DeleteAsync(result.Bucket,result.Path);
  Assert.Equal(new byte[]{1,2,3},await storage.DownloadAsync(result.Bucket,result.Path));
  Assert.Equal("private-test",result.Bucket);Assert.StartsWith("qc-forms/123/form/",result.Path);
  Assert.DoesNotContain(logs.Messages,m=>m.Contains("storage.invalid",StringComparison.Ordinal)||m.Contains(result.Path,StringComparison.Ordinal)||m.Contains(key,StringComparison.Ordinal));
 }
 private sealed class App(Capture capture,string key,bool bearerExpected):WebApplicationFactory<Program>
 {
  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
   builder.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:QmsDatabase"]="Host=localhost;Database=unconnected;Username=test;Password=test",["SupabaseAuth:MetadataAddress"]="https://example.invalid/.well-known/openid-configuration",["SupabaseAuth:Audience"]="authenticated",["SupabaseStorage:BaseUrl"]="https://storage.invalid",["SupabaseStorage:Bucket"]="private-test",["SupabaseStorage:SecretKey"]=key}));
   builder.ConfigureServices(s=>{s.AddLogging(l=>l.AddProvider(capture));s.AddHttpClient<IEvidenceStorage,SupabaseEvidenceStorage>().ConfigurePrimaryHttpMessageHandler(()=>new Reply(key,bearerExpected));});
  }
 }
 private sealed class Reply(string key,bool bearerExpected):HttpMessageHandler
 {
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
  {Assert.Contains(request.Method==HttpMethod.Get?"/storage/v1/object/authenticated/private-test/":"/storage/v1/object/private-test/",request.RequestUri!.AbsolutePath);Assert.Equal(key,request.Headers.GetValues("apikey").Single());if(bearerExpected)Assert.Equal(key,request.Headers.Authorization!.Parameter);else Assert.Null(request.Headers.Authorization);return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(new byte[]{1,2,3})});}
 }
 private sealed class Capture:ILoggerProvider
 {
  public ConcurrentQueue<string> Messages {get;}=new();public ILogger CreateLogger(string categoryName)=>new Sink(Messages);public void Dispose(){}
  private sealed class Sink(ConcurrentQueue<string> messages):ILogger
  {public IDisposable? BeginScope<TState>(TState state) where TState:notnull=>null;public bool IsEnabled(LogLevel level)=>true;public void Log<TState>(LogLevel level,EventId id,TState state,Exception? error,Func<TState,Exception?,string> format)=>messages.Enqueue(format(state,error));}
 }
}
