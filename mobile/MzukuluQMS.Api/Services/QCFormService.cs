using MzukuluQMS.Api.Data.Repositories;
using MzukuluQMS.Api.DTOs;

namespace MzukuluQMS.Api.Services;

public sealed class QCFormService : IQCFormService
{
    private readonly IQCFormRepository _repository;
    private readonly IFieldValueValidator _fieldValueValidator;

    public QCFormService(
        IQCFormRepository repository,
        IFieldValueValidator fieldValueValidator)
    {
        _repository = repository;
        _fieldValueValidator = fieldValueValidator;
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

        return await _repository.CreateAsync(
            request.ProjectID,
            request.ChecklistTemplateID,
            activeVersionId.Value,
            startedByUserId: null,
            assignedToUserId: request.AssignedToUserID,
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

        await _repository.UpdateResponseAsync(
            qcFormId,
            qcResponseId,
            valueJson,
            answeredByUserId: null,
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
}