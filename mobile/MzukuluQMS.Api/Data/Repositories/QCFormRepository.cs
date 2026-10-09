using System.Text.Json;
using Dapper;
using MzukuluQMS.Api.DTOs;

namespace MzukuluQMS.Api.Data.Repositories;

public sealed class QCFormRepository : IQCFormRepository
{
    private readonly IDbConnectionFactory _connectionFactory;


    public QCFormRepository(
        IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> ProjectExistsAsync(
        long projectId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS
            (
                SELECT 1
                FROM public."Project"
                WHERE "ProjectID" = @ProjectID
            );
            """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new { ProjectID = projectId },
            cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<bool>(command);
    }

    public async Task<long?> GetActiveTemplateVersionIdAsync(
        long checklistTemplateId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT v."ChecklistTemplateVersionID"
            FROM public."ChecklistTemplateVersion" v
            INNER JOIN public."ChecklistTemplate" t
                ON t."ChecklistTemplateID" =
                   v."ChecklistTemplateID"
            WHERE v."ChecklistTemplateID" =
                    @ChecklistTemplateID
              AND t."IsActive" = true
              AND v."Status" = 'Active'::template_status
            ORDER BY
                v."EffectiveDate" DESC NULLS LAST,
                v."IssueDate" DESC NULLS LAST,
                v."ChecklistTemplateVersionID" DESC
            LIMIT 1;
            """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                ChecklistTemplateID =
                    checklistTemplateId
            },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<long?>(
            command);
    }

