namespace MzukuluQMS.Api.Services.Workflow;
public interface IQCWorkflow
{
    Task<IQCOperation> BeginAsync(long? formId, long? projectId, string permission, CancellationToken ct);
    Task EnsureSigningAuthorityAsync(long formId, string signOffType, CancellationToken ct);
    Task EnsureCurrentSignaturesAsync(long formId, string currentHash, CancellationToken ct);
}
public interface IQCOperation : IAsyncDisposable
{
    Task CompleteAsync(string action, long formId, object? details, CancellationToken ct, object? result = null);
}
