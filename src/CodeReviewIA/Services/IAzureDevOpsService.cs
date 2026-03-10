using CodeReviewIA.Models;

namespace CodeReviewIA.Services;

public interface IAzureDevOpsService
{
    Task<PullRequestDetails> GetPullRequestDetailsAsync(int prId, CancellationToken ct = default);

    Task<List<PullRequestIteration>> GetPullRequestIterationsAsync(int prId, CancellationToken ct = default);

    Task<List<ChangeEntry>> GetPullRequestChangedFilesAsync(int prId, int iterationId, CancellationToken ct = default);

    Task<FileDiffResponse> GetFileDiffAsync(string baseCommit, string targetCommit, string filePath, CancellationToken ct = default);

    Task<string> GetFileContentAsync(string filePath, string commitId, CancellationToken ct = default);
}
