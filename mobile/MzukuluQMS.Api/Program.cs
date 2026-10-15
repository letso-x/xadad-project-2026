using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using MzukuluQMS.Api.Configuration;
using MzukuluQMS.Api.Data;
using MzukuluQMS.Api.Data.Repositories;
using MzukuluQMS.Api.Exceptions;
using MzukuluQMS.Api.Repositories.Users;
using MzukuluQMS.Api.Security;
using MzukuluQMS.Api.Services;
using MzukuluQMS.Api.Services.Evidence;
using MzukuluQMS.Api.Services.SignOffs;
using MzukuluQMS.Api.Services.Users;
using MzukuluQMS.Api.Services.Workflow;


var builder = WebApplication.CreateBuilder(args);
// Portable process logs for managed hosting and local Visual Studio; no Windows Event Log dependency.
builder.Logging.ClearProviders().AddConsole().AddDebug();

builder.Services.Configure<ForwardedHeadersOptions>(options=>
{
    options.ForwardedHeaders=ForwardedHeaders.XForwardedFor|ForwardedHeaders.XForwardedProto;
    options.ForwardLimit=1; // Retain loopback-only trust; verify the immediate proxy before expanding it.
});

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<IChecklistTemplateRepository,ChecklistTemplateRepository>();
builder.Services.AddScoped<IChecklistTemplateService,ChecklistTemplateService>();
builder.Services.AddScoped<QmsDbSession>();
builder.Services.AddScoped<IQCWorkflow, QCWorkflow>();
builder.Services.AddScoped<QCFormQueries>();
builder.Services.AddScoped<EvidenceContentService>();
builder.Services.AddScoped<NcrService>();
builder.Services.AddScoped<TemplateAuthoring>();
builder.Services.AddScoped<QualityReporting>();
builder.Services.AddScoped<IQCFormRepository,QCFormRepository>();
builder.Services.AddScoped<IQCFormService,QCFormService>();
builder.Services.AddSingleton<IFieldValueValidator,FieldValueValidator>();
builder.Services.AddScoped<IClientRepository,ClientRepository>();
builder.Services.AddScoped<IClientService,ClientService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddScoped<
    IEvidenceRepository,
    EvidenceRepository>();

builder.Services.AddScoped<
    IEvidenceService,
    EvidenceService>();

builder.Services.AddScoped<
    IClaimsTransformation,
    QmsClaimsTransformation>();

builder.Services.Configure<SupabaseStorageOptions>(
    builder.Configuration.GetSection(
        SupabaseStorageOptions.SectionName));

builder.Services.AddHttpClient<
    IEvidenceStorage,
    SupabaseEvidenceStorage>().RemoveAllLoggers(); // Private evidence paths must not enter HttpClient logs.

builder.Services.AddScoped<
    IQCSignOffRepository,
    QCSignOffRepository>();

builder.Services.AddScoped<
    IQCSignOffService,
    QCSignOffService>();

builder.Services.AddScoped<
    IQCFormContentHashService,
    QCFormContentHashService>();


var supabaseAuthSection =
    builder.Configuration.GetSection(
        SupabaseAuthOptions.SectionName);

builder.Services.AddOptions<SupabaseAuthOptions>()
    .Bind(supabaseAuthSection)
    .Validate(options => Uri.TryCreate(options.MetadataAddress, UriKind.Absolute, out var metadata) && metadata.Scheme == Uri.UriSchemeHttps,
        "SupabaseAuth:MetadataAddress must be an absolute HTTPS URL.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "SupabaseAuth:Audience is required.")
    .ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<SupabaseAuthOptions>>((options, configured) =>
    {
        var auth=configured.Value;
        options.MetadataAddress=auth.MetadataAddress;
        options.Audience=auth.Audience;
        options.RequireHttpsMetadata=true;
        options.MapInboundClaims=false;
        options.IncludeErrorDetails=false;
        options.Events=new JwtBearerEvents
        {
            OnAuthenticationFailed=context=>
            {
                context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("QmsAuthentication")
                    .LogWarning("JWT authentication failed ({FailureType}); trace {TraceId}.",context.Exception.GetType().Name,context.HttpContext.TraceIdentifier);
                return Task.CompletedTask;
            }
        };
        options.TokenValidationParameters=new TokenValidationParameters
        {
            ValidateIssuer=true, ValidateAudience=true, ValidateLifetime=true, ValidateIssuerSigningKey=true,
            ValidAlgorithms=new[] {SecurityAlgorithms.EcdsaSha256}, NameClaimType="sub"
        };
    });

builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddAuthorization(MfaPolicies.Configure);

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();



var app = builder.Build();
if(app.Environment.IsProduction())ProductionConfiguration.Validate(app.Configuration);
app.UseForwardedHeaders();
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Production HTTPS terminates at the managed hosting front end (Azure HTTPS Only
// or an ALB HTTPS listener). Internal HTTP health checks must not redirect in a loop.
if(app.Environment.IsDevelopment())app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<OperationFingerprintMiddleware>();

app.MapControllers();
app.MapGet("/health",()=>Results.Ok(new {status="ok"})).AllowAnonymous();

app.Run();

// Expose the real startup pipeline to in-process HTTP/DI verification.
public partial class Program { }
