using Dapper;
using MzukuluQMS.Api.DTOs.SignOffs;

namespace MzukuluQMS.Api.Data.Repositories;

public sealed class QCSignOffRepository : IQCSignOffRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public QCSignOffRepository(
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

    public async Task<bool> SignatureEvidenceIsValidAsync(
        long qcFormId,
        long signatureEvidenceId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM public."Evidence"
                WHERE "EvidenceID" = @EvidenceID
                  AND "QCFormID" = @QCFormID
                  AND "UploadedByUserID" = @UserID
                  AND "EvidenceType" = 'Signature'::public.evidence_type
            );
            """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                EvidenceID = signatureEvidenceId,
                QCFormID = qcFormId,
                UserID = userId
            },
            cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<bool>(
            command);
    }

    public async Task<QCSignOffDto> CreateAsync(
    long qcFormId,
    Guid userId,
    string signOffType,
    long signatureEvidenceId,
    string? comments,
    DateTimeOffset signedAt,
    string contentHash,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO public."QCSignOff"
            (
                "QCFormID",
                "UserID",
                "SignOffType",
                "SignatureEvidenceID",
                "SignedAt",
                "Comments",
                "ContentHash"
            )
            VALUES
            (
                @QCFormID,
                @UserID,
                CAST(@SignOffType AS public.signoff_type),
                @SignatureEvidenceID,
                @SignedAt,
                @Comments,
                @ContentHash
            )
            RETURNING
                "QCSignOffID",
                "QCFormID",
                "UserID",
                "SignOffType"::text AS "SignOffType",
                "SignatureEvidenceID",
                "SignedAt",
                "Comments",
                "ContentHash";
            """;

        await using var connection =
            await _connectionFactory.CreateOpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                QCFormID = qcFormId,
                UserID = userId,
                SignOffType = signOffType,
                SignatureEvidenceID = signatureEvidenceId,
                SignedAt = signedAt,
                Comments = comments,
                ContentHash = contentHash
            },
            cancellationToken: cancellationToken);

        return await connection
            .QuerySingleAsync<QCSignOffDto>(
                command);
    }
}