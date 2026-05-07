# Lifecycle Builtins

Core versioning, validation, testing, analysis, and verification builtins.

## builtin:resolve-version

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

## builtin:validate

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

## builtin:test

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

## builtin:analyze

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

## builtin:verify

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

## builtin:clean

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
