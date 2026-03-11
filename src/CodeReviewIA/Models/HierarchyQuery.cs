using System.Text.Json.Serialization;

namespace CodeReviewIA.Models;

// ─── Request ────────────────────────────────────────────────────────────────

public sealed class HierarchyQueryRequest
{
    [JsonPropertyName("contributionIds")]
    public List<string> ContributionIds { get; set; } =
        ["ms.vss-code-web.file-diff-data-provider"];

    [JsonPropertyName("dataProviderContext")]
    public DataProviderContext DataProviderContext { get; set; } = new();
}

public sealed class DataProviderContext
{
    [JsonPropertyName("properties")]
    public DataProviderProperties Properties { get; set; } = new();
}

public sealed class DataProviderProperties
{
    [JsonPropertyName("repositoryId")]
    public string RepositoryId { get; set; } = string.Empty;

    [JsonPropertyName("diffParameters")]
    public HierarchyDiffParameters DiffParameters { get; set; } = new();

    [JsonPropertyName("sourcePage")]
    public SourcePage SourcePage { get; set; } = new();
}

public sealed class HierarchyDiffParameters
{
    [JsonPropertyName("includeCharDiffs")]
    public bool IncludeCharDiffs { get; set; } = true;

    [JsonPropertyName("modifiedPath")]
    public string ModifiedPath { get; set; } = string.Empty;

    /// <summary>Commit do estado modificado (novo). Formato: GC{commitId}</summary>
    [JsonPropertyName("modifiedVersion")]
    public string ModifiedVersion { get; set; } = string.Empty;

    [JsonPropertyName("originalPath")]
    public string OriginalPath { get; set; } = string.Empty;

    /// <summary>Commit do estado original (base). Formato: GC{commitId}</summary>
    [JsonPropertyName("originalVersion")]
    public string OriginalVersion { get; set; } = string.Empty;

    [JsonPropertyName("partialDiff")]
    public bool PartialDiff { get; set; } = true;

    [JsonPropertyName("forceLoad")]
    public bool ForceLoad { get; set; } = false;
}

public sealed class SourcePage
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("routeId")]
    public string RouteId { get; set; } = "ms.vss-code-web.pull-request-details-route";

    [JsonPropertyName("routeValues")]
    public RouteValues RouteValues { get; set; } = new();
}

public sealed class RouteValues
{
    [JsonPropertyName("project")]
    public string Project { get; set; } = string.Empty;

    [JsonPropertyName("GitRepositoryName")]
    public string GitRepositoryName { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public string Parameters { get; set; } = string.Empty;

    [JsonPropertyName("vctype")]
    public string VcType { get; set; } = "git";

    [JsonPropertyName("controller")]
    public string Controller { get; set; } = "ContributedPage";

    [JsonPropertyName("action")]
    public string Action { get; set; } = "Execute";

    /// <summary>GUID da organizacao/host do Azure DevOps</summary>
    [JsonPropertyName("serviceHost")]
    public string ServiceHost { get; set; } = string.Empty;
}

// ─── Response ───────────────────────────────────────────────────────────────

public sealed class HierarchyQueryResponse
{
    [JsonPropertyName("dataProviders")]
    public HierarchyDataProviders? DataProviders { get; set; }
}

public sealed class HierarchyDataProviders
{
    [JsonPropertyName("ms.vss-code-web.file-diff-data-provider")]
    public FileDiffResponse? FileDiff { get; set; }
}
