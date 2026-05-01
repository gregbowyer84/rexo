namespace Rexo.Execution;

using System.Diagnostics;
using System.Runtime.InteropServices;

public sealed record ShellRunResult(int ExitCode, string Stdout, string Stderr);

public static class ShellRunner
{
    public static async Task<ShellRunResult> RunAsync(
        string command,
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? environment = null,
        Action<string>? onStdout = null,
        CancellationToken cancellationToken = default)
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var (fileName, arguments) = isWindows
            ? ("cmd.exe", $"/c \"{command.Replace("\"", "\\\"")}\"")
            : ("/bin/sh", $"-c \"{command.Replace("\"", "\\\"").Replace("'", "\\'")}\"");

        var psi = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (environment is not null)
        {
            foreach (var kv in environment)
            {
                if (kv.Value is not null)
                    psi.Environment[kv.Key] = kv.Value;
                else
                    psi.Environment.Remove(kv.Key);
            }
        }

        using var process = new Process { StartInfo = psi };
        var stdoutLines = new System.Text.StringBuilder();
        var stderrLines = new System.Text.StringBuilder();

        process.Start();

        var stdoutTask = ReadStreamAsync(process.StandardOutput, stdoutLines, onStdout, cancellationToken);
        var stderrTask = ReadStreamAsync(process.StandardError, stderrLines, null, cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        await stdoutTask;
        await stderrTask;

        return new ShellRunResult(
            process.ExitCode,
            stdoutLines.ToString().Trim(),
            stderrLines.ToString().Trim());
    }

    private static async Task ReadStreamAsync(
        System.IO.TextReader reader,
        System.Text.StringBuilder buffer,
        Action<string>? onLine,
        CancellationToken cancellationToken)
    {
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            buffer.AppendLine(line);
            onLine?.Invoke(line);
        }
    }
}
