# Rexo — Claude Code Instructions

This file is read automatically by Claude Code when working in this repository.
It complements `AGENTS.md`, which contains the full technical reference.

---

## Build

```bash
dotnet build solution.slnx -c Release
dotnet test solution.slnx -c Release --no-build
```

Build must be clean: **0 errors, 0 warnings**. Tests must all pass before committing.

---

## Project at a glance

- **Product**: Rexo — config-driven repository automation CLI
- **CLI command**: `rx`
- **Stack**: .NET 10, C#, xUnit, Spectre.Console, NJsonSchema
- **Solution**: `solution.slnx` (15 src + 4 test projects)
- **Config file**: `repo.json` — requires `$schema` + `schemaVersion: "1.0"`
- **Schema**: `rexo.schema.json` (repo root)

---

## Critical conventions

1. **`src/Core/` has zero project references** — never add a `<ProjectReference>` here.
2. **Namespace prefix in source files is `Rexo.*`** (e.g. `namespace Rexo.Cli;`).
   Follow the namespace that already exists in a file when adding new types.
3. **CancellationToken** must be threaded through every async method.
4. **`int.ToString()`** must always pass `CultureInfo.InvariantCulture`.
5. **No inline constant arrays** (`new[] { ... }`) inside methods called in a loop —
   move to `static readonly`.
6. Use `.Count` (not `.Length`) on `IReadOnlyList<T>`.
7. New packages: add version to `Directory.Packages.props`; reference in `.csproj`
   without a version.

---

## What is implemented

See `docs/todo.md` for the complete checklist. Working today:

- CLI routing (built-in + config commands, multi-word resolution, global flags)
- Config loading with JSON Schema validation
- Template engine with filters (`slug`, `upper`, `lower`, `default(...)`)
- All built-in step primitives (validate, resolve-version, test, analyze, verify, artifacts lifecycle, config-resolved, config-materialize, etc.)
- Version providers: `fixed`, `env`, `gitversion`, `minver`, `nbgv`, `git`
- Artifact providers: `docker`, `nuget`, `helm-oci`
- Git + CI environment detection
- Spectre.Console rich output renderer + Blazor/RazorConsole interactive TUI (`rx ui`)
- `dotnet test` verification runner with TRX + Cobertura coverage thresholds
- `dotnet format` + build analysis runner
- `extends` config merge, policy-provided commands, parallel step execution, output capture
- `config resolved` / `config sources` / `config materialize` sub-commands
- Artifact manifest file output, secret masking, structured error taxonomy
- 189 passing tests

## What is not yet implemented

The implementation is feature-complete per `docs/scope.md`. No known gaps remain.

---

## Key files

| File | Purpose |
| ------ | --------- |
| `AGENTS.md` | Full technical reference for AI agents |
| `docs/scope.md` | Complete product scope (source of truth) |
| `docs/todo.md` | Implementation checklist |
| `src/Cli/Program.cs` | CLI entry point and routing |
| `src/Core/Abstractions/` | All interfaces |
| `src/Core/Models/` | All domain models |
| `src/Execution/ConfigCommandLoader.cs` | Built-in primitive registration |
| `src/Execution/StepExecutor.cs` | Step execution loop |
| `src/Templating/TemplateRenderer.cs` | Template variable/filter engine |
| `src/Configuration/RepoConfigurationLoader.cs` | Config load + schema validation |
| `rexo.schema.json` | JSON Schema for `repo.json` |

