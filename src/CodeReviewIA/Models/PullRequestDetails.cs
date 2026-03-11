using System.Text.Json.Serialization;

namespace CodeReviewIA.Models;

public sealed class PullRequestDetails
{
    [JsonPropertyName("pullRequestId")]
    public int PullRequestId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("createdBy")]
    public IdentityRef? CreatedBy { get; set; }

    [JsonPropertyName("sourceRefName")]
    public string SourceRefName { get; set; } = string.Empty;

    [JsonPropertyName("targetRefName")]
    public string TargetRefName { get; set; } = string.Empty;

    [JsonPropertyName("repository")]
    public GitRepository? Repository { get; set; }

    public string SourceBranch => SourceRefName.Replace("refs/heads/", string.Empty);
    public string TargetBranch => TargetRefName.Replace("refs/heads/", string.Empty);
}

public sealed class GitRepository
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class IdentityRef
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class AzureDevOpsListResponse<T>
{
    [JsonPropertyName("value")]
    public List<T> Value { get; set; } = [];

    [JsonPropertyName("count")]
    public int Count { get; set; }
}
