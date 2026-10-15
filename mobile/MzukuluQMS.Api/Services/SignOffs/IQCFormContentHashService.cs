namespace MzukuluQMS.Api.Services.SignOffs;

public interface IQCFormContentHashService
{
    Task<string> GenerateHashAsync(
        long qcFormId,
        CancellationToken cancellationToken = default);
}