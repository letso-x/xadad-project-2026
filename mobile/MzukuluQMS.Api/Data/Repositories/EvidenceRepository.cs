using Dapper;
using MzukuluQMS.Api.DTOs.Evidence;

namespace MzukuluQMS.Api.Data.Repositories;

public sealed class EvidenceRepository : IEvidenceRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public EvidenceRepository(
        IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> QCFormExistsAsync(
        long qcFormId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM public."QCForm"
                WHERE "QCFormID" = @QCFormID
            );
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

        return await connection.ExecuteScalarAsync<bool>(
            command);
    }

    public async Task<bool> QCFormFieldBelongsToFormAsync(
        long qcFormId,
        long qcFormFieldId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM public."QCFormField" f
                INNER JOIN public."QCFormSection" s
                    ON s."QCFormSectionID" = f."QCFormSectionID"
                WHERE f."QCFormFieldID" = @QCFormFieldID
                  AND s."QCFormID" = @QCFormID
            );
            """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                QCFormID = qcFormId,
                QCFormFieldID = qcFormFieldId
            },
            cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<bool>(
            command);
    }

    public async Task<EvidenceDto> CreateAsync(
        long qcFormId,
        long? qcFormFieldId,
        string evidenceType,
        string storageBucket,
        string storagePath,
        string fileName,
        string? contentType,
        long? fileSizeBytes,
        decimal? latitude,
        decimal? longitude,
        DateTimeOffset? capturedAt,
        Guid uploadedByUserId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO public."Evidence"
            (
                "QCFormID",
                "QCFormFieldID",
                "EvidenceType",
                "StorageBucket",
                "StoragePath",
                "FileName",
                "ContentType",
                "FileSizeBytes",
                "Latitude",
                "Longitude",
                "CapturedAt",
                "UploadedByUserID"
            )
            VALUES
            (
                @QCFormID,
                @QCFormFieldID,
                CAST(@EvidenceType AS public.evidence_type),
                @StorageBucket,
                @StoragePath,
                @FileName,
                @ContentType,
                @FileSizeBytes,
                @Latitude,
                @Longitude,
                @CapturedAt,
                @UploadedByUserID
            )
            RETURNING
                "EvidenceID",
                "QCFormID",
                "QCFormFieldID",
                "EvidenceType"::text AS "EvidenceType",
                "StorageBucket",
                "StoragePath",
                "FileName",
                "ContentType",
                "FileSizeBytes",
                "Latitude",
                "Longitude",
                "CapturedAt",
                "UploadedByUserID",
                "CreatedAt";
            """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                QCFormID = qcFormId,
                QCFormFieldID = qcFormFieldId,
                EvidenceType = evidenceType,
                StorageBucket = storageBucket,
                StoragePath = storagePath,
                FileName = fileName,
                ContentType = contentType,
                FileSizeBytes = fileSizeBytes,
                Latitude = latitude,
                Longitude = longitude,
                CapturedAt = capturedAt,
                UploadedByUserID = uploadedByUserId
            },
            cancellationToken: cancellationToken);

        return await connection
            .QuerySingleAsync<EvidenceDto>(command);
    }

    public async Task<QCFormFieldEvidenceRules?> GetFieldEvidenceRulesAsync(
    long qcFormId,
    long qcFormFieldId,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            f."QCFormFieldID",
            f."FieldType"::text AS "FieldType",
            f."RequiresPhoto",
            f."RequiresSignature"
        FROM public."QCFormField" f
        INNER JOIN public."QCFormSection" s
            ON s."QCFormSectionID" = f."QCFormSectionID"
        WHERE f."QCFormFieldID" = @QCFormFieldID
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
                QCFormFieldID = qcFormFieldId
            },
            cancellationToken: cancellationToken);

        return await connection
            .QuerySingleOrDefaultAsync<QCFormFieldEvidenceRules>(
                command);
    }
}