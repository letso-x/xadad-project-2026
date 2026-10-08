using MzukuluQMS.Api.Data.Repositories;
using MzukuluQMS.Api.DTOs.Evidence;
using MzukuluQMS.Api.Services.Users;

namespace MzukuluQMS.Api.Services.Evidence;

public sealed class EvidenceService : IEvidenceService
{
    private readonly IEvidenceRepository _repository;
    private readonly IEvidenceStorage _storage;
    private readonly ICurrentUserService _currentUserService;

    public EvidenceService(
        IEvidenceRepository repository,
        IEvidenceStorage storage,
        ICurrentUserService currentUserService)
    {
        _repository = repository;
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

            var fieldBelongsToForm =
                await _repository.QCFormFieldBelongsToFormAsync(
                    qcFormId,
                    request.QCFormFieldID.Value,
                    cancellationToken);

            if (!fieldBelongsToForm)
            {
                throw new KeyNotFoundException(
                    "The QC form field was not found for this form.");
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
}