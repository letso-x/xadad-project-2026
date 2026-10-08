namespace MzukuluQMS.Api.Services.Evidence;

public interface IEvidenceStorage
{
    Task<EvidenceStorageResult> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        long qcFormId,
        long? qcFormFieldId,
        CancellationToken cancellationToken = default);
}