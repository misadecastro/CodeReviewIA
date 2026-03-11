using System.CommandLine;
using System.Text.Json;
using CodeReviewIA.Configuration;
using CodeReviewIA.Models;
using CodeReviewIA.Services;
using Spectre.Console;

namespace CodeReviewIA.Commands;

public static class GetPullRequestCommand
{
    private const int MaxDiffFiles = 20;

    private static readonly JsonSerializerOptions JsonOutputOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static Command Build(IAzureDevOpsService service, AzureDevOpsSettings settings)
    {
        var prIdOption = new Option<int>(
            aliases: ["--pr-id", "-p"],
            description: "ID da Pull Request a ser consultada.")
        { IsRequired = true };

        var orgOption = new Option<string?>(
            aliases: ["--org"],
            description: "URL da organizacao Azure DevOps (ex: https://dev.azure.com/MinhaOrg).");

        var projectOption = new Option<string?>(
            aliases: ["--project"],
            description: "Nome do projeto Azure DevOps.");

        var repoOption = new Option<string?>(
            aliases: ["--repo"],
            description: "Nome do repositorio.");

        var patOption = new Option<string?>(
            aliases: ["--pat"],
            description: "Personal Access Token do Azure DevOps.");

        var serviceHostOption = new Option<string?>(
            aliases: ["--service-host"],
            description: "GUID da organizacao/host do Azure DevOps (serviceHost).");

        var repoIdOption = new Option<string?>(
            aliases: ["--repo-id"],
            description: "GUID do repositorio (usado no body do HierarchyQuery).");

        var outputOption = new Option<string?>(
            aliases: ["--output"],
            description: "Diretorio de saida para o arquivo JSON. Sobrescreve OutputDirectory do appsettings.");

        var command = new Command("get-pr", "Exibe detalhes de uma Pull Request do Azure DevOps.")
        {
            prIdOption,
            orgOption,
            projectOption,
            repoOption,
            patOption,
            serviceHostOption,
            repoIdOption,
            outputOption
        };

        command.SetHandler(async (prId, org, project, repo, pat, serviceHost, repoId, output) =>
        {
            if (!string.IsNullOrWhiteSpace(org))          settings.OrganizationUrl     = org;
            if (!string.IsNullOrWhiteSpace(project))      settings.Project             = project;
            if (!string.IsNullOrWhiteSpace(repo))         settings.RepositoryName      = repo;
            if (!string.IsNullOrWhiteSpace(pat))          settings.PersonalAccessToken = pat;
            if (!string.IsNullOrWhiteSpace(serviceHost))  settings.ServiceHostId       = serviceHost;
            if (!string.IsNullOrWhiteSpace(repoId))       settings.RepositoryId        = repoId;
            if (!string.IsNullOrWhiteSpace(output))       settings.OutputDirectory     = output;

            try
            {
                settings.Validate();
                await ExecuteAsync(service, settings, prId);
            }
            catch (InvalidOperationException ex)
            {
                AnsiConsole.MarkupLine($"[red bold]Erro de configuracao:[/] {Markup.Escape(ex.Message)}");
                Environment.Exit(1);
            }
            catch (HttpRequestException ex)
            {
                AnsiConsole.MarkupLine($"[red bold]Erro de API:[/] {Markup.Escape(ex.Message)}");
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red bold]Erro inesperado:[/] {Markup.Escape(ex.Message)}");
                Environment.Exit(1);
            }
        },
        prIdOption, orgOption, projectOption, repoOption, patOption, serviceHostOption, repoIdOption, outputOption);

