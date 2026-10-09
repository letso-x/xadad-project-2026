using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using MzukuluQMS.Api.Configuration;

namespace MzukuluQMS.Api.Services.Evidence;

public sealed class SupabaseEvidenceStorage : IEvidenceStorage
{
    private readonly HttpClient _httpClient;
    private readonly SupabaseStorageOptions _options;

    public SupabaseEvidenceStorage(
        HttpClient httpClient,
        IOptions<SupabaseStorageOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<EvidenceStorageResult> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        long qcFormId,
        long? qcFormFieldId,
        CancellationToken cancellationToken = default)
    {
        var extension =
            Path.GetExtension(fileName);

        var storedFileName =
            $"{Guid.NewGuid():N}{extension}";

        var fieldFolder =
            qcFormFieldId.HasValue
                ? $"field-{qcFormFieldId.Value}"
                : "form";

        var storagePath =
            $"qc-forms/{qcFormId}/{fieldFolder}/{storedFileName}";

        var encodedPath = string.Join(
            "/",
            storagePath
                .Split('/')
                .Select(Uri.EscapeDataString));

        var url =
            $"{_options.BaseUrl.TrimEnd('/')}" +
            $"/storage/v1/object/{Uri.EscapeDataString(_options.Bucket)}/{encodedPath}";

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                _options.SecretKey);

        request.Headers.Add(
            "apikey",
            _options.SecretKey);

        using var content =
            new StreamContent(stream);

        content.Headers.ContentType =
            MediaTypeHeaderValue.Parse(
                string.IsNullOrWhiteSpace(contentType)
                    ? "application/octet-stream"
                    : contentType);

        request.Content = content;

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            throw new InvalidOperationException(
                $"Supabase Storage upload failed. " +
                $"Status: {(int)response.StatusCode}. " +
                $"Response: {error}");
        }

        return new EvidenceStorageResult
        {
            Bucket = _options.Bucket,
            Path = storagePath
        };
    }

    public async Task DeleteAsync(
    string bucket,
    string path,
    CancellationToken cancellationToken = default)
    {
        var encodedPath = string.Join(
            "/",
            path
                .Split('/')
                .Select(Uri.EscapeDataString));

        var url =
            $"{_options.BaseUrl.TrimEnd('/')}" +
            $"/storage/v1/object/{Uri.EscapeDataString(bucket)}/{encodedPath}";

        using var request =
            new HttpRequestMessage(
                HttpMethod.Delete,
                url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                _options.SecretKey);

        request.Headers.Add(
            "apikey",
            _options.SecretKey);

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            throw new InvalidOperationException(
                $"Supabase Storage delete failed. " +
                $"Status: {(int)response.StatusCode}. " +
                $"Response: {error}");
        }
    }
}