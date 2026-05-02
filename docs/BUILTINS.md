# Builtins Reference

This document describes runtime builtins used by `uses: "builtin:<name>"` steps.

Scope:

- Step-level builtins registered in `ConfigCommandLoader.RegisterBuiltins`
- Their inputs, internal calls, outputs, side effects, and exit behavior

For embedded policy command flows, see `docs/EMBEDDED.md`.

## Execution Model

A builtin is invoked by a step such as:

```json
{
  "id": "push",
  "uses": "builtin:push-artifacts",
  "with": {
    "confirm": "{{options.push}}"
  }
}
```

Input channels available to all builtins:

- Step metadata: `id`, `uses`, `when`, `with`, `outputPattern`, `outputFile`
- Execution context: repo root, branch, commit, CI flags, version, completed steps
- Command args/options: `ctx.Args`, `ctx.Options`
- Repository config: artifacts, tests, analysis, versioning, push rules

`with` mapping behavior:

- Step `with` values are rendered and merged into step-local options before `when` evaluation and builtin execution.
- This is how higher-level command options are passed into reusable builtins.

## Core Lifecycle Builtins

### builtin:resolve-version

Purpose:

- Resolve semantic version metadata and populate execution context version.

Calls:

- `VersionProviderRegistry.Resolve(...)`
- provider `ResolveAsync(...)`

Inputs:

- `config.Versioning` provider/fallback/settings
- Context: branch, commit, env

Outputs (`StepResult.Outputs`):

- `__version` (full `VersionResult` object)
- `semver`, `major`, `minor`, `patch`
- `prerelease`, `buildMetadata`
- `branch`, `commitSha`, `shortSha`
- `assemblyVersion`, `fileVersion`, `informationalVersion`, `nugetVersion`, `dockerVersion`
- `isPrerelease`, `isStable`, `commitsSinceVersionSource`

Exit behavior:

- Success: exit code `0`
- Provider errors bubble and fail command execution

### builtin:validate

Purpose:

- Validate configuration state (logical placeholder gate in current implementation).

Calls:

- No external runner; emits success marker.

Inputs:

- Context/config (read-only)

Outputs:

- `message = "Configuration is valid."`

Exit behavior:

- Always success, exit code `0`

### builtin:test

Purpose:

- Run repository tests and capture summary counts.

Calls:

- `DotnetTestRunner.RunAsync(...)`

Inputs:

- `config.Tests` values:
- `enabled`, `projects`, `configuration`, `resultsOutput`, `coverageOutput`, `coverageThreshold`

Outputs:

- `total`, `passed`, `failed`, `skipped`

Exit behavior:

- Success: exit code `0`
- Failures: exit code `4`

### builtin:analyze

Purpose:

- Run formatting/analysis checks and optional custom analysis tools.

Calls:

- `DotnetAnalysisRunner.RunFormatCheckAsync(...)`
- optional `DotnetAnalysisRunner.RunCustomToolAsync(...)` for each configured tool
- optional `DotnetAnalysisRunner.WriteSarifReportAsync(...)`

Inputs:

- `config.Analysis` values:
- `failOnIssues`, `tools[]`, `configuration` (SARIF path)

SARIF path behavior:

- When `analysis.configuration` is set to a `.sarif`/`.sarif.json` path, that path is used.
- When omitted, SARIF defaults to `<runtime.output.root>/analysis.sarif.json`
  (fallback root: `artifacts`).

Outputs:

- Success path: `message = "Analysis passed."`
- Failure path: `error` with issue summary

Exit behavior:

- Success: exit code `0`
- Failure: exit code `1`

### builtin:verify

Purpose:

- Quality gate primitive for `test + analyze`.

Calls:

- builtin dispatch to `builtin:test`
- builtin dispatch to `builtin:analyze`

Inputs:

- Same as test/analyze via delegated calls

Outputs:

- Success path: `message = "Verification passed."`
- Delegated outputs on failure

Exit behavior:

- Success: exit code `0`
- Failures propagate from delegated builtin

### builtin:clean

Purpose:

- Remove generated output directory under repo root.

Calls:

- `Directory.Delete(repo/artifacts, recursive: true)` if present

Inputs:

- Repository root

Outputs:

- `message` (`Cleaned ...` or `Nothing to clean.`)
- `cleaned` (list of deleted paths)

Exit behavior:

- Returns success (`0`) even if folder is absent
- Errors during delete are logged and command still returns success in current behavior

