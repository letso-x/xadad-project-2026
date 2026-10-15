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
            contentType == "image/png" ? ".png" : ".jpg";

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

        // Modern publishable/secret keys are API keys, not bearer JWTs.
        if (!_options.SecretKey.StartsWith("sb_", StringComparison.Ordinal))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);

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


            throw new InvalidOperationException(
                $"Supabase Storage upload failed. " +
                $"Status: {(int)response.StatusCode}.");
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

        // Modern publishable/secret keys are API keys, not bearer JWTs.
        if (!_options.SecretKey.StartsWith("sb_", StringComparison.Ordinal))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);

        request.Headers.Add(
            "apikey",
            _options.SecretKey);

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {


            throw new InvalidOperationException(
                $"Supabase Storage delete failed. " +
                $"Status: {(int)response.StatusCode}.");
        }
    }
    public async Task<byte[]> DownloadAsync(string bucket,string path,CancellationToken cancellationToken=default)
    {
        const int maximum=20*1024*1024;
        var encoded=string.Join("/",path.Split('/').Select(Uri.EscapeDataString));
        using var request=new HttpRequestMessage(HttpMethod.Get,$"{_options.BaseUrl.TrimEnd('/')}/storage/v1/object/authenticated/{Uri.EscapeDataString(bucket)}/{encoded}");
        request.Headers.Add("apikey",_options.SecretKey);
        if(!_options.SecretKey.StartsWith("sb_",StringComparison.Ordinal))request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",_options.SecretKey);
        using var response=await _httpClient.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellationToken);
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException($"Private evidence download failed. Status: {(int)response.StatusCode}.");
        if(response.Content.Headers.ContentLength>maximum)throw new InvalidOperationException("Evidence exceeds the supported download limit.");
        await using var stream=await response.Content.ReadAsStreamAsync(cancellationToken);using var output=new MemoryStream();
        var buffer=new byte[81920];int count;while((count=await stream.ReadAsync(buffer,cancellationToken))>0){if(output.Length+count>maximum)throw new InvalidOperationException("Evidence exceeds the supported download limit.");await output.WriteAsync(buffer.AsMemory(0,count),cancellationToken);}
        return output.ToArray();
    }

}