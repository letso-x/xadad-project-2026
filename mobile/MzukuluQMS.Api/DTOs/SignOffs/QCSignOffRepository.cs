using Dapper;
using MzukuluQMS.Api.DTOs.SignOffs;

namespace MzukuluQMS.Api.Data.Repositories;

public sealed class QCSignOffRepository : IQCSignOffRepository
{
    private readonly QmsDbSession _session;

    public QCSignOffRepository(
        QmsDbSession session)
    {
        _session = session;
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

        await using var lease = await _session.OpenAsync(cancellationToken);
        var connection = lease.Connection;

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
                FROM public."Evidence" e
                JOIN public."QCForm" q ON q."QCFormID"=e."QCFormID"
                WHERE e."EvidenceID" = @EvidenceID
                  AND e."QCFormID" = @QCFormID
                  AND "UploadedByUserID" = @UserID
                  AND "EvidenceType" = 'Signature'::public.evidence_type
                  AND e."CreatedAt" >= q."SubmittedAt"
                  AND NOT EXISTS (SELECT 1 FROM public."QCSignOff" old WHERE old."SignatureEvidenceID"=e."EvidenceID")
            );
            """;

        await using var lease = await _session.OpenAsync(cancellationToken);
        var connection = lease.Connection;

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
                "ContentHash",
                "HashVersion"
            )
            VALUES
            (
                @QCFormID,
                @UserID,
                CAST(@SignOffType AS public.signoff_type),
                @SignatureEvidenceID,
                clock_timestamp(),
                @Comments,
                @ContentHash,
                2
            )
            RETURNING
                "QCSignOffID",
                "QCFormID",
                "UserID",
                "SignOffType"::text AS "SignOffType",
                "SignatureEvidenceID",
                "SignedAt",
                "Comments",
                "ContentHash",
                "HashVersion";
            """;

        await using var lease = await _session.OpenAsync(cancellationToken);
        var connection = lease.Connection;

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