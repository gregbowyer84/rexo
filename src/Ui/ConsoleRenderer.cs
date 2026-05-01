namespace Rexo.Ui;

using Spectre.Console;
using Rexo.Core.Models;

public static class ConsoleRenderer
{
    public static void RenderCommandResult(CommandResult result)
    {
        if (result.Success)
        {
            AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(result.Message ?? "Success")}");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(result.Message ?? "Failed")}");
        }
    }

    public static void RenderStepResults(IReadOnlyList<StepResult> steps)
    {
        if (steps.Count == 0) return;

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Step")
            .AddColumn("Status")
            .AddColumn("Exit Code")
            .AddColumn("Duration");

        foreach (var step in steps)
        {
            var status = step.Success
                ? "[green]passed[/]"
                : step.Outputs.ContainsKey("skipped") ? "[grey]skipped[/]" : "[red]failed[/]";

            table.AddRow(
                Markup.Escape(step.StepId),
                status,
                step.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                $"{step.Duration.TotalMilliseconds:F0}ms");
        }

        AnsiConsole.Write(table);
    }

    public static void RenderVersion(string version)
    {
        AnsiConsole.Write(new FigletText("repo")
            .Color(Color.Blue));

        AnsiConsole.MarkupLine($"[bold]Version:[/] [cyan]{Markup.Escape(version)}[/]");
    }

    public static void RenderDoctorResult(CommandResult result)
    {
        var lines = (result.Message ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.Contains("[OK]", StringComparison.Ordinal))
            {
                AnsiConsole.MarkupLine($"[green]{Markup.Escape(line)}[/]");
            }
            else if (line.Contains("[FAIL]", StringComparison.Ordinal))
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(line)}[/]");
            }
            else
            {
                AnsiConsole.WriteLine(line);
            }
        }
    }

    public static void RenderList(CommandResult result)
    {
        var lines = (result.Message ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("Built-in", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Config-defined", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Aliases", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine($"[bold yellow]{Markup.Escape(line)}[/]");
            }
            else if (line.TrimStart().StartsWith("->", StringComparison.Ordinal))
            {
                AnsiConsole.MarkupLine($"[grey]{Markup.Escape(line)}[/]");
            }
            else
            {
                AnsiConsole.WriteLine(line);
            }
        }
    }

    public static void RenderExplain(CommandResult result)
    {
        var lines = (result.Message ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("Command:", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine($"[bold cyan]{Markup.Escape(line)}[/]");
            }
            else if (line.TrimStart().StartsWith("run:", StringComparison.OrdinalIgnoreCase) ||
                     line.TrimStart().StartsWith("uses:", StringComparison.OrdinalIgnoreCase) ||
                     line.TrimStart().StartsWith("command:", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine($"  [green]{Markup.Escape(line.TrimStart())}[/]");
            }
            else
            {
                AnsiConsole.WriteLine(line);
            }
        }
    }

    public static void RenderError(string message)
    {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] {Markup.Escape(message)}");
    }

    public static void RenderInfo(string message)
    {
        AnsiConsole.MarkupLine($"[blue]Info:[/] {Markup.Escape(message)}");
    }

    public static void RenderRunManifest(RunManifest manifest)
    {
        var status = manifest.Success ? "[green]SUCCESS[/]" : "[red]FAILED[/]";
        AnsiConsole.MarkupLine($"[bold]Run complete:[/] {status}");
        AnsiConsole.MarkupLine($"  Command: [cyan]{Markup.Escape(manifest.CommandExecuted ?? "unknown")}[/]");
        AnsiConsole.MarkupLine($"  Duration: {manifest.Duration.TotalSeconds:F1}s");

        if (manifest.Version is not null)
        {
            AnsiConsole.MarkupLine($"  Version: [cyan]{Markup.Escape(manifest.Version.SemVer)}[/]");
        }

        if (manifest.Steps.Count > 0)
        {
            RenderStepResults(manifest.Steps.Select(s =>
                new StepResult(s.StepId, s.Success, s.ExitCode,
                    TimeSpan.FromMilliseconds(s.DurationMs),
                    new Dictionary<string, object?>())).ToArray());
        }
    }

    /// <summary>
    /// Presents an interactive selection prompt and returns the chosen command name,
    /// or <c>null</c> if the user cancelled or no commands are available.
    /// </summary>
    public static string? PromptCommandPicker(IReadOnlyList<string> commandNames)
    {
        if (commandNames.Count == 0) return null;

        var choices = new List<string>(commandNames) { "(cancel)" };

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold yellow]Select a command to run:[/]")
                .PageSize(20)
                .HighlightStyle(new Style(foreground: Color.Cyan1))
                .AddChoices(choices));

        return selection == "(cancel)" ? null : selection;
    }
}
