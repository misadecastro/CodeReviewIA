using System.CommandLine;
using CodeReviewIA.Commands;
using CodeReviewIA.Configuration;
using CodeReviewIA.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// ============================================================
// 1. Configuracao
// ============================================================
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables(prefix: "AZUREDEVOPS__")
    .Build();

var settings = new AzureDevOpsSettings();
configuration.GetSection(AzureDevOpsSettings.SectionName).Bind(settings);

// ============================================================
// 2. Injecao de dependencias
// ============================================================
var services = new ServiceCollection();

services.AddSingleton(settings);
services.AddTransient<AuthenticationHandler>();

// O AuthenticationHandler le o PAT do singleton AzureDevOpsSettings a cada requisicao,
// garantindo que sobrescrita via --pat funcione mesmo apos o build do container.
services.AddHttpClient<IAzureDevOpsService, AzureDevOpsService>(client =>
{
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(60);
})
.AddHttpMessageHandler<AuthenticationHandler>();

var serviceProvider = services.BuildServiceProvider();

// ============================================================
// 3. CLI — System.CommandLine
// ============================================================
var rootCommand = new RootCommand(
    "CodeReviewIA - Consulta e exibe detalhes de Pull Requests do Azure DevOps.");

var azureDevOpsService = serviceProvider.GetRequiredService<IAzureDevOpsService>();
rootCommand.AddCommand(GetPullRequestCommand.Build(azureDevOpsService, settings));

// ============================================================
// 4. Execucao
// ============================================================
return await rootCommand.InvokeAsync(args);
