using System.Text.Json.Serialization;

namespace CodeReviewIA.Models;

public sealed class PrOutput
{
    [JsonPropertyName("pr")]
    public string Pr { get; set; } = string.Empty;

    [JsonPropertyName("titulo")]
    public string Titulo { get; set; } = string.Empty;

    [JsonPropertyName("descricao")]
    public string Descricao { get; set; } = string.Empty;

    [JsonPropertyName("autor")]
    public string Autor { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("branch")]
    public string Branch { get; set; } = string.Empty;

    [JsonPropertyName("workitems")]
    public List<WorkItemOutput> WorkItems { get; set; } = [];

    [JsonPropertyName("items")]
    public List<PrItemOutput> Items { get; set; } = [];
}

public sealed class WorkItemOutput
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("titulo")]
    public string Titulo { get; set; } = string.Empty;

    [JsonPropertyName("descricao")]
    public string Descricao { get; set; } = string.Empty;
}

public sealed class PrItemOutput
{
    [JsonPropertyName("arquivo")]
    public string Arquivo { get; set; } = string.Empty;

    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("alteracoes")]
    public List<PrAlteracaoOutput> Alteracoes { get; set; } = [];
}

public sealed class PrAlteracaoOutput
{
    [JsonPropertyName("original")]
    public List<string> Original { get; set; } = [];

    [JsonPropertyName("alteracao")]
    public List<string> Alteracao { get; set; } = [];
}
