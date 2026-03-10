using System.Text.Json.Serialization;

namespace CodeReviewIA.Models;

public sealed class PullRequestIteration
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("sourceRefCommit")]
    public GitCommitRef? SourceRefCommit { get; set; }

    [JsonPropertyName("targetRefCommit")]
    public GitCommitRef? TargetRefCommit { get; set; }

    [JsonPropertyName("commonRefCommit")]
    public GitCommitRef? CommonRefCommit { get; set; }
}

public sealed class GitCommitRef
{
    [JsonPropertyName("commitId")]
    public string CommitId { get; set; } = string.Empty;
}
