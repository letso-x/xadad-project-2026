using MzukuluQMS.Api.Data.Repositories;
using MzukuluQMS.Api.DTOs;
using MzukuluQMS.Api.Services.Users;

namespace MzukuluQMS.Api.Services;

public sealed class QCFormService : IQCFormService
{
    private readonly IQCFormRepository _repository;
    private readonly IFieldValueValidator _fieldValueValidator;

    private readonly ICurrentUserService _currentUserService;

    public QCFormService(
    IQCFormRepository repository,
    IFieldValueValidator fieldValueValidator,
    ICurrentUserService currentUserService)
    {
        _repository = repository;
        _fieldValueValidator = fieldValueValidator;
        _currentUserService = currentUserService;
    }

    public async Task<QCFormDto> CreateAsync(
        CreateQCFormRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ProjectID <= 0)
        {
            throw new ArgumentException(
                "ProjectID must be greater than zero.",
                nameof(request.ProjectID));
        }

        if (request.ChecklistTemplateID <= 0)
        {
            throw new ArgumentException(
                "ChecklistTemplateID must be greater than zero.",
                nameof(request.ChecklistTemplateID));
        }

        var projectExists =
            await _repository.ProjectExistsAsync(
                request.ProjectID,
                cancellationToken);

        if (!projectExists)
        {
            throw new KeyNotFoundException(
                $"Project {request.ProjectID} was not found.");
        }

        var activeVersionId =
            await _repository.GetActiveTemplateVersionIdAsync(
                request.ChecklistTemplateID,
                cancellationToken);

        if (!activeVersionId.HasValue)
        {
            throw new InvalidOperationException(
                "The checklist template does not have an active version available for use.");
        }

        var currentUser =
    await _currentUserService.GetCurrentUserAsync(
        cancellationToken);

        return await _repository.CreateAsync(
            request.ProjectID,
            request.ChecklistTemplateID,
            activeVersionId.Value,
            currentUser.UserID,
            request.AssignedToUserID,
            cancellationToken);
    }
    public Task<QCFormDetailsDto?> GetByIdAsync(
    long qcFormId,
    CancellationToken cancellationToken = default)
    {
        if (qcFormId <= 0)
        {
            throw new ArgumentException(
                "QCFormID must be greater than zero.",
                nameof(qcFormId));
        }

        return _repository.GetByIdAsync(
            qcFormId,
            cancellationToken);
    }

    public async Task UpdateResponseAsync(
    long qcFormId,
    long qcResponseId,
    UpdateQCResponseRequest request,
    CancellationToken cancellationToken = default)
    {
        if (qcFormId <= 0)
        {
            throw new ArgumentException(
                "QCFormID must be greater than zero.",
                nameof(qcFormId));
        }

        if (qcResponseId <= 0)
        {
            throw new ArgumentException(
                "QCResponseID must be greater than zero.",
                nameof(qcResponseId));
        }

        var validation =
            await _repository.GetResponseValidationAsync(
                qcFormId,
                qcResponseId,
                cancellationToken);

        await EnsureFormIsEditableAsync(
    qcFormId,
    cancellationToken);

        if (validation is null)
        {
            throw new KeyNotFoundException(
                "The QC response was not found for this form.");
        }

        _fieldValueValidator.Validate(
            request.Value,
            validation.FieldType,
            validation.IsRequired,
            validation.OptionsJson,
            validation.ValidationRulesJson);

        var valueJson =
            request.Value.GetRawText();

        var currentUser =
    await _currentUserService.GetCurrentUserAsync(
        cancellationToken);

        await _repository.UpdateResponseAsync(
            qcFormId,
            qcResponseId,
            valueJson,
            answeredByUserId: currentUser.UserID,
            cancellationToken);
    }

    public async Task UpdateProjectFieldAsync(
    long qcFormId,
    long qcFormProjectFieldId,
    UpdateQCFormProjectFieldRequest request,
    CancellationToken cancellationToken = default)
    {
        if (qcFormId <= 0)
        {
            throw new ArgumentException(
                "QCFormID must be greater than zero.",
                nameof(qcFormId));
        }

        if (qcFormProjectFieldId <= 0)
        {
            throw new ArgumentException(
                "QCFormProjectFieldID must be greater than zero.",
                nameof(qcFormProjectFieldId));
        }

        var validation =
            await _repository.GetProjectFieldValidationAsync(
                qcFormId,
                qcFormProjectFieldId,
                cancellationToken);

        await EnsureFormIsEditableAsync(
        qcFormId,
        cancellationToken);

        if (validation is null)
        {
            throw new KeyNotFoundException(
                "The QC form project field was not found for this form.");
        }

        _fieldValueValidator.Validate(
            request.Value,
            validation.FieldType,
            validation.IsRequired,
            validation.OptionsJson,
            validation.ValidationRulesJson);

        var valueJson =
            request.Value.GetRawText();

        await _repository.UpdateProjectFieldAsync(
            qcFormId,
            qcFormProjectFieldId,
            valueJson,
            cancellationToken);
    }

    public async Task SubmitAsync(
    long qcFormId,
    CancellationToken cancellationToken = default)
    {
        if (qcFormId <= 0)
        {
            throw new ArgumentException(
                "QCFormID must be greater than zero.",
                nameof(qcFormId));
        }

        var status =
            await _repository.GetStatusAsync(
                qcFormId,
                cancellationToken);

        if (status is null)
        {
            throw new KeyNotFoundException(
                $"QC form {qcFormId} was not found.");
        }

        if (!string.Equals(
                status,
                "Draft",
                StringComparison.OrdinalIgnoreCase)
            &&
            !string.Equals(
                status,
                "InProgress",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"QC form cannot be submitted from status '{status}'.");
        }

        var missingProjectFields =
            await _repository.CountMissingRequiredProjectFieldsAsync(
                qcFormId,
                cancellationToken);

        if (missingProjectFields > 0)
        {
            throw new InvalidOperationException(
                $"QC form cannot be submitted because {missingProjectFields} required project field(s) are incomplete.");
        }

        var missingResponses =
            await _repository.CountMissingRequiredResponsesAsync(
                qcFormId,
                cancellationToken);

        if (missingResponses > 0)
        {
            throw new InvalidOperationException(
                $"QC form cannot be submitted because {missingResponses} required response(s) are incomplete.");
        }

        var missingPhotoEvidence =
            await _repository.CountMissingRequiredPhotoEvidenceAsync(
                qcFormId,
                cancellationToken);

        if (missingPhotoEvidence > 0)
        {
            throw new InvalidOperationException(
                $"QC form cannot be submitted because {missingPhotoEvidence} required photo evidence item(s) are missing.");
        }

        await _repository.SubmitAsync(
            qcFormId,
            cancellationToken);
    }

    private async Task EnsureFormIsEditableAsync(
    long qcFormId,
    CancellationToken cancellationToken)
    {
        var status =
            await _repository.GetStatusAsync(
                qcFormId,
                cancellationToken);

        if (status is null)
        {
            throw new KeyNotFoundException(
                $"QC form {qcFormId} was not found.");
        }

        var editable =
            string.Equals(
                status,
                "Draft",
                StringComparison.OrdinalIgnoreCase)
            ||
            string.Equals(
                status,
                "InProgress",
                StringComparison.OrdinalIgnoreCase);

        if (!editable)
        {
            throw new InvalidOperationException(
                $"QC form cannot be edited while its status is '{status}'.");
        }
    }
}