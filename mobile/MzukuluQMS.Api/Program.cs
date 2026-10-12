using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
builder.Services.AddScoped<NcrService>();
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
    SupabaseEvidenceStorage>();

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
        options.TokenValidationParameters=new TokenValidationParameters
        {
            ValidateIssuer=true, ValidateAudience=true, ValidateLifetime=true, ValidateIssuerSigningKey=true,
            ValidAlgorithms=new[] {SecurityAlgorithms.EcdsaSha256}, NameClaimType="sub"
        };
    });

builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in new[] { "ReadProjects", "ManageProjects", "ReadClients", "ReadTemplates", "ManageTemplates", "DatabaseHealth" })
        options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(permission)));

    options.AddPolicy(
        "QmsUser",
        policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("qms_user_id");
            policy.RequireClaim("qms_role");
        });

    options.AddPolicy(
        "QmsAdmin",
        policy =>
        {
            policy.RequireClaim("qms_role", "Admin");
        });
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();



var app = builder.Build();
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<OperationFingerprintMiddleware>();

app.MapControllers();

app.Run();

// Expose the real startup pipeline to in-process HTTP/DI verification.
public partial class Program { }
