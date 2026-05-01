namespace Rexo.Analysis;

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
