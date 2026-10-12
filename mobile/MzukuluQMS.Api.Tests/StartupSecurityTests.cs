using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MzukuluQMS.Api.Services;
using MzukuluQMS.Api.Services.Workflow;
using MzukuluQMS.Api.Services.SignOffs;
using MzukuluQMS.Api.Services.Evidence;
namespace MzukuluQMS.Api.Tests;
public sealed class StartupSecurityTests
{
 private sealed class App : WebApplicationFactory<Program>
 {
  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
   builder.ConfigureAppConfiguration((_,configuration)=>configuration.AddInMemoryCollection(new Dictionary<string,string?> {
    ["ConnectionStrings:QmsDatabase"]="Host=localhost;Database=qms_test_unconnected;Username=test;Password=test",
    ["SupabaseAuth:MetadataAddress"]="https://example.invalid/.well-known/openid-configuration",["SupabaseAuth:Audience"]="authenticated"
   }));
  }
 }
 [Theory][InlineData("/api/projects")][InlineData("/api/clients")][InlineData("/api/checklist-templates")][InlineData("/api/qc-forms/101")][InlineData("/api/quality/notifications")]
 public async Task Anonymous_requests_are_401_in_real_pipeline(string route)
 {
  await using var app=new App();using var client=app.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});using var response=await client.GetAsync(route);Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
 }
 [Fact]public async Task Critical_service_graph_resolves_without_database_access()
 {
  await using var app=new App();await using var scope=app.Services.CreateAsyncScope();
  Assert.NotNull(scope.ServiceProvider.GetRequiredService<IQCFormService>());
  Assert.NotNull(scope.ServiceProvider.GetRequiredService<IQCSignOffService>());
  Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEvidenceService>());
  Assert.NotNull(scope.ServiceProvider.GetRequiredService<QualityReporting>());
 }
}
