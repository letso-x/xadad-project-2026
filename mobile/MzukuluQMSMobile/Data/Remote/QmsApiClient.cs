using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MzukuluQMSMobile.Models;

namespace MzukuluQMSMobile.Data.Remote;

public sealed class QmsApiClient : IQmsApiClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public QmsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<Project>> GetProjectsAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            "api/projects",
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var projects = await response.Content.ReadFromJsonAsync<
            IReadOnlyList<Project>>(
            JsonOptions,
            cancellationToken);

        return projects ?? [];
    }

    public async Task<Project?> GetProjectAsync(
        long projectId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"api/projects/{projectId}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<Project>(
            JsonOptions,
            cancellationToken);
    }

    public async Task<Project> CreateProjectAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/projects",
            request,
            JsonOptions,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var project = await response.Content.ReadFromJsonAsync<Project>(
            JsonOptions,
            cancellationToken);

        return project
            ?? throw new InvalidOperationException(
                "The API returned an empty project response.");
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var problemDetails =
            await response.Content.ReadFromJsonAsync<ApiProblemDetails>(
                JsonOptions,
                cancellationToken);

        if (problemDetails is not null)
        {
            throw new QmsApiException(
                response.StatusCode,
                problemDetails.Title,
                problemDetails.Detail);
        }

        throw new QmsApiException(
            response.StatusCode,
            "API request failed.",
            $"The API returned HTTP {(int)response.StatusCode}.");
    }
}