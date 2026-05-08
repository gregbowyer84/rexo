# Runtime Configuration

Configure runtime output, push policies, tests, and analysis behavior.

---

## `runtime.output`

Controls filesystem artifact emission and the root output folder.

```jsonc
"runtime": {
  "output": {
    "emitRuntimeFiles": true,
    "root": "artifacts"
  }
}
```

- `emitRuntimeFiles` (default: `true`): when `false`, runtime-generated files (for example artifact manifest files) are not written.
- `root` (default: `artifacts`): root folder used by runtime artifact outputs.

Notes:

- `builtin:clean` removes this folder.
- Embedded test command overlays typically write results under `<runtime.output.root>/tests` when not explicitly set.
- NuGet artifacts default `settings.output` to `<runtime.output.root>/packages` when not explicitly set.

---

## `runtime.push`

Push policy and eligibility rules enforced by `builtin:push-artifacts`.

```jsonc
"runtime": {
  "push": {
    "enabled": true,
    "noPushInPullRequest": true,
    "requireCleanWorkingTree": true,
    "branches": ["main", "release/*"]
  }
}
```

`builtin:push-artifacts` enforces these rules globally, then merges per-artifact
overrides from `artifacts[].settings.push.*`.

| Rule | Effect |
| --- | --- |
| `enabled` | Enables/disables push globally |
| `noPushInPullRequest` | Rejects push when the CI environment reports a PR build |
| `requireCleanWorkingTree` | Rejects push when the git working tree has uncommitted changes |
| `branches` | Allows push only when branch matches listed patterns |

---

## `tests`

```jsonc
"tests": {
  "enabled": true,
  "projects": ["tests/**/*.Tests.csproj"],
  "configuration": "Release",
  "resultsOutput": "artifacts/tests",
  "coverageOutput": "artifacts/coverage",
  "coverageThreshold": 80         // fail if line coverage < 80%
}
```

If `resultsOutput` is omitted, the default path is `<runtime.output.root>/tests`
(or `artifacts/tests` when `runtime.output.root` is omitted).

Coverage is parsed from Cobertura XML written by `XPlat Code Coverage`.

---

## `analysis`

```jsonc
"analysis": {
  "enabled": true,
  "configuration": "Release"
}
```

Runs `dotnet format --verify-no-changes` and a build-only pass.

SARIF behavior:

- If `analysis.configuration` is provided and ends with `.sarif` or `.sarif.json`, SARIF is written there.
- If `analysis.configuration` is omitted, SARIF defaults to `<runtime.output.root>/analysis.sarif.json`
  (fallback root: `artifacts`, so default file is `artifacts/analysis.sarif.json`).
