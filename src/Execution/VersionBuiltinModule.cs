namespace Rexo.Execution;

using Rexo.Core.Models;

internal sealed class VersionBuiltinModule : IConfigBuiltinModule
{
    public void Register(BuiltinRegistry registry, ConfigBuiltinModuleContext context)
    {
        registry.Register("builtin:resolve-version", async (step, ctx, ct) =>
        {
            var versioningConfig = context.Config.Versioning is not null
                ? new VersioningConfig(
                    context.Config.Versioning.Provider,
                    context.Config.Versioning.Fallback,
                    context.Config.Versioning.Settings)
                : new VersioningConfig("auto", "0.1.0-local");

            var provider = context.Loader.VersionProviders.Resolve(versioningConfig.Provider);
            var versionResult = await provider.ResolveAsync(versioningConfig, ctx, ct);

            Console.WriteLine($"  Resolved version: {versionResult.SemVer}");

            return new StepResult(
                step.Id ?? "resolve-version",
                true,
                0,
                TimeSpan.Zero,
                new Dictionary<string, object?>
                {
                    ["__version"] = versionResult,
                    ["semver"] = versionResult.SemVer,
                    ["major"] = versionResult.Major.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["minor"] = versionResult.Minor.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["patch"] = versionResult.Patch.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["prerelease"] = versionResult.PreRelease,
                    ["buildMetadata"] = versionResult.BuildMetadata,
                    ["branch"] = versionResult.Branch,
                    ["commitSha"] = versionResult.CommitSha,
                    ["shortSha"] = versionResult.ShortSha,
                    ["assemblyVersion"] = versionResult.AssemblyVersion,
                    ["fileVersion"] = versionResult.FileVersion,
                    ["informationalVersion"] = versionResult.InformationalVersion,
                    ["nugetVersion"] = versionResult.NuGetVersion,
                    ["dockerVersion"] = versionResult.DockerVersion,
                    ["isPrerelease"] = versionResult.IsPreRelease.ToString().ToLowerInvariant(),
                    ["isStable"] = versionResult.IsStable.ToString().ToLowerInvariant(),
                    ["commitsSinceVersionSource"] = versionResult.CommitsSinceVersionSource?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                });
        });
    }
}
