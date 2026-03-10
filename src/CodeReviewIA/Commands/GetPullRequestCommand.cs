using System.CommandLine;
using CodeReviewIA.Configuration;
using CodeReviewIA.Models;
using CodeReviewIA.Services;
using Spectre.Console;

namespace CodeReviewIA.Commands;

public static class GetPullRequestCommand
{
    private const int MaxDiffFiles = 20;
    private const int MaxLinesNewOrDeleted = 50;

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
            description: "ID ou nome do repositorio.");

        var patOption = new Option<string?>(
            aliases: ["--pat"],
            description: "Personal Access Token do Azure DevOps.");

        var command = new Command("get-pr", "Exibe detalhes de uma Pull Request do Azure DevOps.")
        {
            prIdOption,
            orgOption,
            projectOption,
            repoOption,
            patOption
        };

        command.SetHandler(async (prId, org, project, repo, pat) =>
        {
            // Argumentos CLI sobrescrevem o appsettings.json
            if (!string.IsNullOrWhiteSpace(org))     settings.OrganizationUrl = org;
            if (!string.IsNullOrWhiteSpace(project)) settings.Project = project;
            if (!string.IsNullOrWhiteSpace(repo))    settings.RepositoryId = repo;
            if (!string.IsNullOrWhiteSpace(pat))     settings.PersonalAccessToken = pat;

            try
            {
                settings.Validate();
                await ExecuteAsync(service, prId);
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
        prIdOption, orgOption, projectOption, repoOption, patOption);

        return command;
    }

    private static async Task ExecuteAsync(IAzureDevOpsService service, int prId)
    {
        // 1. Detalhes da PR
        PullRequestDetails pr = null!;
        await AnsiConsole.Status().StartAsync("Buscando detalhes da PR...", async _ =>
        {
            pr = await service.GetPullRequestDetailsAsync(prId);
        });

        RenderPrPanel(pr);

        // 2. Iteracoes
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

        // Base para diffs: usa o commit comum (merge base) quando disponivel
        var baseCommit = string.IsNullOrWhiteSpace(commonCommit) ? targetCommit : commonCommit;

        // 3. Arquivos alterados
        List<ChangeEntry> changes = null!;
        await AnsiConsole.Status().StartAsync($"Buscando arquivos alterados (iteracao {lastIteration.Id})...", async _ =>
        {
            changes = await service.GetPullRequestChangedFilesAsync(prId, lastIteration.Id);
        });

        RenderChangesTable(changes);

        // 4. Diffs
        var filesToDiff = changes
            .Where(c => c.Item != null && !c.Item.IsFolder)
            .Take(MaxDiffFiles)
            .ToList();

        if (changes.Count > MaxDiffFiles)
            AnsiConsole.MarkupLine($"[grey]Exibindo diffs dos primeiros {MaxDiffFiles} de {changes.Count} arquivo(s).[/]\n");

        foreach (var change in filesToDiff)
        {
            var filePath = change.Item!.Path;

            FileDiffResponse? diff = null;
            string[] baseLines = [];
            string[] targetLines = [];

            await AnsiConsole.Status().StartAsync($"Carregando diff: {filePath}...", async _ =>
            {
                if (change.ChangeType.Equals("add", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(sourceCommit))
                        targetLines = SplitLines(await service.GetFileContentAsync(filePath, sourceCommit));
                }
                else if (change.ChangeType.Equals("delete", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(baseCommit))
                        baseLines = SplitLines(await service.GetFileContentAsync(filePath, baseCommit));
                }
                else
                {
                    diff = await service.GetFileDiffAsync(baseCommit, sourceCommit, filePath);
                    if (!string.IsNullOrWhiteSpace(baseCommit))
                        baseLines = SplitLines(await service.GetFileContentAsync(filePath, baseCommit));
                    if (!string.IsNullOrWhiteSpace(sourceCommit))
                        targetLines = SplitLines(await service.GetFileContentAsync(filePath, sourceCommit));
                }
            });

            RenderFileDiff(filePath, change.ChangeType, diff, baseLines, targetLines);
        }
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

    private static void RenderFileDiff(
        string filePath,
        string changeType,
        FileDiffResponse? diff,
        string[] baseLines,
        string[] targetLines)
    {
        AnsiConsole.MarkupLine($"[bold cyan]--- {Markup.Escape(filePath)} ---[/]");

        if (changeType.Equals("add", StringComparison.OrdinalIgnoreCase))
        {
            var lines = targetLines.Take(MaxLinesNewOrDeleted).ToArray();
            foreach (var line in lines)
                AnsiConsole.MarkupLine($"[green]+ {Markup.Escape(line)}[/]");

            if (targetLines.Length > MaxLinesNewOrDeleted)
                AnsiConsole.MarkupLine($"[grey]... ({targetLines.Length - MaxLinesNewOrDeleted} linhas omitidas)[/]");

            AnsiConsole.WriteLine();
            return;
        }

        if (changeType.Equals("delete", StringComparison.OrdinalIgnoreCase))
        {
            var lines = baseLines.Take(MaxLinesNewOrDeleted).ToArray();
            foreach (var line in lines)
                AnsiConsole.MarkupLine($"[red]- {Markup.Escape(line)}[/]");

            if (baseLines.Length > MaxLinesNewOrDeleted)
                AnsiConsole.MarkupLine($"[grey]... ({baseLines.Length - MaxLinesNewOrDeleted} linhas omitidas)[/]");

            AnsiConsole.WriteLine();
            return;
        }

        // Edit / Rename: usa blocos do diff
        if (diff == null || diff.Blocks.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey](sem alteracoes detectadas no diff)[/]");
            AnsiConsole.WriteLine();
            return;
        }

        foreach (var block in diff.Blocks)
        {
            if (block.ChangeType == 0) continue; // contexto nao alterado

            AnsiConsole.MarkupLine(
                $"[grey]@@ -{block.OriginalLineStart},{block.OriginalLinesCount} " +
                $"+{block.ModifiedLineStart},{block.ModifiedLinesCount} @@[/]");

            for (int i = 0; i < block.OriginalLinesCount; i++)
            {
                var idx = block.OriginalLineStart - 1 + i;
                if (idx >= 0 && idx < baseLines.Length)
                    AnsiConsole.MarkupLine($"[red]- {Markup.Escape(baseLines[idx])}[/]");
            }

            for (int i = 0; i < block.ModifiedLinesCount; i++)
            {
                var idx = block.ModifiedLineStart - 1 + i;
                if (idx >= 0 && idx < targetLines.Length)
                    AnsiConsole.MarkupLine($"[green]+ {Markup.Escape(targetLines[idx])}[/]");
            }
        }

        AnsiConsole.WriteLine();
    }

    private static string[] SplitLines(string content) =>
        content.Split('\n');
}
