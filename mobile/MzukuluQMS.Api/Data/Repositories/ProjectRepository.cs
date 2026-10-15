using MzukuluQMS.Api.Services.Users;
﻿using Dapper;
using MzukuluQMS.Api.DTOs;

namespace MzukuluQMS.Api.Data.Repositories;

public sealed class ProjectRepository : IProjectRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ICurrentUserService _users;

    public ProjectRepository(IDbConnectionFactory connectionFactory, ICurrentUserService users)
    {
        _connectionFactory = connectionFactory;
        _users = users;
    }

    public async Task<IReadOnlyList<ProjectDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                (SELECT c."ClientName" FROM public."Client" c WHERE c."ClientID"="Project"."ClientID") AS "ClientName",
                CASE WHEN EXISTS(SELECT 1 FROM public."User" u JOIN qms_security."RolePermission" rp ON rp."RoleName"=u."Role"::text WHERE u."UserID"=@UserID AND u."IsActive" AND rp."Permission"='ReadInspection')
                  THEN (SELECT count(*) FROM public."QCForm" q WHERE q."ProjectID"="Project"."ProjectID") END AS "QCFormCount",
                "ProjectID",
                "ClientID",
                "ProjectNumber",
                "ProjectName",
                "Description",
                "ContractOrderNumber",
                "EnclosureNumber",
                "CabinetNumber",
                "SiteName",
                "SiteLocation",
                "StartDate",
                "EndDate",
                "IsActive",
                "Status",
                "CreatedAt",
                "UpdatedAt"
            FROM public."Project"
            WHERE EXISTS(SELECT 1 FROM qms_security."ProjectAccess" pa WHERE pa."ProjectID"="Project"."ProjectID" AND pa."UserID"=@UserID)
            ORDER BY "CreatedAt" DESC, "ProjectID" DESC;
            """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new { (await _users.GetCurrentUserAsync(cancellationToken)).UserID },
            cancellationToken: cancellationToken);

        var projects = await connection.QueryAsync<ProjectDto>(command);

        return projects.AsList();
    }

    public async Task<ProjectDto?> GetByIdAsync(
    long projectId,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            (SELECT c."ClientName" FROM public."Client" c WHERE c."ClientID"="Project"."ClientID") AS "ClientName",
                CASE WHEN EXISTS(SELECT 1 FROM public."User" u JOIN qms_security."RolePermission" rp ON rp."RoleName"=u."Role"::text WHERE u."UserID"=@UserID AND u."IsActive" AND rp."Permission"='ReadInspection')
                  THEN (SELECT count(*) FROM public."QCForm" q WHERE q."ProjectID"="Project"."ProjectID") END AS "QCFormCount",
                "ProjectID",
            "ClientID",
            "ProjectNumber",
            "ProjectName",
            "Description",
            "ContractOrderNumber",
            "EnclosureNumber",
            "CabinetNumber",
            "SiteName",
            "SiteLocation",
            "StartDate",
            "EndDate",
            "IsActive",
            "Status",
            "CreatedAt",
            "UpdatedAt"
        FROM public."Project"
        WHERE "ProjectID" = @ProjectID
          AND EXISTS(SELECT 1 FROM qms_security."ProjectAccess" pa WHERE pa."ProjectID"=@ProjectID AND pa."UserID"=@UserID);
        """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new { ProjectID = projectId, (await _users.GetCurrentUserAsync(cancellationToken)).UserID },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<ProjectDto>(command);
    }
    public async Task<ProjectDto> CreateAsync(
    CreateProjectRequest request,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        INSERT INTO public."Project"
        (
            "ClientID",
            "ProjectNumber",
            "ProjectName",
            "Description",
            "ContractOrderNumber",
            "EnclosureNumber",
            "CabinetNumber",
            "SiteName",
            "SiteLocation",
            "StartDate",
            "EndDate"
        )
        VALUES
        (
            @ClientID,
            @ProjectNumber,
            @ProjectName,
            @Description,
            @ContractOrderNumber,
            @EnclosureNumber,
            @CabinetNumber,
            @SiteName,
            @SiteLocation,
            @StartDate,
            @EndDate
        )
        RETURNING
            "ProjectID",
            "ClientID",
            "ProjectNumber",
            "ProjectName",
            "Description",
            "ContractOrderNumber",
            "EnclosureNumber",
            "CabinetNumber",
            "SiteName",
            "SiteLocation",
            "StartDate",
            "EndDate",
            "IsActive",
            "Status",
            "CreatedAt",
            "UpdatedAt";
        """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var actor=await _users.GetCurrentUserAsync(cancellationToken);
        await using var transaction=await connection.BeginTransactionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                request.ClientID,
                request.ProjectNumber,
                request.ProjectName,
                request.Description,
                request.ContractOrderNumber,
                request.EnclosureNumber,
                request.CabinetNumber,
                request.SiteName,
                request.SiteLocation,
                request.StartDate,
                request.EndDate
            },
            transaction, cancellationToken: cancellationToken);

        var project=await connection.QuerySingleAsync<ProjectDto>(command);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO qms_security."ProjectAccess" ("UserID","ProjectID") VALUES(@userId,@projectId) ON CONFLICT DO NOTHING;
            INSERT INTO qms_security."AuditEvent" ("ActorUserID","Action","EntityType","EntityID","Details")
            VALUES(@userId,'ProjectCreated','Project',@projectId,'{}'::jsonb)
            """,new {userId=actor.UserID,projectId=project.ProjectID},transaction,cancellationToken:cancellationToken));
        await transaction.CommitAsync(cancellationToken);return project;
    }
}