        return command;
    }

    private static async Task ExecuteAsync(IAzureDevOpsService service, AzureDevOpsSettings settings, int prId)
    {
        // 1. Detalhes da PR
        PullRequestDetails pr = null!;
        await AnsiConsole.Status().StartAsync("Buscando detalhes da PR...", async _ =>
        {
            pr = await service.GetPullRequestDetailsAsync(prId);
        });

        RenderPrPanel(pr);

        // 2. Iteracoes — usa a ultima para obter os commits de base e source
        List<PullRequestIteration> iterations = null!;
        await AnsiConsole.Status().StartAsync("Buscando iteracoes...", async _ =>
        {
            iterations = await service.GetPullRequestIterationsAsync(prId);
        });

        if (iterations.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Nenhuma iteracao encontrada para esta PR.[/]");
            return;
        }

        var lastIteration = iterations[^1];
        var sourceCommit = lastIteration.SourceRefCommit?.CommitId ?? string.Empty;
        var commonCommit = lastIteration.CommonRefCommit?.CommitId ?? string.Empty;
        var targetCommit = lastIteration.TargetRefCommit?.CommitId ?? string.Empty;

        // originalVersion = merge base (commonRef); fallback para targetRef
        var baseCommit = string.IsNullOrWhiteSpace(commonCommit) ? targetCommit : commonCommit;

        // 3. Arquivos alterados (paginado, apenas blobs)
        List<ChangeEntry> changes = null!;
        await AnsiConsole.Status().StartAsync($"Buscando arquivos alterados (iteracao {lastIteration.Id})...", async _ =>
        {
            changes = await service.GetPullRequestChangedFilesAsync(prId, lastIteration.Id);
        });

        RenderChangesTable(changes);

        // 3b. Work items vinculados
        List<WorkItemOutput> workItems = [];
        await AnsiConsole.Status().StartAsync("Buscando work items vinculados...", async _ =>
        {
            workItems = await service.GetPullRequestWorkItemsAsync(prId);
        });

        if (workItems.Count > 0)
            RenderWorkItemsTable(workItems);

        // 4. Diffs via HierarchyQuery
        var repositoryName = pr.Repository?.Name ?? settings.RepositoryName;

        var filesToDiff = changes
            .Where(c => c.Item != null && !c.Item.IsFolder)
            .Take(MaxDiffFiles)
            .ToList();

        if (changes.Count > MaxDiffFiles)
            AnsiConsole.MarkupLine($"[grey]Exibindo diffs dos primeiros {MaxDiffFiles} de {changes.Count} arquivo(s).[/]\n");

        // Coleta os diffs para exibicao e para o JSON de saida
        var diffResults = new List<(ChangeEntry Change, FileDiffResponse Diff)>();

        foreach (var change in filesToDiff)
        {
            var filePath = change.Item!.Path;
            FileDiffResponse diff = new();

            await AnsiConsole.Status().StartAsync($"Carregando diff: {filePath}...", async _ =>
            {
                diff = await service.GetFileDiffFromHierarchyAsync(
                    baseCommit, sourceCommit, filePath, prId, repositoryName);
            });

            RenderFileDiff(filePath, diff);
            diffResults.Add((change, diff));
        }

        // 5. Salva JSON e baixa arquivos (se OutputDirectory configurado)
        if (!string.IsNullOrWhiteSpace(settings.OutputDirectory))
            await SaveOutputAsync(service, settings.OutputDirectory, pr, workItems, changes, diffResults, sourceCommit, baseCommit);
    }

    private static async Task SaveOutputAsync(
        IAzureDevOpsService service,
        string outputDirectory,
        PullRequestDetails pr,
        List<WorkItemOutput> workItems,
        List<ChangeEntry> changes,
        List<(ChangeEntry Change, FileDiffResponse Diff)> diffResults,
        string sourceCommit,
        string baseCommit)
    {
        var prFolder = Path.Combine(outputDirectory, $"pr-{pr.PullRequestId}");
        Directory.CreateDirectory(prFolder);

        // 5a. Salva JSON com dados da PR
        var diffMap = diffResults.ToDictionary(d => d.Change.Item!.Path, d => d.Diff);

        var output = new PrOutput
        {
            Pr        = pr.PullRequestId.ToString(),
            Titulo    = pr.Title,
            Descricao = pr.Description ?? string.Empty,
            Autor     = pr.CreatedBy?.DisplayName ?? string.Empty,
            Status    = pr.Status,
            Branch    = $"{pr.SourceBranch} -> {pr.TargetBranch}",
            WorkItems = workItems,
            Items     = changes
                .Where(c => c.Item != null && !c.Item.IsFolder)
                .Select(c =>
                {
                    diffMap.TryGetValue(c.Item!.Path, out var diff);
                    return new PrItemOutput
                    {
                        Arquivo    = c.Item.Path,
                        Tipo       = c.ChangeType,
                        Url        = c.Item.Url,
                        Alteracoes = (diff?.Blocks ?? [])
                            .Where(b => b.ChangeType != 0)
                            .Select(b => new PrAlteracaoOutput
                            {
                                Original  = b.OLines,
                                Alteracao = b.MLines
                            })
                            .ToList()
                    };
                })
                .ToList()
        };

        var jsonPath = Path.Combine(prFolder, $"pr-{pr.PullRequestId}.json");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(output, JsonOutputOptions));
        AnsiConsole.MarkupLine($"\n[green]JSON salvo em:[/] {jsonPath}");

        // 5b. Baixa os arquivos alterados para a pasta da PR
        var fileChanges = changes.Where(c => c.Item != null && !c.Item.IsFolder).ToList();

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("Baixando arquivos alterados", maxValue: fileChanges.Count);

                foreach (var change in fileChanges)
                {
                    var filePath   = change.Item!.Path;
                    var commitId   = change.ChangeType.Equals("delete", StringComparison.OrdinalIgnoreCase)
                        ? baseCommit
                        : sourceCommit;

                    task.Description = $"Baixando {filePath}";

                    var bytes = await service.GetFileContentAsync(filePath, commitId);

                    if (bytes.Length > 0)
                    {
                        // Remove a barra inicial do path para montar o caminho relativo
                        var relativePath = filePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                        var destPath     = Path.Combine(prFolder, relativePath);

                        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                        await File.WriteAllBytesAsync(destPath, bytes);
                    }

                    task.Increment(1);
                }
            });

        AnsiConsole.MarkupLine($"[green]Arquivos salvos em:[/] {prFolder}");
    }

    private static void RenderPrPanel(PullRequestDetails pr)
    {
        var content = new Markup(
            $"[bold]Descricao:[/] {Markup.Escape(pr.Description ?? "(sem descricao)")}\n" +
            $"[bold]Autor:[/]     [white]{Markup.Escape(pr.CreatedBy?.DisplayName ?? "Desconhecido")}[/]\n" +
            $"[bold]Status:[/]    [white]{Markup.Escape(pr.Status)}[/]\n" +
            $"[bold]Branch:[/]    [cyan]{Markup.Escape(pr.SourceBranch)}[/] [grey]->[/] [green]{Markup.Escape(pr.TargetBranch)}[/]");

        var panel = new Panel(content)
        {
            Header = new PanelHeader($"[bold]PR #{pr.PullRequestId}:[/] {Markup.Escape(pr.Title)}"),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Blue),
            Padding = new Padding(1, 0)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private static void RenderChangesTable(List<ChangeEntry> changes)
    {
        AnsiConsole.MarkupLine($"[bold]Arquivos alterados ({changes.Count}):[/]");

        var table = new Table { Border = TableBorder.Rounded };
        table.AddColumn(new TableColumn("[bold]Arquivo[/]").LeftAligned());
        table.AddColumn(new TableColumn("[bold]Mudanca[/]").Centered());

        foreach (var change in changes)
        {
            var path = change.Item?.Path ?? "(desconhecido)";
            var changeLabel = change.ChangeType.ToLowerInvariant() switch
            {
                "add"    => "[green]Add[/]",
                "delete" => "[red]Delete[/]",
                "edit"   => "[yellow]Edit[/]",
                "rename" => "[blue]Rename[/]",
                _        => Markup.Escape(change.ChangeType)
            };

            table.AddRow(Markup.Escape(path), changeLabel);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void RenderWorkItemsTable(List<WorkItemOutput> workItems)
    {
        AnsiConsole.MarkupLine($"[bold]Work Items vinculados ({workItems.Count}):[/]");

        var table = new Table { Border = TableBorder.Rounded };
        table.AddColumn(new TableColumn("[bold]ID[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Titulo[/]").LeftAligned());
        table.AddColumn(new TableColumn("[bold]Descricao[/]").LeftAligned());

        foreach (var wi in workItems)
            table.AddRow(Markup.Escape(wi.Id), Markup.Escape(wi.Titulo), Markup.Escape(wi.Descricao ?? string.Empty));

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void RenderFileDiff(string filePath, FileDiffResponse diff)
    {
        AnsiConsole.MarkupLine($"[bold cyan]--- {Markup.Escape(filePath)} ---[/]");

        var changedBlocks = diff.Blocks.Where(b => b.ChangeType != 0).ToList();

        if (changedBlocks.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey](sem alteracoes detectadas)[/]");
            AnsiConsole.WriteLine();
            return;
        }

        foreach (var block in changedBlocks)
        {
            AnsiConsole.MarkupLine(
                $"[grey]@@ -{block.OriginalLineStart},{block.OriginalLinesCount} " +
                $"+{block.ModifiedLineStart},{block.ModifiedLinesCount} @@[/]");

            foreach (var line in block.OLines)
                AnsiConsole.MarkupLine($"[red]- {Markup.Escape(line)}[/]");

            foreach (var line in block.MLines)
                AnsiConsole.MarkupLine($"[green]+ {Markup.Escape(line)}[/]");
        }

        AnsiConsole.WriteLine();
    }
}
