namespace Rexo.Verification;

using Rexo.Core.Models;

public sealed record VerificationConfig(
    bool Enabled,
    string[]? Projects,
    string Configuration,
    string? ResultsOutput,
    string? CoverageOutput,
    int? LineCoverageThreshold,
    int? BranchCoverageThreshold);

public sealed record VerificationResult(
    bool Success,
    int TotalTests,
    int PassedTests,
    int FailedTests,
    int SkippedTests,
    double? LineCoverage,
    string? ResultsPath,
    string? CoveragePath);

public static class DotnetTestRunner
{
    public static async Task<VerificationResult> RunAsync(
        VerificationConfig config,
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        if (!config.Enabled)
        {
            return new VerificationResult(true, 0, 0, 0, 0, null, null, null);
        }

        var outputDir = config.ResultsOutput ?? "artifacts/tests";
        Directory.CreateDirectory(Path.Combine(repositoryRoot, outputDir));

        var projectPattern = config.Projects?.Length > 0
            ? string.Join(" ", config.Projects)
            : "";

        var testArgs = $"test {projectPattern} --configuration {config.Configuration} --no-build"
                     + $" --results-directory {outputDir} --logger trx";

        if (config.CoverageOutput is not null)
        {
            Directory.CreateDirectory(Path.Combine(repositoryRoot, config.CoverageOutput));
            testArgs += $" --collect:\"XPlat Code Coverage\" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.OutputDirectory={config.CoverageOutput}";
        }

        Console.WriteLine($"  > dotnet {testArgs}");

        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo("dotnet", testArgs)
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

        if (!string.IsNullOrWhiteSpace(stdout)) Console.WriteLine(stdout);
        if (!string.IsNullOrWhiteSpace(stderr)) Console.Error.WriteLine(stderr);

        var (total, passed, failed, skipped) = ParseTestOutput(stdout);

        // Attempt to read line coverage from Cobertura XML if output was collected
        double? lineCoverage = null;
        if (config.CoverageOutput is not null)
        {
            lineCoverage = TryReadLineCoverage(Path.Combine(repositoryRoot, config.CoverageOutput));
        }
        // Enforce coverage threshold if configured
        var coverageFailed = false;
        if (config.LineCoverageThreshold.HasValue && lineCoverage.HasValue)
        {
            var threshold = config.LineCoverageThreshold.Value;
            if (lineCoverage.Value < threshold)
            {
                Console.Error.WriteLine(
                    $"  Coverage threshold not met: {lineCoverage.Value:F1}% < {threshold}% required.");
                coverageFailed = true;
            }
        }

        return new VerificationResult(
            Success: process.ExitCode == 0 && !coverageFailed,
            TotalTests: total,
            PassedTests: passed,
            FailedTests: failed,
            SkippedTests: skipped,
            LineCoverage: lineCoverage,
            ResultsPath: outputDir,
            CoveragePath: config.CoverageOutput);
    }

    private static double? TryReadLineCoverage(string coverageDir)
    {
        try
        {
            var xmlFiles = Directory.GetFiles(coverageDir, "coverage.cobertura.xml", SearchOption.AllDirectories);
            if (xmlFiles.Length == 0) return null;

            using var reader = System.Xml.XmlReader.Create(xmlFiles[0]);
            while (reader.Read())
            {
                if (reader.NodeType == System.Xml.XmlNodeType.Element && reader.Name == "coverage")
                {
                    var lineRate = reader.GetAttribute("line-rate");
                    if (double.TryParse(lineRate, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var rate))
                    {
                        return rate * 100.0;
                    }
                }
            }
        }
        catch (Exception)
        {
            // coverage XML not available — skip
        }

        return null;
    }

    private static (int total, int passed, int failed, int skipped) ParseTestOutput(string output)
    {
        // Parse "Test summary: total: 5, failed: 0, succeeded: 5, skipped: 0"
        var totalMatch = System.Text.RegularExpressions.Regex.Match(output, @"total:\s*(\d+)");
        var failedMatch = System.Text.RegularExpressions.Regex.Match(output, @"failed:\s*(\d+)");
        var passedMatch = System.Text.RegularExpressions.Regex.Match(output, @"succeeded:\s*(\d+)");
        var skippedMatch = System.Text.RegularExpressions.Regex.Match(output, @"skipped:\s*(\d+)");

        var total = totalMatch.Success ? int.Parse(totalMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
        var failed = failedMatch.Success ? int.Parse(failedMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
        var passed = passedMatch.Success ? int.Parse(passedMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : total - failed;
        var skipped = skippedMatch.Success ? int.Parse(skippedMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;

        return (total, passed, failed, skipped);
    }
}
