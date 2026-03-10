using System.Net;
using System.Text.Json;
using CodeReviewIA.Configuration;
using CodeReviewIA.Models;

namespace CodeReviewIA.Services;

public sealed class AzureDevOpsService : IAzureDevOpsService
{
    private readonly HttpClient _httpClient;
    private readonly AzureDevOpsSettings _settings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AzureDevOpsService(HttpClient httpClient, AzureDevOpsSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public async Task<PullRequestDetails> GetPullRequestDetailsAsync(int prId, CancellationToken ct = default)
    {
        var url = BuildRepoUrl($"pullrequests/{prId}", "api-version=7.1");
        var response = await _httpClient.GetAsync(url, ct);
        await EnsureSuccessAsync(response, $"PR #{prId}");
        return await DeserializeAsync<PullRequestDetails>(response, ct);
    }

    public async Task<List<PullRequestIteration>> GetPullRequestIterationsAsync(int prId, CancellationToken ct = default)
    {
        var url = BuildRepoUrl($"pullrequests/{prId}/iterations", "api-version=7.1");
        var response = await _httpClient.GetAsync(url, ct);
        await EnsureSuccessAsync(response, $"iteracoes da PR #{prId}");
        var envelope = await DeserializeAsync<AzureDevOpsListResponse<PullRequestIteration>>(response, ct);
        return envelope.Value;
    }

    public async Task<List<ChangeEntry>> GetPullRequestChangedFilesAsync(int prId, int iterationId, CancellationToken ct = default)
    {
        var allChanges = new List<ChangeEntry>();
        int skip = 0;
        const int pageSize = 100;

        while (true)
        {
            var url = BuildRepoUrl(
                $"pullrequests/{prId}/iterations/{iterationId}/changes",
                $"$top={pageSize}&$skip={skip}&api-version=7.1");

            var response = await _httpClient.GetAsync(url, ct);
            await EnsureSuccessAsync(response, $"alteracoes da iteracao {iterationId}");

            var result = await DeserializeAsync<IterationChangesResponse>(response, ct);

            var fileChanges = result.ChangeEntries
                .Where(c => c.Item != null && !c.Item.IsFolder)
                .ToList();

            allChanges.AddRange(fileChanges);

            if (result.ChangeEntries.Count < pageSize)
                break;

            skip += pageSize;
        }

        return allChanges;
    }

    public async Task<FileDiffResponse> GetFileDiffAsync(string baseCommit, string targetCommit, string filePath, CancellationToken ct = default)
    {
        var encodedPath = Uri.EscapeDataString(filePath);
        var url = $"{_settings.OrganizationUrl}/{Uri.EscapeDataString(_settings.Project)}/_apis/git/repositories/" +
                  $"{Uri.EscapeDataString(_settings.RepositoryId)}/diffs/commits?" +
                  $"baseVersion={baseCommit}&targetVersion={targetCommit}" +
                  $"&path={encodedPath}&diffCommonCommit=true&api-version=7.1";

        var response = await _httpClient.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new FileDiffResponse();

        await EnsureSuccessAsync(response, $"diff de '{filePath}'");
        return await DeserializeAsync<FileDiffResponse>(response, ct);
    }

    public async Task<string> GetFileContentAsync(string filePath, string commitId, CancellationToken ct = default)
    {
        var encodedPath = Uri.EscapeDataString(filePath);
        var url = BuildRepoUrl(
            "items",
            $"path={encodedPath}&versionDescriptor.version={commitId}&versionDescriptor.versionType=commit&api-version=7.1");

        var response = await _httpClient.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return string.Empty;

        await EnsureSuccessAsync(response, $"conteudo de '{filePath}'");
        return await response.Content.ReadAsStringAsync(ct);
    }

    private string BuildRepoUrl(string resource, string queryString)
    {
        return $"{_settings.OrganizationUrl}/{Uri.EscapeDataString(_settings.Project)}/_apis/git/repositories/" +
               $"{Uri.EscapeDataString(_settings.RepositoryId)}/{resource}?{queryString}";
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string context)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync();
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Autenticacao falhou. Verifique o PAT configurado.",
            HttpStatusCode.Forbidden    => $"Acesso negado ao recurso: {context}.",
            HttpStatusCode.NotFound     => $"Recurso nao encontrado: {context}. Verifique org/project/repo/PR ID.",
            _                           => $"Erro HTTP {(int)response.StatusCode} ao acessar {context}: {body}"
        };

        throw new HttpRequestException(message, null, response.StatusCode);
    }

    private static async Task<T> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct)
               ?? throw new InvalidOperationException("Resposta JSON vazia ou invalida.");
    }
}
