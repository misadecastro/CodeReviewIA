using System.Text;

namespace CodeReviewIA.Configuration;

public sealed class AzureDevOpsSettings
{
    public const string SectionName = "AzureDevOps";

    public string OrganizationUrl { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string RepositoryId { get; set; } = string.Empty;
    public string PersonalAccessToken { get; set; } = string.Empty;

    public string GetAuthorizationHeader()
    {
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{PersonalAccessToken}"));
        return $"Basic {credentials}";
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationUrl))
            throw new InvalidOperationException(
                "OrganizationUrl nao configurada. Use appsettings.json, variavel de ambiente AZUREDEVOPS__ORGANIZATIONURL ou --org.");

        if (string.IsNullOrWhiteSpace(Project))
            throw new InvalidOperationException(
                "Project nao configurado. Use appsettings.json, variavel de ambiente AZUREDEVOPS__PROJECT ou --project.");

        if (string.IsNullOrWhiteSpace(RepositoryId))
            throw new InvalidOperationException(
                "RepositoryId nao configurado. Use appsettings.json, variavel de ambiente AZUREDEVOPS__REPOSITORYID ou --repo.");

        if (string.IsNullOrWhiteSpace(PersonalAccessToken))
            throw new InvalidOperationException(
                "PersonalAccessToken nao configurado. Use appsettings.json, variavel de ambiente AZUREDEVOPS__PERSONALACCESSTOKEN ou --pat.");
    }
}
