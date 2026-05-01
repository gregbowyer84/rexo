namespace Rexo.Templating;

using System.Text.RegularExpressions;
using Rexo.Core.Abstractions;
using Rexo.Core.Models;

public sealed class TemplateRenderer : ITemplateRenderer
{
    private static readonly Regex ExpressionPattern =
        new(@"\{\{([^}]+)\}\}", RegexOptions.Compiled, TimeSpan.FromSeconds(5));

    private static readonly Regex SlugCleanPattern =
        new(@"[^a-z0-9]+", RegexOptions.Compiled, TimeSpan.FromSeconds(5));

    public string Render(string templateText, ExecutionContext context)
    {
        var root = BuildContext(context);
        return ExpressionPattern.Replace(templateText, match =>
            EvaluateExpression(match.Groups[1].Value.Trim(), root));
    }

    private static string EvaluateExpression(string expr, Dictionary<string, object?> root)
    {
        // Check for equality/inequality expressions before pipe-filter handling
        if (expr.Contains(" == ", StringComparison.Ordinal))
        {
            var idx = expr.IndexOf(" == ", StringComparison.Ordinal);
            var left = ResolveValue(expr[..idx].Trim(), root);
            var right = ResolveValue(expr[(idx + 4)..].Trim(), root);
            return string.Equals(left, right, StringComparison.Ordinal) ? "true" : "false";
        }

        if (expr.Contains(" != ", StringComparison.Ordinal))
        {
            var idx = expr.IndexOf(" != ", StringComparison.Ordinal);
            var left = ResolveValue(expr[..idx].Trim(), root);
            var right = ResolveValue(expr[(idx + 4)..].Trim(), root);
            return !string.Equals(left, right, StringComparison.Ordinal) ? "true" : "false";
        }

        var pipeIndex = expr.IndexOf('|', StringComparison.Ordinal);
        string path;
        string? filter = null;
        string? filterArg = null;

        if (pipeIndex >= 0)
        {
            path = expr[..pipeIndex].Trim();
            var filterPart = expr[(pipeIndex + 1)..].Trim();
            var parenIndex = filterPart.IndexOf('(', StringComparison.Ordinal);
            if (parenIndex >= 0)
            {
                filter = filterPart[..parenIndex].Trim();
                var closeIndex = filterPart.LastIndexOf(')');
                filterArg = closeIndex > parenIndex
                    ? filterPart[(parenIndex + 1)..closeIndex].Trim().Trim('\'', '"')
                    : string.Empty;
            }
            else
            {
                filter = filterPart;
            }
        }
        else
        {
            path = expr;
        }

        var value = ResolvePath(path, root);
        var result = value?.ToString() ?? string.Empty;

        return filter?.Trim() switch
        {
            "default" => string.IsNullOrEmpty(result) ? (filterArg ?? string.Empty) : result,
            "slug" => Slug(result),
            "upper" => result.ToUpperInvariant(),
            "lower" => result.ToLowerInvariant(),
            "trim" => result.Trim(),
            "basename" => Path.GetFileName(result),
            "dirname" => Path.GetDirectoryName(result) ?? string.Empty,
            "fileext" => Path.GetExtension(result),
            "filestem" => Path.GetFileNameWithoutExtension(result),
            "urlencode" => Uri.EscapeDataString(result),
            "sha256" => ComputeSha256Hex(result),
            "replace" when filterArg is not null => ApplyReplace(result, filterArg),
            "truncate" when filterArg is not null && int.TryParse(filterArg, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var len)
                => result.Length > len ? result[..len] : result,
            "first" when filterArg is not null && int.TryParse(filterArg, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n)
                => result.Length > n ? result[..n] : result,
            _ => result,
        };
    }

    private static object? ResolvePath(string path, Dictionary<string, object?> root)
    {
        if (path.StartsWith("env.", StringComparison.OrdinalIgnoreCase))
        {
            return Environment.GetEnvironmentVariable(path[4..]);
        }

        var parts = path.Split('.');
        object? current = root;

        foreach (var part in parts)
        {
            current = current switch
            {
                Dictionary<string, object?> d when d.TryGetValue(part, out var v) => v,
                IReadOnlyDictionary<string, object?> rd when rd.TryGetValue(part, out var v) => v,
                _ => null,
            };

            if (current is null) return null;
        }

        return current;
    }

    /// <summary>
    /// Resolves a value that may be a quoted string literal (single or double quotes)
    /// or a context path reference. Returns the string value in either case.
    /// </summary>
    private static string ResolveValue(string expr, Dictionary<string, object?> root)
    {
        if (expr.Length >= 2 &&
            ((expr[0] == '\'' && expr[^1] == '\'') ||
             (expr[0] == '"' && expr[^1] == '"')))
        {
            return expr[1..^1];
        }

        return ResolvePath(expr, root)?.ToString() ?? string.Empty;
    }

    private static Dictionary<string, object?> BuildContext(ExecutionContext context)
    {
        var args = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in context.Args) args[kv.Key] = kv.Value;

        var options = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in context.Options) options[kv.Key] = kv.Value;

        var repo = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["root"] = context.RepositoryRoot,
            ["branch"] = context.Branch,
            ["commitSha"] = context.CommitSha,
            ["shortSha"] = context.ShortSha,
            ["remoteUrl"] = context.RemoteUrl,
        };

        var ci = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["isCi"] = context.IsCi.ToString().ToLowerInvariant(),
            ["provider"] = context.CiProvider,
        };

        var steps = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in context.CompletedSteps)
        {
            var outputs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in kv.Value.Outputs) outputs[o.Key] = o.Value;
            steps[kv.Key] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["outputs"] = outputs,
                ["exitCode"] = kv.Value.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["success"] = kv.Value.Success.ToString().ToLowerInvariant(),
            };
        }

        var root = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["args"] = args,
            ["options"] = options,
            ["repo"] = repo,
            ["ci"] = ci,
            ["steps"] = steps,
        };

        if (context.Version is not null)
        {
            root["version"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["semver"] = context.Version.SemVer,
                ["major"] = context.Version.Major.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["minor"] = context.Version.Minor.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["patch"] = context.Version.Patch.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["prerelease"] = context.Version.PreRelease,
                ["commitSha"] = context.Version.CommitSha,
                ["shortSha"] = context.Version.ShortSha,
                ["isPrerelease"] = context.Version.IsPreRelease.ToString().ToLowerInvariant(),
                ["isStable"] = context.Version.IsStable.ToString().ToLowerInvariant(),
            };
        }

        return root;
    }

    private static string Slug(string value) =>
        SlugCleanPattern.Replace(value.ToLowerInvariant(), "-").Trim('-');

    private static string ComputeSha256Hex(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Apply a replace filter with argument format <c>old,new</c> (comma-separated).
    /// </summary>
    private static string ApplyReplace(string value, string filterArg)
    {
        var commaIndex = filterArg.IndexOf(',', StringComparison.Ordinal);
        if (commaIndex < 0) return value;
        var oldValue = filterArg[..commaIndex];
        var newValue = filterArg[(commaIndex + 1)..];
        return value.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
