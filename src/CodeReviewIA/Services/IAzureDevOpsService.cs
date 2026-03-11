using CodeReviewIA.Models;

namespace CodeReviewIA.Services;

public interface IAzureDevOpsService
{
    Task<PullRequestDetails> GetPullRequestDetailsAsync(int prId, CancellationToken ct = default);

    Task<List<PullRequestIteration>> GetPullRequestIterationsAsync(int prId, CancellationToken ct = default);

    Task<List<ChangeEntry>> GetPullRequestChangedFilesAsync(int prId, int iterationId, CancellationToken ct = default);

    /// <summary>
    /// Obtém o diff de um arquivo via endpoint HierarchyQuery.
    /// Os blocos retornados já contêm oLines (antes) e mLines (depois).
    /// </summary>
    Task<FileDiffResponse> GetFileDiffFromHierarchyAsync(
        string originalCommit,
        string modifiedCommit,
        string filePath,
        int prId,
        string repositoryName,
        CancellationToken ct = default);

    /// <summary>Retorna o conteudo bruto de um arquivo em um commit especifico.</summary>
    Task<byte[]> GetFileContentAsync(string filePath, string commitId, CancellationToken ct = default);

    /// <summary>Retorna os work items vinculados a uma PR com id, titulo e descricao.</summary>
    Task<List<WorkItemOutput>> GetPullRequestWorkItemsAsync(int prId, CancellationToken ct = default);
}
