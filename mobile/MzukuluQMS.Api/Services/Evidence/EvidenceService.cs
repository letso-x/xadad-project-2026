using MzukuluQMS.Api.Data.Repositories;
using MzukuluQMS.Api.DTOs.Evidence;
using MzukuluQMS.Api.Services.Users;
using MzukuluQMS.Api.Services.Workflow;

namespace MzukuluQMS.Api.Services.Evidence;

public sealed class EvidenceService : IEvidenceService
{
    private readonly IEvidenceRepository _repository;
    private readonly IQCWorkflow _workflow;
    private readonly ILogger<EvidenceService> _logger;
    private readonly IEvidenceStorage _storage;
    private readonly ICurrentUserService _currentUserService;
    private readonly IQCFormRepository _qcFormRepository;

    public EvidenceService(
    IEvidenceRepository repository,
    IQCFormRepository qcFormRepository,
    IEvidenceStorage storage,
    ICurrentUserService currentUserService, IQCWorkflow workflow, ILogger<EvidenceService> logger)
    {
        _repository = repository;
        _workflow = workflow;
        _logger = logger;
        _qcFormRepository = qcFormRepository;
        _storage = storage;
        _currentUserService = currentUserService;
    }

    public async Task<EvidenceDto> UploadAsync(
        long qcFormId,
        UploadEvidenceRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var operation = await _workflow.BeginAsync(qcFormId, null, string.Equals(request.EvidenceType,"Signature",StringComparison.OrdinalIgnoreCase) ? "SignInspection" : "PerformInspection", cancellationToken);

        if (qcFormId <= 0)
        {
            throw new ArgumentException(
                "QCFormID must be greater than zero.",
                nameof(qcFormId));
        }

        var formExists =
            await _repository.QCFormExistsAsync(
                qcFormId,
                cancellationToken);

        var status =
    await _qcFormRepository.GetStatusAsync(
        qcFormId,
        cancellationToken);

        var isDraftOrInProgress =
            string.Equals(
                status,
                "Draft",
                StringComparison.OrdinalIgnoreCase)
            ||
            string.Equals(
                status,
                "InProgress",
                StringComparison.OrdinalIgnoreCase);

        var isSubmitted =
            string.Equals(
                status,
                "Submitted",
                StringComparison.OrdinalIgnoreCase);

        var isSignatureEvidence =
            string.Equals(
                request.EvidenceType,
                "Signature",
                StringComparison.OrdinalIgnoreCase);

        var evidenceAllowed =
            isDraftOrInProgress
            || string.Equals(status, "ReturnedForCorrection", StringComparison.OrdinalIgnoreCase)
            ||
            (isSubmitted && isSignatureEvidence);

        if (!evidenceAllowed)
        {
            throw new InvalidOperationException(
                $"Evidence type '{request.EvidenceType}' cannot be added while QC form status is '{status}'.");
        }

        if (!formExists)
        {
            throw new KeyNotFoundException(
                $"QC form {qcFormId} was not found.");
        }

        if (request.QCFormFieldID.HasValue)
        {
            if (request.QCFormFieldID.Value <= 0)
            {
                throw new ArgumentException(
                    "QCFormFieldID must be greater than zero.",
                    nameof(request.QCFormFieldID));
            }

            var fieldRules =
                await _repository.GetFieldEvidenceRulesAsync(
                    qcFormId,
                    request.QCFormFieldID.Value,
                    cancellationToken);

            if (fieldRules is null)
            {
                throw new KeyNotFoundException(
                    "The QC form field was not found for this form.");
            }

            if (isSignatureEvidence && !string.Equals(fieldRules.FieldType,"Signature",StringComparison.OrdinalIgnoreCase) && !fieldRules.RequiresSignature)
                throw new ArgumentException("Signature evidence cannot be attached to this QC form field.");

            if (string.Equals(
                    request.EvidenceType,
                    "Photo",
                    StringComparison.OrdinalIgnoreCase))
            {
                var acceptsPhoto =
                    string.Equals(
                        fieldRules.FieldType,
                        "Photo",
                        StringComparison.OrdinalIgnoreCase)
                    || fieldRules.RequiresPhoto;

                if (!acceptsPhoto)
                {
                    throw new ArgumentException(
                        "Photo evidence cannot be attached to this QC form field.");
                }
            }
        }

        if (request.File is null ||
            request.File.Length <= 0)
        {
            throw new ArgumentException(
                "An evidence file is required.",
                nameof(request.File));
        }

        if (string.IsNullOrWhiteSpace(request.EvidenceType))
        {
            throw new ArgumentException(
                "EvidenceType is required.",
                nameof(request.EvidenceType));
        }

        var allowedEvidenceTypes = new[]
{
    "Photo",
    "Signature"
};

        if (!allowedEvidenceTypes.Contains(
                request.EvidenceType,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Unsupported EvidenceType '{request.EvidenceType}'.",
                nameof(request.EvidenceType));
        }

        var evidenceType = allowedEvidenceTypes.Single(t => t.Equals(request.EvidenceType, StringComparison.OrdinalIgnoreCase));
        if (request.File.Length > 20 * 1024 * 1024) throw new ArgumentException("Evidence file exceeds the 20 MiB limit.");
        if (request.File.ContentType is not ("image/png" or "image/jpeg")) throw new ArgumentException("Photo/signature evidence must be PNG or JPEG.");
        await using (var headerStream = request.File.OpenReadStream())
        {
            var header = new byte[8];
            var count = await headerStream.ReadAtLeastAsync(header,8,false,cancellationToken);
            var png = count==8 && header.SequenceEqual(new byte[] {137,80,78,71,13,10,26,10});
            var jpeg = count>=3 && header[0]==255 && header[1]==216 && header[2]==255;
            if (!(request.File.ContentType=="image/png" ? png : jpeg)) throw new ArgumentException("Evidence bytes do not match the declared image type.");
        }
        var currentUser =
            await _currentUserService.GetCurrentUserAsync(
                cancellationToken);

        await using var stream =
            request.File.OpenReadStream();

        var storageResult =
            await _storage.UploadAsync(
                stream,
                Path.GetFileName(request.File.FileName),
                request.File.ContentType,
                qcFormId,
                request.QCFormFieldID,
                cancellationToken);

        DateTimeOffset? capturedAtUtc =
     request.CapturedAt?.ToUniversalTime();

        try
        {
            var evidence = await _repository.CreateAsync(
                qcFormId,
                request.QCFormFieldID,
                evidenceType,
                storageResult.Bucket,
                storageResult.Path,
                request.File.FileName,
                request.File.ContentType,
                request.File.Length,
                request.Latitude,
                request.Longitude,
                capturedAtUtc,
                currentUser.UserID,
                cancellationToken);
            await operation.CompleteAsync("EvidenceUploaded", qcFormId, new { evidence.EvidenceID, request.QCFormFieldID }, cancellationToken, evidence);
            return evidence;
        }
        catch
        {
            try
            {
                await _storage.DeleteAsync(storageResult.Bucket, storageResult.Path, CancellationToken.None);
            }
            catch
            {
                _logger.LogError("Evidence cleanup failed for form {FormID}; storage reconciliation required.", qcFormId);
            }

            throw;
        }
    }
}