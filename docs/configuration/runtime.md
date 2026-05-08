# Runtime Configuration

Configure runtime output and push policies.

---

## `runtime.output`

Controls filesystem artifact emission, the root output folder, and per-category output paths.

```jsonc
"runtime": {
  "output": {
    "emitRuntimeFiles": true,
    "root": "artifacts",
    "tests": {
      "results": "artifacts/tests",
      "coverage": "artifacts/coverage",
      "reports": "artifacts/coverage/reports"
    },
    "analysis": {
      "reports": "artifacts/analysis",
      "sarif": "artifacts/analysis/sarif"
    },
    "packages": "artifacts/packages",
    "manifests": "artifacts/manifests",
    "logs": "artifacts/logs"
  }
}
```

- `emitRuntimeFiles` (default: `true`): when `false`, runtime-generated files (for example artifact manifest files) are not written.
- `root` (default: `artifacts`): root folder used by runtime artifact outputs.
- `tests` — overrides where test results, coverage data, and coverage reports are written. The policy overlay (e.g. `embedded:dotnet`) reads these paths when constructing test commands.
- `analysis` — overrides where analysis reports and SARIF files are written. The policy overlay reads these paths when constructing analysis commands.
- `packages` (default: `artifacts/packages`): NuGet and other package output directory.
- `manifests` (default: `artifacts/manifests`): run manifest output directory.
- `logs` (default: `artifacts/logs`): log output directory.

> **Note**: test and analysis *execution* (toolchain, arguments, triggers) is not configured here. It is provided by the active policy overlay — see [Embedded policies](../EMBEDDED.md).

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
