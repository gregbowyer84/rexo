# Rexo — GitHub Copilot Workspace Instructions

## Project overview

Rexo (`rx`) is a config-driven repository runtime CLI written in .NET 10 / C#.
A `repo.json` file in a repository root defines commands, versioning, artifacts,
tests, and analysis. The CLI is identical locally and in CI.

Full context: read `AGENTS.md` in the repository root.

## Build and test

```bash
dotnet build solution.slnx -c Release          # must produce 0 errors, 0 warnings
dotnet test solution.slnx -c Release --no-build
```

## Key conventions

- **Zero warnings**: `TreatWarningsAsErrors=true` — every CA/CS analyzer warning is an error
- **Namespace prefix**: Source files use `Rexo.*` namespace prefix (e.g. `namespace Rexo.Cli;`).
  Keep new files consistent with the file's existing namespace prefix.
- **Central NuGet versions**: Add new packages to `Directory.Packages.props` without a version
  in the `.csproj` (central package management is enabled)
- **Core has zero deps**: `src/Core/` must never take a project reference — all abstractions
  live there
- **CancellationToken**: Always thread through to all async methods
- **`int.ToString()`**: Always pass `CultureInfo.InvariantCulture`
- **Static readonly arrays**: Do not use `new[] { ... }` inline in methods called in a loop

## Project layout

```
src/Core/              Domain models + interfaces (zero project deps)
src/Configuration/     repo.json loading with NJsonSchema validation
src/Execution/         Step executor, command registry, built-in primitives
src/Templating/        {{var | filter}} engine
src/Cli/               Entry point — packs as `rx` dotnet tool
src/Versioning/        fixed / env / gitversion / minver / nbgv / git providers
src/Artifacts/         IArtifactProvider + registry
src/Artifacts.Docker/  Docker build/tag/push
src/Artifacts.Helm/    Helm OCI chart build/tag/push
src/Artifacts.NuGet/   dotnet pack/push
src/Git/               Branch/SHA/remote detector
src/Ci/                CI provider detector
src/Tui/               Blazor/RazorConsole interactive TUI (`rx ui`)
src/Ui/                Spectre.Console rich renderer
src/Verification/      dotnet test runner
src/Analysis/          dotnet format + build analysis
src/Policies/          Local file policy source
```

## Config model (`repo.json`)

Every `repo.json` must have `$schema` and `schemaVersion: "1.0"`.
Schema file: `rexo.schema.json` (repo root).
Loader: `src/Configuration/RepoConfigurationLoader.cs`.

## Current status

The implementation is feature-complete per `docs/scope.md`. All 189 tests pass.
See `docs/todo.md` for the full checklist.

