using Dapper;
using MzukuluQMS.Api.DTOs;
using System.Text.Json;

namespace MzukuluQMS.Api.Data.Repositories;

public sealed class ChecklistTemplateRepository
    : IChecklistTemplateRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ChecklistTemplateRepository(
        IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ChecklistTemplateDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                "ChecklistTemplateID",
                "TemplateNumber",
                "TemplateName",
                "Subtitle",
                "IsActive"
            FROM public."ChecklistTemplate"
            ORDER BY "TemplateName";
            """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            cancellationToken: cancellationToken);

        var templates =
            await connection.QueryAsync<ChecklistTemplateDto>(command);

        return templates.AsList();
    }

    private sealed class ChecklistTemplateVersionRow
    {
        public long ChecklistTemplateVersionID { get; init; }
        public long ChecklistTemplateID { get; init; }

        public string? TemplateNumber { get; init; }
        public string TemplateName { get; init; } = string.Empty;
        public string? Subtitle { get; init; }
        public bool IsTemplateActive { get; init; }

        public string Revision { get; init; } = string.Empty;
        public DateTime? IssueDate { get; init; }
        public DateTime? EffectiveDate { get; init; }
        public string Status { get; init; } = string.Empty;

        public string? Scope { get; init; }
        public string? AcceptanceCriteria { get; init; }
    }

    private sealed class ChecklistTemplateProjectFieldRow
    {
        public long ChecklistTemplateProjectFieldID { get; init; }

        public string FieldKey { get; init; } = string.Empty;
        public string FieldLabel { get; init; } = string.Empty;
        public string FieldType { get; init; } = string.Empty;

        public string? Placeholder { get; init; }
        public string? HelpText { get; init; }

        public bool IsRequired { get; init; }

        public string? OptionsJson { get; init; }
        public string? ValidationRulesJson { get; init; }

        public int DisplayOrder { get; init; }
        public bool IsActive { get; init; }
    }

    private sealed class ChecklistSectionRow
    {
        public long ChecklistSectionID { get; init; }

        public string SectionName { get; init; } = string.Empty;
        public string? Description { get; init; }

        public int DisplayOrder { get; init; }
        public bool IsRequired { get; init; }
    }

    private sealed class ChecklistFieldRow
    {
        public long ChecklistFieldID { get; init; }
        public long ChecklistSectionID { get; init; }

        public string FieldKey { get; init; } = string.Empty;
        public string FieldLabel { get; init; } = string.Empty;
        public string FieldType { get; init; } = string.Empty;

        public string? Placeholder { get; init; }
        public string? HelpText { get; init; }

        public bool IsRequired { get; init; }
        public bool RequiresPhoto { get; init; }
        public bool RequiresSignature { get; init; }

        public string? OptionsJson { get; init; }
        public string? ValidationRulesJson { get; init; }

        public int DisplayOrder { get; init; }
        public bool IsActive { get; init; }
    }
    public async Task<ChecklistTemplateVersionDefinitionDto?> GetVersionDefinitionAsync(
    long checklistTemplateVersionId,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            v."ChecklistTemplateVersionID",
            v."ChecklistTemplateID",
            t."TemplateNumber",
            t."TemplateName",
            t."Subtitle",
            t."IsActive" AS "IsTemplateActive",
            v."Revision",
            v."IssueDate",
            v."EffectiveDate",
            v."Status"::text AS "Status",
            v."Scope",
            v."AcceptanceCriteria"
        FROM public."ChecklistTemplateVersion" v
        INNER JOIN public."ChecklistTemplate" t
            ON t."ChecklistTemplateID" = v."ChecklistTemplateID"
        WHERE v."ChecklistTemplateVersionID" =
            @ChecklistTemplateVersionID;

        SELECT
            "ChecklistTemplateProjectFieldID",
            "FieldKey",
            "FieldLabel",
            "FieldType"::text AS "FieldType",
            "Placeholder",
            "HelpText",
            "IsRequired",
            "Options"::text AS "OptionsJson",
            "ValidationRules"::text AS "ValidationRulesJson",
            "DisplayOrder",
            "IsActive"
        FROM public."ChecklistTemplateProjectField"
        WHERE "ChecklistTemplateVersionID" =
            @ChecklistTemplateVersionID
        ORDER BY
            "DisplayOrder",
            "ChecklistTemplateProjectFieldID";

        SELECT
            "ChecklistSectionID",
            "SectionName",
            "Description",
            "DisplayOrder",
            "IsRequired"
        FROM public."ChecklistSection"
        WHERE "ChecklistTemplateVersionID" =
            @ChecklistTemplateVersionID
        ORDER BY
            "DisplayOrder",
            "ChecklistSectionID";

        SELECT
            f."ChecklistFieldID",
            f."ChecklistSectionID",
            f."FieldKey",
            f."FieldLabel",
            f."FieldType"::text AS "FieldType",
            f."Placeholder",
            f."HelpText",
            f."IsRequired",
            f."RequiresPhoto",
            f."RequiresSignature",
            f."Options"::text AS "OptionsJson",
            f."ValidationRules"::text AS "ValidationRulesJson",
            f."DisplayOrder",
            f."IsActive"
        FROM public."ChecklistField" f
        INNER JOIN public."ChecklistSection" s
            ON s."ChecklistSectionID" = f."ChecklistSectionID"
        WHERE s."ChecklistTemplateVersionID" =
            @ChecklistTemplateVersionID
        ORDER BY
            s."DisplayOrder",
            f."DisplayOrder",
            f."ChecklistFieldID";
        """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                ChecklistTemplateVersionID =
                    checklistTemplateVersionId
            },
            cancellationToken: cancellationToken);

        using var multi =
            await connection.QueryMultipleAsync(command);

        var version =
            (await multi.ReadAsync<ChecklistTemplateVersionRow>())
            .SingleOrDefault();

        var projectFieldRows =
            (await multi.ReadAsync<ChecklistTemplateProjectFieldRow>())
            .AsList();

        var sectionRows =
            (await multi.ReadAsync<ChecklistSectionRow>())
            .AsList();

        var fieldRows =
            (await multi.ReadAsync<ChecklistFieldRow>())
            .AsList();

        if (version is null)
        {
            return null;
        }

        var fieldsBySection =
            fieldRows.ToLookup(field => field.ChecklistSectionID);

        var sections =
            sectionRows
                .Select(section => new ChecklistSectionDto
                {
                    ChecklistSectionID =
                        section.ChecklistSectionID,

                    SectionName =
                        section.SectionName,

                    Description =
                        section.Description,

                    DisplayOrder =
                        section.DisplayOrder,

                    IsRequired =
                        section.IsRequired,

                    Fields = fieldsBySection[
                            section.ChecklistSectionID]
                        .Select(MapChecklistField)
                        .ToList()
                })
                .ToList();

        var projectFields =
            projectFieldRows
                .Select(MapProjectField)
                .ToList();

        return new ChecklistTemplateVersionDefinitionDto
        {
            ChecklistTemplateVersionID =
                version.ChecklistTemplateVersionID,

            ChecklistTemplateID =
                version.ChecklistTemplateID,

            TemplateNumber =
                version.TemplateNumber,

            TemplateName =
                version.TemplateName,

            Subtitle =
                version.Subtitle,

            IsTemplateActive =
                version.IsTemplateActive,

            Revision =
                version.Revision,

            IssueDate =
                version.IssueDate,

            EffectiveDate =
                version.EffectiveDate,

            Status =
                version.Status,

            Scope =
                version.Scope,

            AcceptanceCriteria =
                version.AcceptanceCriteria,

            ProjectFields =
                projectFields,

            Sections =
                sections
        };
    }
    private static ChecklistTemplateProjectFieldDto MapProjectField(
    ChecklistTemplateProjectFieldRow row)
    {
        return new ChecklistTemplateProjectFieldDto
        {
            ChecklistTemplateProjectFieldID =
                row.ChecklistTemplateProjectFieldID,

            FieldKey =
                row.FieldKey,

            FieldLabel =
                row.FieldLabel,

            FieldType =
                row.FieldType,

            Placeholder =
                row.Placeholder,

            HelpText =
                row.HelpText,

            IsRequired =
                row.IsRequired,

            Options =
                ParseJson(row.OptionsJson),

            ValidationRules =
                ParseJson(row.ValidationRulesJson),

            DisplayOrder =
                row.DisplayOrder,

            IsActive =
                row.IsActive
        };
    }

    private static ChecklistFieldDto MapChecklistField(
        ChecklistFieldRow row)
    {
        return new ChecklistFieldDto
        {
            ChecklistFieldID =
                row.ChecklistFieldID,

            FieldKey =
                row.FieldKey,

            FieldLabel =
                row.FieldLabel,

            FieldType =
                row.FieldType,

            Placeholder =
                row.Placeholder,

            HelpText =
                row.HelpText,

            IsRequired =
                row.IsRequired,

            RequiresPhoto =
                row.RequiresPhoto,

            RequiresSignature =
                row.RequiresSignature,

            Options =
                ParseJson(row.OptionsJson),

            ValidationRules =
                ParseJson(row.ValidationRulesJson),

            DisplayOrder =
                row.DisplayOrder,

            IsActive =
                row.IsActive
        };
    }

    private static JsonElement? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document =
            JsonDocument.Parse(json);

        return document.RootElement.Clone();
    }
}