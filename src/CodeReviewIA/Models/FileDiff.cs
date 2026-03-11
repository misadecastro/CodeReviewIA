using System.Text.Json.Serialization;

namespace CodeReviewIA.Models;

public sealed class FileDiffResponse
{
    [JsonPropertyName("blocks")]
    public List<DiffBlock> Blocks { get; set; } = [];

    [JsonPropertyName("modifiedFile")]
    public DiffFileInfo? ModifiedFile { get; set; }

    [JsonPropertyName("originalFile")]
    public DiffFileInfo? OriginalFile { get; set; }
}

public sealed class DiffBlock
{
    /// <summary>changeType: 0=None (contexto), 1=Add, 2=Delete, 3=Edit</summary>
    [JsonPropertyName("changeType")]
    public int ChangeType { get; set; }

    [JsonPropertyName("mLine")]
    public int ModifiedLineStart { get; set; }

    [JsonPropertyName("mLinesCount")]
    public int ModifiedLinesCount { get; set; }

    [JsonPropertyName("oLine")]
    public int OriginalLineStart { get; set; }

    [JsonPropertyName("oLinesCount")]
    public int OriginalLinesCount { get; set; }

    /// <summary>Linhas da versao original (antes da alteracao)</summary>
    [JsonPropertyName("oLines")]
    public List<string> OLines { get; set; } = [];

    /// <summary>Linhas da versao modificada (apos a alteracao)</summary>
    [JsonPropertyName("mLines")]
    public List<string> MLines { get; set; } = [];

    [JsonPropertyName("truncatedBefore")]
    public bool TruncatedBefore { get; set; }

    [JsonPropertyName("truncatedAfter")]
    public bool TruncatedAfter { get; set; }
}

public sealed class DiffFileInfo
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