    public async Task<QCFormDto> CreateAsync(
    long projectId,
    long checklistTemplateId,
    long checklistTemplateVersionId,
    Guid? startedByUserId,
    Guid? assignedToUserId,
    CancellationToken cancellationToken = default)
    {
        const string insertSql = """
        INSERT INTO public."QCForm"
        (
            "ProjectID",
            "ChecklistTemplateID",
            "ChecklistTemplateVersionID",
            "FormNumber",
            "Status",
            "StartedByUserID",
            "AssignedToUserID",
            "StartedAt"
        )
        VALUES
        (
            @ProjectID,
            @ChecklistTemplateID,
            @ChecklistTemplateVersionID,
            @TemporaryFormNumber,
            'Draft'::form_status,
            @StartedByUserID,
            @AssignedToUserID,
            now()
        )
        RETURNING "QCFormID";
        """;

        const string updateFormNumberSql = """
        UPDATE public."QCForm"
        SET
            "FormNumber" = @FormNumber,
            "UpdatedAt" = now()
        WHERE "QCFormID" = @QCFormID;
        """;

        const string generateSql = """
        SELECT public.generate_qcform_structure(
            @QCFormID,
            @ChecklistTemplateVersionID
        );
        """;

        const string selectSql = """
        SELECT
            "QCFormID",
            "ProjectID",
            "ChecklistTemplateID",
            "ChecklistTemplateVersionID",
            "FormNumber",
            "Status"::text AS "Status",
            "StartedByUserID",
            "AssignedToUserID",
            "StartedAt",
            "SubmittedAt",
            "CreatedAt",
            "UpdatedAt"
        FROM public."QCForm"
        WHERE "QCFormID" = @QCFormID;
        """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        await using var transaction =
            await connection.BeginTransactionAsync(
                cancellationToken);

        try
        {
            var temporaryFormNumber =
                $"TEMP-{Guid.NewGuid():N}";

            var insertCommand =
                new CommandDefinition(
                    insertSql,
                    new
                    {
                        ProjectID = projectId,
                        ChecklistTemplateID =
                            checklistTemplateId,

                        ChecklistTemplateVersionID =
                            checklistTemplateVersionId,

                        TemporaryFormNumber =
                            temporaryFormNumber,

                        StartedByUserID =
                            startedByUserId,

                        AssignedToUserID =
                            assignedToUserId
                    },
                    transaction,
                    cancellationToken:
                        cancellationToken);

            var qcFormId =
                await connection.QuerySingleAsync<long>(
                    insertCommand);

            var formNumber =
                $"QC-{qcFormId:D4}";

            var updateCommand =
                new CommandDefinition(
                    updateFormNumberSql,
                    new
                    {
                        QCFormID = qcFormId,
                        FormNumber = formNumber
                    },
                    transaction,
                    cancellationToken:
                        cancellationToken);

            await connection.ExecuteAsync(
                updateCommand);

            var generateCommand =
                new CommandDefinition(
                    generateSql,
                    new
                    {
                        QCFormID = qcFormId,

                        ChecklistTemplateVersionID =
                            checklistTemplateVersionId
                    },
                    transaction,
                    cancellationToken:
                        cancellationToken);

            await connection.ExecuteAsync(
                generateCommand);

            var selectCommand =
                new CommandDefinition(
                    selectSql,
                    new
                    {
                        QCFormID = qcFormId
                    },
                    transaction,
                    cancellationToken:
                        cancellationToken);

            var form =
                await connection.QuerySingleAsync<QCFormDto>(
                    selectCommand);

            await transaction.CommitAsync(
                cancellationToken);

            return form;
        }
        catch
        {
            await transaction.RollbackAsync(
                cancellationToken);

            throw;
        }
    }
    public async Task<QCFormDetailsDto?> GetByIdAsync(
    long qcFormId,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            "QCFormID",
            "ProjectID",
            "ChecklistTemplateID",
            "ChecklistTemplateVersionID",
            "FormNumber",
            "Status"::text AS "Status",
            "StartedByUserID",
            "AssignedToUserID",
            "StartedAt",
            "SubmittedAt",
            "CreatedAt",
            "UpdatedAt"
        FROM public."QCForm"
        WHERE "QCFormID" = @QCFormID;

        SELECT
            "QCFormProjectFieldID",
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
            "Value"::text AS "ValueJson"
        FROM public."QCFormProjectField"
        WHERE "QCFormID" = @QCFormID
        ORDER BY
            "DisplayOrder",
            "QCFormProjectFieldID";

        SELECT
            "QCFormSectionID",
            "ChecklistSectionID",
            "SectionName",
            "Description",
            "DisplayOrder"
        FROM public."QCFormSection"
        WHERE "QCFormID" = @QCFormID
        ORDER BY
            "DisplayOrder",
            "QCFormSectionID";

        SELECT
            f."QCFormFieldID",
            f."QCFormSectionID",
            f."ChecklistFieldID",
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
            r."QCResponseID",
            r."Value"::text AS "ValueJson",
            r."IsAnswered",
            r."AnsweredByUserID",
            r."AnsweredAt",
            r."UpdatedAt"
        FROM public."QCFormField" f
        INNER JOIN public."QCFormSection" s
            ON s."QCFormSectionID" = f."QCFormSectionID"
        INNER JOIN public."QCResponse" r
            ON r."QCFormFieldID" = f."QCFormFieldID"
           AND r."QCFormID" = s."QCFormID"
        WHERE s."QCFormID" = @QCFormID
        ORDER BY
            s."DisplayOrder",
            f."DisplayOrder",
            f."QCFormFieldID";
        """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new { QCFormID = qcFormId },
            cancellationToken: cancellationToken);

        using var multi =
            await connection.QueryMultipleAsync(command);

        var form =
            (await multi.ReadAsync<QCFormRow>())
            .SingleOrDefault();

        var projectFieldRows =
            (await multi.ReadAsync<QCFormProjectFieldRow>())
            .AsList();

        var sectionRows =
            (await multi.ReadAsync<QCFormSectionRow>())
            .AsList();

        var fieldRows =
            (await multi.ReadAsync<QCFormFieldRow>())
            .AsList();

        if (form is null)
        {
            return null;
        }

        var fieldsBySection =
            fieldRows.ToLookup(
                field => field.QCFormSectionID);

        var projectFields =
            projectFieldRows
                .Select(row => new QCFormProjectFieldDto
                {
                    QCFormProjectFieldID =
                        row.QCFormProjectFieldID,

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

                    Value =
                        ParseJson(row.ValueJson)
                })
                .ToList();

        var sections =
            sectionRows
                .Select(section => new QCFormSectionDto
                {
                    QCFormSectionID =
                        section.QCFormSectionID,

                    ChecklistSectionID =
                        section.ChecklistSectionID,

                    SectionName =
                        section.SectionName,

                    Description =
                        section.Description,

                    DisplayOrder =
                        section.DisplayOrder,

                    Fields = fieldsBySection[
                            section.QCFormSectionID]
                        .Select(MapQCFormField)
                        .ToList()
                })
                .ToList();

        return new QCFormDetailsDto
        {
            QCFormID =
                form.QCFormID,

            ProjectID =
                form.ProjectID,

            ChecklistTemplateID =
                form.ChecklistTemplateID,

            ChecklistTemplateVersionID =
                form.ChecklistTemplateVersionID,

            FormNumber =
                form.FormNumber,

            Status =
                form.Status,

            StartedByUserID =
                form.StartedByUserID,

            AssignedToUserID =
                form.AssignedToUserID,

            StartedAt =
                form.StartedAt,

            SubmittedAt =
                form.SubmittedAt,

            CreatedAt =
                form.CreatedAt,

            UpdatedAt =
                form.UpdatedAt,

            ProjectFields =
                projectFields,

            Sections =
                sections
        };
    }
    private static QCFormFieldDto MapQCFormField(
    QCFormFieldRow row)
    {
        return new QCFormFieldDto
        {
            QCFormFieldID =
                row.QCFormFieldID,

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

            QCResponseID =
                row.QCResponseID,

            Value =
                ParseJson(row.ValueJson),

            IsAnswered =
                row.IsAnswered,

            AnsweredByUserID =
                row.AnsweredByUserID,

            AnsweredAt =
                row.AnsweredAt,

            UpdatedAt =
                row.UpdatedAt
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

    public async Task UpdateResponseAsync(
    long qcFormId,
    long qcResponseId,
    string valueJson,
    Guid? answeredByUserId,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        UPDATE public."QCResponse"
        SET
            "Value" = CAST(@ValueJson AS jsonb),
            "IsAnswered" = true,
            "AnsweredByUserID" = @AnsweredByUserID,
            "AnsweredAt" = now(),
            "UpdatedAt" = now()
        WHERE "QCResponseID" = @QCResponseID
          AND "QCFormID" = @QCFormID;
        """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                QCFormID = qcFormId,
                QCResponseID = qcResponseId,
                ValueJson = valueJson,
                AnsweredByUserID = answeredByUserId
            },
            cancellationToken: cancellationToken);

        var affectedRows =
            await connection.ExecuteAsync(command);

        if (affectedRows != 1)
        {
            throw new KeyNotFoundException(
                "The QC response was not found.");
        }
    }

    public async Task UpdateProjectFieldAsync(
    long qcFormId,
    long qcFormProjectFieldId,
    string valueJson,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        UPDATE public."QCFormProjectField"
        SET
            "Value" = CAST(@ValueJson AS jsonb),
            "UpdatedAt" = now()
        WHERE "QCFormProjectFieldID" =
                @QCFormProjectFieldID
          AND "QCFormID" = @QCFormID;
        """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                QCFormID = qcFormId,
                QCFormProjectFieldID =
                    qcFormProjectFieldId,
                ValueJson = valueJson
            },
            cancellationToken: cancellationToken);

        var affectedRows =
            await connection.ExecuteAsync(command);

        if (affectedRows != 1)
        {
            throw new KeyNotFoundException(
                "The QC form project field was not found.");
        }
    }

    public async Task<string?> GetStatusAsync(
    long qcFormId,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT "Status"::text
        FROM public."QCForm"
        WHERE "QCFormID" = @QCFormID;
        """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                QCFormID = qcFormId
            },
            cancellationToken: cancellationToken);

        return await connection
            .QuerySingleOrDefaultAsync<string>(
                command);
    }
    public async Task SubmitAsync(
    long qcFormId,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        UPDATE public."QCForm"
        SET
            "Status" = CAST('Submitted' AS public.form_status),
            "SubmittedAt" = now(),
            "UpdatedAt" = now()
        WHERE "QCFormID" = @QCFormID;
        """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                QCFormID = qcFormId
            },
            cancellationToken: cancellationToken);

        var affectedRows =
            await connection.ExecuteAsync(
                command);

        if (affectedRows == 0)
        {
            throw new KeyNotFoundException(
                $"QC form {qcFormId} was not found.");
        }
    }
    public async Task<int> CountMissingRequiredProjectFieldsAsync(
    long qcFormId,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT COUNT(*)
        FROM public."QCFormProjectField"
        WHERE "QCFormID" = @QCFormID
          AND "IsRequired" = true
          AND (
                "Value" IS NULL
                OR "Value" = 'null'::jsonb
              );
        """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new { QCFormID = qcFormId },
            cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<int>(
            command);
    }
    public async Task<int> CountMissingRequiredResponsesAsync(
    long qcFormId,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT COUNT(*)
        FROM public."QCFormField" f
        INNER JOIN public."QCFormSection" s
            ON s."QCFormSectionID" = f."QCFormSectionID"
        LEFT JOIN public."QCResponse" r
            ON r."QCFormFieldID" = f."QCFormFieldID"
           AND r."QCFormID" = s."QCFormID"
        WHERE s."QCFormID" = @QCFormID
          AND f."IsRequired" = true
          AND f."FieldType"::text NOT IN ('Photo', 'Signature')
          AND (
                r."QCResponseID" IS NULL
                OR r."IsAnswered" = false
                OR r."Value" IS NULL
                OR r."Value" = 'null'::jsonb
              );
        """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new { QCFormID = qcFormId },
            cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<int>(
            command);
    }
   
    public async Task<int> CountMissingRequiredPhotoEvidenceAsync(
    long qcFormId,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT COUNT(*)
        FROM public."QCFormField" f
        INNER JOIN public."QCFormSection" s
            ON s."QCFormSectionID" = f."QCFormSectionID"
        WHERE s."QCFormID" = @QCFormID
          AND (
                f."FieldType"::text = 'Photo'
                OR f."RequiresPhoto" = true
              )
          AND NOT EXISTS (
                SELECT 1
                FROM public."Evidence" e
                WHERE e."QCFormID" = @QCFormID
                  AND e."QCFormFieldID" = f."QCFormFieldID"
                  AND e."EvidenceType" = CAST('Photo' AS public.evidence_type)
              );
        """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new { QCFormID = qcFormId },
            cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<int>(
            command);
    }
    public async Task<string> GetSignableContentAsync(
    long qcFormId,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT jsonb_build_object(
            'qcForm', (
                SELECT jsonb_build_object(
                    'qcFormID', q."QCFormID",
                    'projectID', q."ProjectID",
                    'checklistTemplateID', q."ChecklistTemplateID",
                    'checklistTemplateVersionID', q."ChecklistTemplateVersionID",
                    'formNumber', q."FormNumber",
                    'status', q."Status"::text,
                    'submittedAt', q."SubmittedAt"
                )
                FROM public."QCForm" q
                WHERE q."QCFormID" = @QCFormID
            ),

            'projectFields', (
                SELECT COALESCE(
                    jsonb_agg(
                        jsonb_build_object(
                            'qcFormProjectFieldID', pf."QCFormProjectFieldID",
                            'fieldKey', pf."FieldKey",
                            'value', pf."Value"
                        )
                        ORDER BY pf."QCFormProjectFieldID"
                    ),
                    '[]'::jsonb
                )
                FROM public."QCFormProjectField" pf
                WHERE pf."QCFormID" = @QCFormID
            ),

            'responses', (
                SELECT COALESCE(
                    jsonb_agg(
                        jsonb_build_object(
                            'qcResponseID', r."QCResponseID",
                            'qcFormFieldID', r."QCFormFieldID",
                            'value', r."Value",
                            'isAnswered', r."IsAnswered"
                        )
                        ORDER BY r."QCResponseID"
                    ),
                    '[]'::jsonb
                )
                FROM public."QCResponse" r
                WHERE r."QCFormID" = @QCFormID
            ),

            'evidence', (
                SELECT COALESCE(
                    jsonb_agg(
                        jsonb_build_object(
                            'evidenceID', e."EvidenceID",
                            'qcFormFieldID', e."QCFormFieldID",
                            'evidenceType', e."EvidenceType"::text,
                            'storageBucket', e."StorageBucket",
                            'storagePath', e."StoragePath",
                            'fileName', e."FileName",
                            'contentType', e."ContentType",
                            'fileSizeBytes', e."FileSizeBytes",
                            'capturedAt', e."CapturedAt",
                            'uploadedByUserID', e."UploadedByUserID"
                        )
                        ORDER BY e."EvidenceID"
                    ),
                    '[]'::jsonb
                )
                FROM public."Evidence" e
                WHERE e."QCFormID" = @QCFormID
                  AND e."EvidenceType" <> CAST('Signature' AS public.evidence_type)
            )
        )::text;
        """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                QCFormID = qcFormId
            },
            cancellationToken: cancellationToken);

        var content =
            await connection.QuerySingleOrDefaultAsync<string>(
                command);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new KeyNotFoundException(
                $"QC form {qcFormId} was not found.");
        }

        return content;
    }


    private sealed class QCFormRow
    {
        public long QCFormID { get; init; }
        public long ProjectID { get; init; }
        public long ChecklistTemplateID { get; init; }
        public long ChecklistTemplateVersionID { get; init; }

        public string FormNumber { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;

        public Guid? StartedByUserID { get; init; }
        public Guid? AssignedToUserID { get; init; }

        public DateTime? StartedAt { get; init; }
        public DateTime? SubmittedAt { get; init; }

        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }

    private sealed class QCFormProjectFieldRow
    {
        public long QCFormProjectFieldID { get; init; }

        public long? ChecklistTemplateProjectFieldID { get; init; }

        public string FieldKey { get; init; } = string.Empty;
        public string FieldLabel { get; init; } = string.Empty;
        public string FieldType { get; init; } = string.Empty;

        public string? Placeholder { get; init; }
        public string? HelpText { get; init; }

        public bool IsRequired { get; init; }

        public string? OptionsJson { get; init; }
        public string? ValidationRulesJson { get; init; }

        public int DisplayOrder { get; init; }

        public string? ValueJson { get; init; }
    }

    private sealed class QCFormSectionRow
    {
        public long QCFormSectionID { get; init; }

        public long? ChecklistSectionID { get; init; }

        public string SectionName { get; init; } = string.Empty;

        public string? Description { get; init; }

        public int DisplayOrder { get; init; }
    }

    private sealed class QCFormFieldRow
    {
        public long QCFormFieldID { get; init; }

        public long QCFormSectionID { get; init; }

        public long? ChecklistFieldID { get; init; }

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

        public long QCResponseID { get; init; }

        public string? ValueJson { get; init; }

        public bool IsAnswered { get; init; }

        public Guid? AnsweredByUserID { get; init; }

        public DateTime? AnsweredAt { get; init; }

        public DateTime UpdatedAt { get; init; }
    }
    public async Task<QCResponseValidationRow?> GetResponseValidationAsync(
    long qcFormId,
    long qcResponseId,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            r."QCFormID",
            r."QCResponseID",
            r."QCFormFieldID",
            f."FieldType"::text AS "FieldType",
            f."IsRequired",
            f."RequiresPhoto",
            f."RequiresSignature",
            f."Options"::text AS "OptionsJson",
            f."ValidationRules"::text AS "ValidationRulesJson"
        FROM public."QCResponse" r
        INNER JOIN public."QCFormField" f
            ON f."QCFormFieldID" = r."QCFormFieldID"
        INNER JOIN public."QCFormSection" s
            ON s."QCFormSectionID" = f."QCFormSectionID"
        WHERE r."QCResponseID" = @QCResponseID
          AND r."QCFormID" = @QCFormID
          AND s."QCFormID" = @QCFormID;
        """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                QCFormID = qcFormId,
                QCResponseID = qcResponseId
            },
            cancellationToken: cancellationToken);

        return await connection
            .QuerySingleOrDefaultAsync<QCResponseValidationRow>(
                command);
    }
    public async Task<QCProjectFieldValidationRow?> GetProjectFieldValidationAsync(
    long qcFormId,
    long qcFormProjectFieldId,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            "QCFormID",
            "QCFormProjectFieldID",
            "FieldType"::text AS "FieldType",
            "IsRequired",
            "Options"::text AS "OptionsJson",
            "ValidationRules"::text AS "ValidationRulesJson"
        FROM public."QCFormProjectField"
        WHERE "QCFormID" = @QCFormID
          AND "QCFormProjectFieldID" =
              @QCFormProjectFieldID;
        """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                QCFormID = qcFormId,
                QCFormProjectFieldID =
                    qcFormProjectFieldId
            },
            cancellationToken: cancellationToken);

        return await connection
            .QuerySingleOrDefaultAsync<QCProjectFieldValidationRow>(
                command);
    }
}
