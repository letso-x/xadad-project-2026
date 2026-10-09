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

builder.Services.Configure<SupabaseAuthOptions>(
    supabaseAuthSection);

var supabaseAuth =
    supabaseAuthSection.Get<SupabaseAuthOptions>()
    ?? throw new InvalidOperationException(
        "Supabase authentication configuration is missing.");

if (string.IsNullOrWhiteSpace(
        supabaseAuth.MetadataAddress))
{
    throw new InvalidOperationException(
        "SupabaseAuth:MetadataAddress is required.");
}

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MetadataAddress =
            supabaseAuth.MetadataAddress;

        options.Audience =
            supabaseAuth.Audience;

        options.RequireHttpsMetadata = true;

        options.MapInboundClaims = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                NameClaimType = "sub"
            };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "QmsUser",
        policy =>
        {
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

app.MapControllers();

app.Run();
