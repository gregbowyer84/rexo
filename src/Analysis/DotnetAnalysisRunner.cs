namespace Rexo.Analysis;

using System.Text.Json;
using Rexo.Core.Models;

public sealed record AnalysisResult(
    bool Success,
    string Tool,
    string? Output,
    IReadOnlyList<string> Issues);

public static class DotnetAnalysisRunner
{
    private static readonly string[] FormatIssueMessages =
        ["Code formatting issues detected. Run 'dotnet format' to fix."];

    private static readonly JsonSerializerOptions SarifJsonOptions =
        new() { WriteIndented = true };

    public static async Task<AnalysisResult> RunFormatCheckAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        const string tool = "dotnet-format";
        var args = "format --verify-no-changes --severity warn";

        Console.WriteLine($"  > dotnet {args}");

        var result = await RunDotnetAsync(args, repositoryRoot, cancellationToken);

        return new AnalysisResult(
            Success: result.exitCode == 0,
            Tool: tool,
            Output: result.output,
            Issues: result.exitCode != 0
                ? FormatIssueMessages
                : Array.Empty<string>());
    }

    public static async Task<AnalysisResult> RunBuildAnalysisAsync(
        string repositoryRoot,
        string configuration,
        CancellationToken cancellationToken)
    {
        const string tool = "dotnet-build";
        var args = $"build --configuration {configuration} --no-incremental /warnaserror";

        Console.WriteLine($"  > dotnet {args}");

        var result = await RunDotnetAsync(args, repositoryRoot, cancellationToken);

        var issues = new List<string>();
        if (result.exitCode != 0)
        {
            issues.Add("Build analysis found warnings treated as errors.");
        }

        return new AnalysisResult(
            Success: result.exitCode == 0,
            Tool: tool,
            Output: result.output,
            Issues: issues);
    }

    /// <summary>
    /// Runs a custom analysis tool by shell command. The tool is invoked in the repository root
    /// and its stdout/stderr are captured. Any non-zero exit code is treated as a failure.
    /// </summary>
    /// <param name="toolCommand">The full shell command to execute (e.g. "security-scan --format sarif").</param>
    /// <param name="repositoryRoot">Working directory for the tool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<AnalysisResult> RunCustomToolAsync(
        string toolCommand,
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"  > {toolCommand}");

        var (executable, arguments) = SplitCommand(toolCommand);

        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo(executable, arguments)
            {
                WorkingDirectory = repositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        var combined = string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}\n{stderr}";
        var output = combined.Trim();

        var issues = new List<string>();
        if (process.ExitCode != 0)
        {
            issues.Add($"Custom tool '{executable}' exited with code {process.ExitCode}.");
        }

        return new AnalysisResult(
            Success: process.ExitCode == 0,
            Tool: executable,
            Output: output,
            Issues: issues);
    }

    /// <summary>
    /// Writes a minimal SARIF 2.1.0 report for a collection of analysis results to <paramref name="outputPath"/>.
    /// </summary>
    public static async Task WriteSarifReportAsync(
        IReadOnlyList<AnalysisResult> results,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var rules = results
            .Select((r, i) => (object)new
            {
                id = $"RX{i + 1:D4}",
                name = r.Tool,
                shortDescription = new { text = r.Tool },
            })
            .ToArray();

        var runs = results.Select((r, i) => new
        {
            tool = new
            {
                driver = new
                {
                    name = "Rexo Analysis",
                    version = "1.0",
                    rules = new[]
                    {
                        new
                        {
                            id = $"RX{i + 1:D4}",
                            name = r.Tool,
                            shortDescription = new { text = r.Tool },
                        }
                    },
                },
            },
            results = r.Issues.Select(issue => new
            {
                ruleId = $"RX{i + 1:D4}",
                level = r.Success ? "note" : "error",
                message = new { text = issue },
            }).ToArray(),
        }).ToArray();

        var sarif = new
        {
            version = "2.1.0",
            schema = "https://json.schemastore.org/sarif-2.1.0.json",
            runs,
        };

        var dir = System.IO.Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);

        await System.IO.File.WriteAllTextAsync(
            outputPath,
            JsonSerializer.Serialize(sarif, SarifJsonOptions),
            cancellationToken);
    }

    /// <summary>Splits a shell command string into executable + arguments.</summary>
    private static (string executable, string arguments) SplitCommand(string command)
    {
        var trimmed = command.Trim();
        var spaceIndex = trimmed.IndexOf(' ', StringComparison.Ordinal);
        if (spaceIndex < 0) return (trimmed, string.Empty);
        return (trimmed[..spaceIndex], trimmed[(spaceIndex + 1)..]);
    }

    private static async Task<(int exitCode, string output)> RunDotnetAsync(
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo("dotnet", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        var combined = string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}\n{stderr}";
        return (process.ExitCode, combined.Trim());
    }
}

