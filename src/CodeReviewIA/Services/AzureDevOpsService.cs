using System.Net;
using System.Text;
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

    public async Task<FileDiffResponse> GetFileDiffFromHierarchyAsync(
        string originalCommit,
        string modifiedCommit,
        string filePath,
        int prId,
        string repositoryName,
        CancellationToken ct = default)
    {
        var url = $"{_settings.OrganizationUrl}/_apis/Contribution/HierarchyQuery/project/" +
                  $"{_settings.RepositoryName}?api-version=5.0-preview.1";

        var requestBody = new HierarchyQueryRequest
        {
            DataProviderContext = new DataProviderContext
            {
                Properties = new DataProviderProperties
                {
                    RepositoryId = _settings.RepositoryId,
                    DiffParameters = new HierarchyDiffParameters
                    {
                        ModifiedPath  = filePath,
                        ModifiedVersion = $"GC{modifiedCommit}",
                        OriginalPath  = filePath,
                        OriginalVersion = $"GC{originalCommit}"
                    },
                    SourcePage = new SourcePage
                    {
                        Url = $"{_settings.OrganizationUrl}/{_settings.Project}/_git/{repositoryName}/pullrequest/{prId}?_a=files",
                        RouteValues = new RouteValues
                        {
                            Project           = _settings.Project,
                            GitRepositoryName = repositoryName,
                            Parameters        = prId.ToString(),
                            ServiceHost       = _settings.ServiceHostId
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new FileDiffResponse();

        await EnsureSuccessAsync(response, $"diff de '{filePath}'");

        var result = await DeserializeAsync<HierarchyQueryResponse>(response, ct);
        return result.DataProviders?.FileDiff ?? new FileDiffResponse();
    }

    public async Task<byte[]> GetFileContentAsync(string filePath, string commitId, CancellationToken ct = default)
    {
        var url = BuildRepoUrl(
            "items",
            $"path={Uri.EscapeDataString(filePath)}&versionDescriptor.version={commitId}" +
            $"&versionDescriptor.versionType=commit&api-version=7.1");

        // Envia a requisicao com Accept: application/octet-stream para receber conteudo bruto
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Accept", "application/octet-stream");

        var response = await _httpClient.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        await EnsureSuccessAsync(response, $"conteudo de '{filePath}'");
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<List<WorkItemOutput>> GetPullRequestWorkItemsAsync(int prId, CancellationToken ct = default)
    {
        // 1. Busca as referencias de work items vinculados a PR (retorna apenas id e url)
        var refsUrl = BuildRepoUrl($"pullrequests/{prId}/workitems", "api-version=7.1");
        var refsResponse = await _httpClient.GetAsync(refsUrl, ct);

        if (refsResponse.StatusCode == HttpStatusCode.NotFound)
            return [];

        await EnsureSuccessAsync(refsResponse, $"work items da PR #{prId}");
        var refs = await DeserializeAsync<AzureDevOpsListResponse<WorkItemReference>>(refsResponse, ct);

        if (refs.Value.Count == 0)
            return [];

        // 2. Busca os detalhes (titulo e descricao) em lote pelo endpoint wit/workitems
        var ids = string.Join(",", refs.Value.Select(r => r.Id));
        var detailsUrl = $"{_settings.OrganizationUrl}/{Uri.EscapeDataString(_settings.Project)}/_apis/wit/workitems" +
                         $"?ids={ids}&fields=System.Title,System.Description&api-version=7.1";

        var detailsResponse = await _httpClient.GetAsync(detailsUrl, ct);
        await EnsureSuccessAsync(detailsResponse, $"detalhes dos work items da PR #{prId}");

        var details = await DeserializeAsync<AzureDevOpsListResponse<WorkItemDetail>>(detailsResponse, ct);

        return details.Value.Select(wi => new WorkItemOutput
        {
            Id        = wi.Id.ToString(),
            Titulo    = wi.Fields?.Title ?? string.Empty,
            Descricao = wi.Fields?.Description ?? string.Empty
        }).ToList();
    }

    private string BuildRepoUrl(string resource, string queryString)
    {
        return $"{_settings.OrganizationUrl}/{Uri.EscapeDataString(_settings.Project)}/_apis/git/repositories/" +
               $"{Uri.EscapeDataString(_settings.RepositoryName)}/{resource}?{queryString}";
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
