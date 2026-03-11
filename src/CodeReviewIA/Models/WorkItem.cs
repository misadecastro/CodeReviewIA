using System.Text.Json.Serialization;

namespace CodeReviewIA.Models;

/// <summary>Referencia de work item retornada pelo endpoint pullrequests/{id}/workitems</summary>
public sealed class WorkItemReference
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

/// <summary>Work item com campos detalhados retornado pelo endpoint wit/workitems</summary>
public sealed class WorkItemDetail
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("fields")]
    public WorkItemFields? Fields { get; set; }
}

public sealed class WorkItemFields
{
    [JsonPropertyName("System.Title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("System.Description")]
    public string? Description { get; set; }
}
