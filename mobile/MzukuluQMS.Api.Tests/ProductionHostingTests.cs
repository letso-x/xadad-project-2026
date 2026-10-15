using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MzukuluQMS.Api.Configuration;
using MzukuluQMS.Api.Exceptions;
using Npgsql;
namespace MzukuluQMS.Api.Tests;
public sealed class ProductionHostingTests
{
 static Dictionary<string,string?> Values(string ssl="VerifyFull")=>new(){["ConnectionStrings:QmsDatabase"]=$"Host=db.invalid;Database=qms;Username=test;Password=synthetic-only;SSL Mode={ssl}",["SupabaseAuth:MetadataAddress"]="https://auth.invalid/auth/v1/.well-known/openid-configuration",["SupabaseAuth:Audience"]="authenticated",["SupabaseStorage:BaseUrl"]="https://storage.invalid",["SupabaseStorage:SecretKey"]="synthetic-only",["SupabaseStorage:Bucket"]="private-test"};
 sealed class App:WebApplicationFactory<Program>
 {
  protected override void ConfigureWebHost(IWebHostBuilder builder){builder.UseEnvironment("Production");builder.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(Values()));}
 }
 [Fact]public async Task Production_health_is_anonymous_non_sensitive_and_does_not_require_database_or_https_backend()
 {await using var app=new App();using var client=app.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("http://localhost"),AllowAutoRedirect=false});using var health=await client.GetAsync("/health");Assert.Equal(HttpStatusCode.OK,health.StatusCode);Assert.Equal("{\"status\":\"ok\"}",await health.Content.ReadAsStringAsync());using var protectedResponse=await client.GetAsync("/api/projects");Assert.Equal(HttpStatusCode.Unauthorized,protectedResponse.StatusCode);}
 [Theory][InlineData("Disable")][InlineData("Prefer")][InlineData("Require")][InlineData("VerifyCA")]
 public void Production_requires_hostname_validated_encryption(string ssl)
 {var error=Assert.Throws<InvalidOperationException>(()=>ProductionConfiguration.Validate(new ConfigurationBuilder().AddInMemoryCollection(Values(ssl)).Build()));Assert.Contains("VerifyFull",error.Message);Assert.DoesNotContain("synthetic-only",error.Message);}
 [Theory][InlineData("ConnectionStrings:QmsDatabase")][InlineData("SupabaseStorage:SecretKey")][InlineData("SupabaseStorage:Bucket")][InlineData("SupabaseStorage:BaseUrl")]
 public void Missing_production_configuration_fails_without_value_disclosure(string key)
 {var values=Values();values[key]="";var error=Assert.Throws<InvalidOperationException>(()=>ProductionConfiguration.Validate(new ConfigurationBuilder().AddInMemoryCollection(values).Build()));Assert.DoesNotContain("synthetic-only",error.Message);}
 [Fact]public void Missing_CA_file_is_rejected_without_exposing_connection_string()
 {var values=Values();values["ConnectionStrings:QmsDatabase"]+=";Root Certificate=missing-synthetic-ca.crt";var error=Assert.Throws<InvalidOperationException>(()=>ProductionConfiguration.Validate(new ConfigurationBuilder().AddInMemoryCollection(values).Build()));Assert.Contains("Root Certificate",error.Message);Assert.DoesNotContain("synthetic-only",error.Message);}
 [Theory][InlineData("127.0.0.1","https")][InlineData("203.0.113.25","http")]
 public async Task Forwarded_scheme_is_accepted_only_from_trusted_local_proxy(string remote,string expected)
 {var options=new ForwardedHeadersOptions{ForwardedHeaders=ForwardedHeaders.XForwardedFor|ForwardedHeaders.XForwardedProto,ForwardLimit=1};var context=new DefaultHttpContext();context.Connection.RemoteIpAddress=IPAddress.Parse(remote);context.Request.Scheme="http";context.Request.Headers["X-Forwarded-Proto"]="https";var middleware=new ForwardedHeadersMiddleware(_=>Task.CompletedTask,NullLoggerFactory.Instance,Options.Create(options));await middleware.Invoke(context);Assert.Equal(expected,context.Request.Scheme);}
 sealed class CaptureLogger:ILogger<ApiExceptionHandler>
 {public List<string> Messages=[];public List<Exception?> Exceptions=[];public bool IsEnabled(LogLevel level)=>true;public IDisposable? BeginScope<TState>(TState state)where TState:notnull=>null;public void Log<TState>(LogLevel level,EventId id,TState state,Exception? error,Func<TState,Exception?,string> formatter){Messages.Add(formatter(state,error));Exceptions.Add(error);}}
 [Fact]public async Task Database_error_logs_do_not_include_provider_message_or_exception_payload()
 {var logger=new CaptureLogger();var context=new DefaultHttpContext{RequestServices=new ServiceCollection().AddOptions().BuildServiceProvider()};context.Response.Body=new MemoryStream();await new ApiExceptionHandler(logger).TryHandleAsync(context,new PostgresException("Password=synthetic-secret-do-not-log","ERROR","ERROR","28P01"),default);Assert.Contains(logger.Messages,m=>m.Contains("28P01"));Assert.DoesNotContain(logger.Messages,m=>m.Contains("synthetic-secret"));Assert.All(logger.Exceptions,e=>Assert.Null(e));}
 [Fact]public void Nested_environment_names_bind_to_existing_keys()
 {var name="SupabaseStorage__Bucket";var old=Environment.GetEnvironmentVariable(name);try{Environment.SetEnvironmentVariable(name,"synthetic-environment-bucket");var config=new ConfigurationBuilder().AddEnvironmentVariables().Build();Assert.Equal("synthetic-environment-bucket",config["SupabaseStorage:Bucket"]);}finally{Environment.SetEnvironmentVariable(name,old);}}
}
