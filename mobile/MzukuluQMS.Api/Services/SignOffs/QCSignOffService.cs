using MzukuluQMS.Api.Data.Repositories;
using MzukuluQMS.Api.DTOs.SignOffs;
using MzukuluQMS.Api.Services.Users;
using MzukuluQMS.Api.Services.Workflow;

namespace MzukuluQMS.Api.Services.SignOffs;

public sealed class QCSignOffService : IQCSignOffService


{
    private static readonly HashSet<string> AllowedSignOffTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Inspector",
            "Technician",
            "Supervisor",
            "Client"
        };
    private readonly IQCWorkflow _workflow;
    private readonly IQCFormRepository _qcFormRepository;
    private readonly IQCFormContentHashService _contentHashService;
    private readonly IQCSignOffRepository _repository;
    private readonly ICurrentUserService _currentUserService;

    public QCSignOffService(
    IQCSignOffRepository repository,
    IQCFormRepository qcFormRepository,
    ICurrentUserService currentUserService,
    IQCFormContentHashService contentHashService, IQCWorkflow workflow)
    {
        _repository = repository;
        _workflow = workflow;
        _qcFormRepository = qcFormRepository;
        _currentUserService = currentUserService;
        _contentHashService = contentHashService;
    }

    public async Task<QCSignOffDto> CreateAsync(
        long qcFormId,
        CreateQCSignOffRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var operation = await _workflow.BeginAsync(qcFormId, null, "SignInspection", cancellationToken);

        if (qcFormId <= 0)
        {
            throw new ArgumentException(
                "QCFormID must be greater than zero.",
                nameof(qcFormId));
        }

        if (string.IsNullOrWhiteSpace(request.SignOffType))
        {
            throw new ArgumentException(
                "SignOffType is required.",
                nameof(request.SignOffType));
        }

        if (!AllowedSignOffTypes.Contains(
                request.SignOffType))
        {
            throw new ArgumentException(
                $"Unsupported SignOffType '{request.SignOffType}'.",
                nameof(request.SignOffType));
        }

        if (request.SignatureEvidenceID <= 0)
        {
            throw new ArgumentException(
                "SignatureEvidenceID must be greater than zero.",
                nameof(request.SignatureEvidenceID));
        }
        var signOffType = AllowedSignOffTypes.Single(t => t.Equals(request.SignOffType, StringComparison.OrdinalIgnoreCase));
        await _workflow.EnsureSigningAuthorityAsync(qcFormId, signOffType, cancellationToken);
        var contentHash =
    await _contentHashService.GenerateHashAsync(
        qcFormId,
        cancellationToken);

        var formExists =
            await _repository.QCFormExistsAsync(
                qcFormId,
                cancellationToken);

        var status =
    await _qcFormRepository.GetStatusAsync(
        qcFormId,
        cancellationToken);

        if (!string.Equals(
                status,
                "Submitted",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"QC form cannot be signed while its status is '{status}'.");
        }

        if (!formExists)
        {
            throw new KeyNotFoundException(
                $"QC form {qcFormId} was not found.");
        }

        var currentUser =
            await _currentUserService.GetCurrentUserAsync(
                cancellationToken);

        var validSignatureEvidence =
            await _repository.SignatureEvidenceIsValidAsync(
                qcFormId,
                request.SignatureEvidenceID,
                currentUser.UserID,
                cancellationToken);

        if (!validSignatureEvidence)
        {
            throw new ArgumentException(
                "The signature evidence is invalid for this form and user.",
                nameof(request.SignatureEvidenceID));
        }

        var signedAt =
            DateTimeOffset.UtcNow;

        var comments =
            string.IsNullOrWhiteSpace(request.Comments)
                ? null
                : request.Comments.Trim();

        var signoff = await _repository.CreateAsync(
    qcFormId,
    currentUser.UserID,
    signOffType,
    request.SignatureEvidenceID,
    comments,
    signedAt,
    contentHash,
    cancellationToken);

        await operation.CompleteAsync("QCFormSigned", qcFormId, new { signoff.QCSignOffID, HashVersion = 2 }, cancellationToken, signoff);
        return signoff;
}
}