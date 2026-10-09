using System.Security.Cryptography;
using System.Text;
using MzukuluQMS.Api.Data.Repositories;

namespace MzukuluQMS.Api.Services.SignOffs;

public sealed class QCFormContentHashService : IQCFormContentHashService
{
    private readonly IQCFormRepository _qcFormRepository;

    public QCFormContentHashService(
        IQCFormRepository qcFormRepository)
    {
        _qcFormRepository = qcFormRepository;
    }

    public async Task<string> GenerateHashAsync(
        long qcFormId,
        CancellationToken cancellationToken = default)
    {
        var content =
            await _qcFormRepository.GetSignableContentAsync(
                qcFormId,
                cancellationToken);

        var bytes =
            Encoding.UTF8.GetBytes(content);

        var hash =
            SHA256.HashData(bytes);

        return Convert.ToHexString(hash)
            .ToLowerInvariant();
    }
}