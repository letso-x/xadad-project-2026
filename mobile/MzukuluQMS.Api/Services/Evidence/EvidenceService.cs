using MzukuluQMS.Api.Data.Repositories;
using MzukuluQMS.Api.DTOs.Evidence;
using MzukuluQMS.Api.Services.Users;

namespace MzukuluQMS.Api.Services.Evidence;

public sealed class EvidenceService : IEvidenceService
{
    private readonly IEvidenceRepository _repository;
    private readonly IEvidenceStorage _storage;
    private readonly ICurrentUserService _currentUserService;
    private readonly IQCFormRepository _qcFormRepository;

    public EvidenceService(
    IEvidenceRepository repository,
    IQCFormRepository qcFormRepository,
    IEvidenceStorage storage,
    ICurrentUserService currentUserService)
    {
        _repository = repository;
        _qcFormRepository = qcFormRepository;
        _storage = storage;
        _currentUserService = currentUserService;
    }

    public async Task<EvidenceDto> UploadAsync(
        long qcFormId,
        UploadEvidenceRequest request,
        CancellationToken cancellationToken = default)
    {
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
    "Signature",
    "Document"
};

        if (!allowedEvidenceTypes.Contains(
                request.EvidenceType,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Unsupported EvidenceType '{request.EvidenceType}'.",
                nameof(request.EvidenceType));
        }

        var currentUser =
            await _currentUserService.GetCurrentUserAsync(
                cancellationToken);

        await using var stream =
            request.File.OpenReadStream();

        var storageResult =
            await _storage.UploadAsync(
                stream,
                request.File.FileName,
                request.File.ContentType,
                qcFormId,
                request.QCFormFieldID,
                cancellationToken);

        DateTimeOffset? capturedAtUtc =
     request.CapturedAt?.ToUniversalTime();

        try
        {
            return await _repository.CreateAsync(
                qcFormId,
                request.QCFormFieldID,
                request.EvidenceType,
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
        }
        catch
        {
            await _storage.DeleteAsync(
                storageResult.Bucket,
                storageResult.Path,
                cancellationToken);

            throw;
        }
    }
}