## Artifact Lifecycle Builtins

### builtin:plan-artifacts

Purpose:

- Produce human-readable plan and structured JSON model for matching artifacts.

Calls:

- Internal planning logic only (no provider build/tag/push calls)

Inputs:

- Artifact selection predicate (caller-provided)
- Context version/branch/commit/PR/clean-tree flags
- Option `push` (typically mapped via `with`) to indicate push intent

Outputs:

- `message`
- `plan` (JSON string of per-artifact plan model)
- `pushRequested` (`bool`)
- `canPush` (`bool`)
- `skipReasons` (`string[]`)

Exit behavior:

- Success: exit code `0`
- No artifacts: success with informative message

### builtin:build-artifacts

Purpose:

- Build matching artifacts via provider implementations.

Calls:

- For each artifact: provider `BuildAsync(...)`

Inputs:

- `config.Artifacts`
- Context (includes resolved version for tagging/build args where providers use it)

Outputs:

- `message`

Exit behavior:

- Success: exit code `0`
- Build failure: exit code `5`

### builtin:tag-artifacts

Purpose:

- Tag matching artifacts via provider implementations.

Calls:

- For each artifact: provider `TagAsync(...)`

Inputs:

- `config.Artifacts`
- Context (version/branch metadata)

Outputs:

- `message`

Exit behavior:

- Success: exit code `0`
- Provider errors may bubble and fail execution

### builtin:push-artifacts

Purpose:

- Push matching artifacts with confirmation and push-policy gates.

Calls:

- Parse global push rules from `config.runtime.push`
- Merge per-artifact push overrides from artifact settings
- Enforce local explicit confirmation (`confirm`/`push` option)
- For allowed artifacts: provider `PushAsync(...)`
- Writes `<runtime.output.root>/manifest.json` when `runtime.output.emitRuntimeFiles=true` (default)

Inputs:

- `ctx.Options.confirm` and/or `ctx.Options.push`
- CI context (`ctx.IsCi`)
- Global push rules (`runtime.push`)
- Per-artifact settings:
- `push.enabled`, `push.noPushInPullRequest`, `push.requireCleanWorkingTree`, `push.branches`
- legacy synonyms (`pushEnabled`, `pushBranches`, etc.)

Outputs:

- `message`
- `__artifacts` (`ArtifactManifestEntry[]`)
- `__pushDecisions` (`PushDecision[]`)
- On failure: `error`

Exit behavior:

- Local, not confirmed: success `0`, push skipped with guidance
- Policy-gated skip: success `0` with decision reasons
- Provider push failure: exit code `6`

## Composite Convenience Builtins

These chain core lifecycle builtins for reusable shorthand behavior.

### builtin:ship-artifacts

Calls:

1. `tag-artifacts`
2. `push-artifacts`

### builtin:all-artifacts

Calls:

1. `build-artifacts`
2. `tag-artifacts`
3. `push-artifacts`

### builtin:plan

Calls:

- `plan-artifacts` (all artifact types)

### builtin:ship

Calls:

- `ship-artifacts`

### builtin:all

Calls:

- `all-artifacts`

## Docker-Scoped Builtins

### builtin:docker-plan

Calls:

- `plan-artifacts` filtered to docker artifacts only

### builtin:docker-ship

Calls:

1. `tag-artifacts` (docker only)
2. `push-artifacts` (docker only)

### builtin:docker-all

Calls:

1. `build-artifacts` (docker only)
2. `tag-artifacts` (docker only)
3. `push-artifacts` (docker only)

### builtin:docker-stage

Purpose:

- Build a single named docker stage from artifact `settings.stages`.

Calls:

- For each docker artifact, provider `BuildAsync(...)` with stage-focused settings

Inputs:

- Stage name from either:
- `ctx.Args.stage`
- `ctx.Options.stage`
- Artifact setting object: `settings.stages.<stageName>`

Outputs:

- Success: `message = "Docker stage '<name>' completed."`
- Failure: `error`

Exit behavior:

- Missing stage argument: exit code `2`
- Missing stage config for artifact: exit code `2`
- Build failure: exit code `5`

## Configuration Utility Builtins

### builtin:config-resolved

Purpose:

- Emit effective loaded config as JSON.

Calls:

- `JsonSerializer.Serialize(config)`

Outputs:

- `json` (serialized config)

Exit behavior:

- Success: exit code `0`

### builtin:config-materialize

Purpose:

- Materialize provider-side files when needed (currently GitVersion bootstrap support).

