# Lifecycle Builtins

Core lifecycle builtins currently registered by runtime.

Toolchain-specific test/analyze/verify behavior is now delivered by embedded policy command overlays (for example `embedded:dotnet`, `embedded:node`), not core builtins.

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
