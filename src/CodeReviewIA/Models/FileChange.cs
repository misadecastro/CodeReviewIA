using System.Text.Json.Serialization;

namespace CodeReviewIA.Models;

public sealed class IterationChangesResponse
{
    [JsonPropertyName("changeEntries")]
    public List<ChangeEntry> ChangeEntries { get; set; } = [];
}

public sealed class ChangeEntry
{
    [JsonPropertyName("changeId")]
    public int ChangeId { get; set; }

    [JsonPropertyName("changeType")]
    public string ChangeType { get; set; } = string.Empty;

    [JsonPropertyName("item")]
    public ChangeItem? Item { get; set; }
}

public sealed class ChangeItem
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("isFolder")]
    public bool IsFolder { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
