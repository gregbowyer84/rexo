# Output Contract

This page defines exactly what Rexo emits for machine-readable output and when fields are populated.

## Flags And Emission

Global flags:

- `--json`: write `CommandResult` JSON to stdout.
- `--json-file <path>`: write `CommandResult` JSON to `<path>`.
- `--quiet`: suppress human console rendering.

When `--json-file` is provided, Rexo also writes a sidecar run manifest next to the JSON file:

- Input file: `artifacts/runs/release.json`
- Sidecar: `artifacts/runs/release-manifest.json`

If the file name does not end in `.json`, sidecar naming falls back to `<path>.manifest.json`.

## CommandResult (`--json` and `--json-file`)

Primary machine payload:

```jsonc
{
  "Command": "release",
  "Success": true,
  "ExitCode": 0,
  "Message": "Command 'release' completed successfully.",
  "Outputs": {},
  "Steps": [
    {
      "StepId": "build",
      "Success": true,
      "ExitCode": 0,
      "Duration": "00:00:12.3456789",
      "Outputs": {
        "message": "Command 'build' completed successfully.",
        "__version": { "SemVer": "1.2.3" }
      }
    }
  ],
  "Version": { "SemVer": "1.2.3" },
  "StructuredErrors": [],
  "Artifacts": [],
  "PushDecisions": []
}
```

### Field Population Rules

`Version`

- Populated when a step resolves/provides version metadata (for example via `builtin:resolve-version`), including nested `command` steps.
- `null` when no executed step produced version metadata.

`Artifacts`

- Populated by `builtin:push-artifacts` output (`__artifacts`).
- Empty when push phase is skipped (for example `release` without `--push`) or when no artifacts are configured.

`PushDecisions`

- Populated by `builtin:push-artifacts` output (`__pushDecisions`).
- Entries include policy allow/deny reasons per artifact.
- Empty when push phase is skipped.

`Steps[*].Outputs`

- Contains per-step details (messages, skip markers, extracted outputs, `__version`, `__artifacts`, `__pushDecisions`, etc.).

## Run Manifest Sidecar (`*-manifest.json`)

Written only when `--json-file` is provided.

Example shape:

```jsonc
{
  "SchemaVersion": "1.0",
  "ToolVersion": "1.0.0+abcdef",
  "RepoName": "runtime-licensing",
  "RepoRoot": "S:\\repos\\nrth\\runtime-licensing",
  "Branch": "feature/x",
  "CommitSha": "...",
  "RemoteUrl": "https://github.com/...",
  "CommandExecuted": "release",
  "Success": true,
  "ExitCode": 0,
  "StartedAt": "...",
  "CompletedAt": "...",
  "Duration": "00:01:21.5500465",
  "Version": { "SemVer": "1.2.3" },
  "Steps": [
    {
      "StepId": "verify",
      "Success": true,
      "ExitCode": 0,
      "DurationMs": 67364.3721,
      "FileOutputs": {}
    }
  ],
  "Artifacts": [],
  "PushDecisions": [],
  "Warnings": [],
  "Errors": [],
  "ConfigHash": "sha256...",
  "AssemblyVersion": "1.2.3.0",
  "InformationalVersion": "1.2.3",
  "NuGetVersion": "1.2.3"
}
```

### Notes

- Sidecar includes repository and CI context, timing, config hash, and normalized step timing (`DurationMs`).
- `Artifacts`/`PushDecisions` follow the same population rules as `CommandResult`.
- `Version` follows the same population rule: populated only if version metadata is produced during executed steps.

## Practical Scenarios

`rx release --json-file artifacts/runs/release.json`

- Build/verify lifecycle runs.
- Push steps are skipped unless `--push` is provided.
- `Artifacts` and `PushDecisions` are typically empty.

`rx release --push --json-file artifacts/runs/release.json`

- Push phase runs.
- `Artifacts` and `PushDecisions` are populated.
- If policy blocks push, entries are present with deny reasons and `Pushed=false`.
