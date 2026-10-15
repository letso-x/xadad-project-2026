using MzukuluQMS.Api.Data.Repositories;
using MzukuluQMS.Api.DTOs;
using MzukuluQMS.Api.Services.Users;
using MzukuluQMS.Api.Services.Workflow;
using MzukuluQMS.Api.Services.SignOffs;

namespace MzukuluQMS.Api.Services;

public sealed class QCFormService : IQCFormService
{
    private readonly IQCFormRepository _repository;
    private readonly IQCWorkflow _workflow;
    private readonly NcrService _ncr;
    private readonly IQCFormContentHashService _hash;
    private readonly IFieldValueValidator _fieldValueValidator;

    private readonly ICurrentUserService _currentUserService;

    public QCFormService(
    IQCFormRepository repository,
    IFieldValueValidator fieldValueValidator,
    ICurrentUserService currentUserService, IQCWorkflow workflow, IQCFormContentHashService hash, NcrService ncr)
    {
        _repository = repository;
        _workflow = workflow;
        _hash = hash;
        _ncr = ncr;
        _fieldValueValidator = fieldValueValidator;
        _currentUserService = currentUserService;
    }

    public async Task<QCFormDto> CreateAsync(
        CreateQCFormRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var operation = await _workflow.BeginAsync(null, request.ProjectID, "PerformInspection", cancellationToken);

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

        if (request.ChecklistTemplateVersionID.HasValue && request.ChecklistTemplateVersionID.Value != activeVersionId.Value)
            throw new MzukuluQMS.Api.Exceptions.ConflictException("The cached template version is no longer active. Preserve local work and reconcile the snapshot.");

        var currentUser =
    await _currentUserService.GetCurrentUserAsync(
        cancellationToken);

        var form = await _repository.CreateAsync(
            request.ProjectID,
            request.ChecklistTemplateID,
            activeVersionId.Value,
            currentUser.UserID,
            request.AssignedToUserID,
            cancellationToken);

        await operation.CompleteAsync("QCFormCreated", form.QCFormID, null, cancellationToken, form);
        return form;
}
    public async Task<QCFormDetailsDto?> GetByIdAsync(
    long qcFormId,
    CancellationToken cancellationToken = default)
    {
        if (qcFormId <= 0)
        {
            throw new ArgumentException(
                "QCFormID must be greater than zero.",
                nameof(qcFormId));
        }

        await using var access = await _workflow.BeginAsync(qcFormId, null, "ReadInspection", cancellationToken);
        return await _repository.GetByIdAsync(
            qcFormId,
            cancellationToken);
    }

    public async Task UpdateResponseAsync(
    long qcFormId,
    long qcResponseId,
    UpdateQCResponseRequest request,
    CancellationToken cancellationToken = default)
    {
        await using var operation = await _workflow.BeginAsync(qcFormId, null, "PerformInspection", cancellationToken);

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

        if (InspectionOutcome.IsFailure(validation.FieldType,validation.OptionsJson,request.Value))
            await _ncr.RecordFailureAsync(qcFormId,qcResponseId,cancellationToken);
        await operation.CompleteAsync("QCResponseUpdated", qcFormId, new { qcResponseId }, cancellationToken);
}

    public async Task UpdateProjectFieldAsync(
    long qcFormId,
    long qcFormProjectFieldId,
    UpdateQCFormProjectFieldRequest request,
    CancellationToken cancellationToken = default)
    {
        await using var operation = await _workflow.BeginAsync(qcFormId, null, "PerformInspection", cancellationToken);

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

        await operation.CompleteAsync("QCFormProjectFieldUpdated", qcFormId, new { qcFormProjectFieldId }, cancellationToken);
}

    public async Task SubmitAsync(
    long qcFormId,
    CancellationToken cancellationToken = default)
    {
        await using var operation = await _workflow.BeginAsync(qcFormId, null, "SubmitInspection", cancellationToken);

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

        var canSubmit =
    string.Equals(
        status,
        "Draft",
        StringComparison.OrdinalIgnoreCase)
    ||
    string.Equals(
        status,
        "InProgress",
        StringComparison.OrdinalIgnoreCase)
    ||
    string.Equals(
        status,
        "ReturnedForCorrection",
        StringComparison.OrdinalIgnoreCase);

        if (!canSubmit)
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

        await operation.CompleteAsync("QCFormSubmitted", qcFormId, null, cancellationToken);
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
            string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase)
    || string.Equals(status, "InProgress", StringComparison.OrdinalIgnoreCase)
    || string.Equals(
        status,
        "ReturnedForCorrection",
        StringComparison.OrdinalIgnoreCase);

        if (!editable)
        {
            throw new InvalidOperationException(
                $"QC form cannot be edited while its status is '{status}'.");
        }
    }

    public async Task ApproveAsync(
    long qcFormId,
    CancellationToken cancellationToken = default)
    {
        await using var operation = await _workflow.BeginAsync(qcFormId, null, "ApproveInspection", cancellationToken);

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
                "Submitted",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"QC form cannot be approved from status '{status}'.");
        }

        await _workflow.EnsureCurrentSignaturesAsync(qcFormId, await _hash.GenerateHashAsync(qcFormId, cancellationToken), cancellationToken);

        await _repository.ApproveAsync(
            qcFormId,
            cancellationToken);

        await operation.CompleteAsync("QCFormApproved", qcFormId, null, cancellationToken);
}

    public async Task ReturnForCorrectionAsync(
    long qcFormId,
    string reason,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000) throw new ArgumentException("Correction reason is required (maximum 2000 characters).");
        reason = reason.Trim();

        await using var operation = await _workflow.BeginAsync(qcFormId, null, "ReviewInspection", cancellationToken);

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
                "Submitted",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"QC form cannot be returned for correction from status '{status}'.");
        }

        await _repository.ReturnForCorrectionAsync(
            qcFormId,
            cancellationToken);

        await operation.CompleteAsync("QCFormReturnedForCorrection", qcFormId, new { reason }, cancellationToken);
}
}