Calls:

- If version provider is gitversion and file absent:
- Write `GitVersion.yml`

Outputs:

- `message`
- `files` (comma-separated written file paths)

Exit behavior:

- Success: exit code `0`

## Common Input Patterns

### Passing push intent from command option to builtin

```json
{
  "id": "push",
  "uses": "builtin:push-artifacts",
  "with": {
    "confirm": "{{options.push}}"
  }
}
```

### Planning with push semantics

```json
{
  "id": "plan",
  "uses": "builtin:plan-artifacts",
  "with": {
    "push": "{{options.push}}"
  }
}
```

### Stage-targeted docker build

```json
{
  "id": "docker-stage",
  "uses": "builtin:docker-stage",
  "with": {
    "stage": "publish"
  }
}
```

## Approximate Shell Equivalents

These mappings are intentionally approximate.

- Builtins may apply policy gates, config defaults, provider-specific behavior, and
  richer output handling that plain shell commands do not.
- Use this section as a mental model, not as an exact 1:1 implementation contract.

### Core lifecycle builtins

| Builtin | Approximate shell behavior |
| --- | --- |
| `builtin:resolve-version` | `gitversion /output json` or equivalent provider command, then map fields into execution context |
| `builtin:validate` | Logical validation gate; no direct external command in current implementation |
| `builtin:test` | `dotnet test` with options from `config.tests` |
| `builtin:analyze` | `dotnet format --verify-no-changes` plus any configured custom analysis commands |
| `builtin:verify` | Run `builtin:test` then `builtin:analyze` |
| `builtin:clean` | Remove `<runtime.output.root>/` recursively (default `artifacts/`) |

### Artifact lifecycle builtins

| Builtin | Approximate shell behavior |
| --- | --- |
| `builtin:plan-artifacts` | Read artifact config + context and print a computed plan (no build/tag/push mutation) |
| `builtin:build-artifacts` | For each artifact provider, run equivalent build (`docker build`, `dotnet pack`, etc.) |
| `builtin:tag-artifacts` | For each artifact provider, apply version tags (`docker tag`, package version tagging flows) |
| `builtin:push-artifacts` | Enforce push gates then run provider push (`docker push`, `dotnet nuget push`, etc.), then write `<runtime.output.root>/manifest.json` when enabled |

### Composite convenience builtins

| Builtin | Approximate shell behavior |
| --- | --- |
| `builtin:ship-artifacts` | `tag-artifacts` then `push-artifacts` |
| `builtin:all-artifacts` | `build-artifacts` then `tag-artifacts` then `push-artifacts` |
| `builtin:plan` | Alias-style wrapper around `plan-artifacts` |
| `builtin:ship` | Alias-style wrapper around `ship-artifacts` |
| `builtin:all` | Alias-style wrapper around `all-artifacts` |

### Docker-scoped builtins

| Builtin | Approximate shell behavior |
| --- | --- |
| `builtin:docker-plan` | `plan-artifacts` filtered to docker artifact type |
| `builtin:docker-ship` | docker-only `tag-artifacts` then docker-only `push-artifacts` |
| `builtin:docker-all` | docker-only `build-artifacts` then `tag-artifacts` then `push-artifacts` |
| `builtin:docker-stage` | Build one named stage from `settings.stages.<name>` for each docker artifact |

### Configuration utility builtins

| Builtin | Approximate shell behavior |
| --- | --- |
| `builtin:config-resolved` | Serialize effective config to JSON and print |
| `builtin:config-materialize` | Generate provider-side helper files (currently `GitVersion.yml` when needed) |

### Concrete examples

`builtin:test` (approximate):

```bash
dotnet test -c Release
```

`builtin:analyze` (approximate):

```bash
dotnet format --verify-no-changes
```

`builtin:build-artifacts` for docker artifact (approximate):

```bash
docker build -f Dockerfile -t ghcr.io/org/app:1.2.3 .
```

`builtin:push-artifacts` for docker artifact (approximate, after gates pass):

```bash
docker push ghcr.io/org/app:1.2.3
```

`builtin:push-artifacts` for nuget artifact (approximate, after gates pass):

```bash
dotnet nuget push artifacts/packages/*.nupkg --source <source>
```

## Source Of Truth

Implementation source:

- `src/Execution/ConfigCommandLoader.cs`
- `src/Execution/StepExecutor.cs`

When behavior appears to differ from docs, treat code as authoritative and update this file.
