# Configuration Reference

Default repository configuration file: **`rexo.json`**

Supported config locations (first match wins):

- `rexo.json`, `rexo.yaml`, `rexo.yml` (repo root)
- `.rexo/rexo.json`, `.rexo/rexo.yaml`, `.rexo/rexo.yml`
- Backward-compatible fallback: `repo.json|yaml|yml` in root or `.repo/`

---

## Schema Contract (required)

Every config file (`rexo.json`/`rexo.yml`) must begin with:

```json
{
  "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema/v1.0/rexo.schema.json",
  "schemaVersion": "1.0",
  ...
}
```

- `$schema`: the canonical URL `https://raw.githubusercontent.com/agile-north/rexo/schema/v1.0/rexo.schema.json` (recommended), or the relative `rexo.schema.json` / `../rexo.schema.json` for local-only use
- `schemaVersion`: must be `"1.0"`

The loader validates against the embedded schema (or a local `rexo.schema.json`) via NJsonSchema before
deserializing. Missing/unsupported metadata or schema violations cause a hard failure.

Policy files (`policy.json`/`policy.yml`) follow the same contract, using:

- `$schema`: `https://raw.githubusercontent.com/agile-north/rexo/schema/v1.0/policy.schema.json` (recommended), or `policy.schema.json` / `../policy.schema.json`
- `schemaVersion`: must be `"1.0"`

When `rx init --schema-source local --with-policy` is used, both schema files are written to `.rexo/`:

- `.rexo/rexo.schema.json`
- `.rexo/policy.schema.json`

---

## Top-level Structure

```jsonc
{
  "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema/v1.0/rexo.schema.json",
  "schemaVersion": "1.0",
  "name": "my-repo",
  "description": "Optional description",

  // Inherit from one or more base configs (local paths, resolved relative to this file)
  "extends": ["./base/rexo.json"],

  "commands": { ... },
  "aliases": { ... },
  "versioning": { ... },
  "artifacts": [ ... ],
  "tests": { ... },
  "analysis": { ... },
  "pushRulesJson": "{ ... }"
}
```

---

## `extends` — Config Merge Pipeline

`extends` accepts an array of local file paths. Configs are merged breadth-first;
child properties win over base properties. Commands and aliases are merged (child
additions take priority). Circular references are detected and rejected.

```json
{ "extends": ["../../shared/rexo.json"] }
```

---

## `commands`

Each key is a command name (spaces allowed for multi-word commands).

```jsonc
"commands": {
  "build": {
    "description": "Build the project",
    "options": {
      "configuration": { "type": "string", "default": "Release" }
    },
    "args": {
      "target": { "required": false, "description": "Build target" }
    },
    "steps": [ ... ]
  },
  "branch feature": {          // invoked as: rx branch feature <name>
    "steps": [ ... ]
  }
}
```

### Steps

Each step has one of `run`, `uses`, or `command`:

```jsonc
{
  "id": "my-step",             // optional; enables output referencing
  "run": "echo {{args.name}}", // shell command (template-expanded)
  "when": "{{options.flag}}",  // skip step if value is falsey after rendering
  "continueOnError": true,     // don't fail the command if this step fails
  "parallel": true,            // run concurrently with adjacent parallel steps
  "outputPattern": "v(?P<version>[\\d.]+)", // regex: named groups → step outputs
  "outputFile": "build/version.txt"         // write stdout to this file path
}
```

Command-level concurrency is controlled with `maxParallel`:

```jsonc
"commands": {
  "build": {
    "maxParallel": 4,
    "steps": [ ... ]
  }
}
```

```jsonc
{
  "uses": "builtin:resolve-version"  // built-in primitive
}
```

```jsonc
{
  "command": "build"                 // delegate to another configured command
}
```

#### Parallel execution

Consecutive steps marked `parallel: true` are batched and run concurrently via
`Task.WhenAll`. Each parallel step receives a snapshot of the context at the start of
the group (they cannot see each other's outputs within the same group).

#### Output capture

- **`outputPattern`**: a .NET regex with named groups. Matched groups are stored in
  `steps.<id>.output.<groupName>` and available to subsequent template steps.
- **`outputFile`**: stdout is written to this path (relative to the repo root).

---

## `versioning`

```jsonc
"versioning": {
  "provider": "gitversion",  // fixed | env | gitversion | minver | nbgv
  "settings": {
    "variable": "MY_VERSION_VAR",     // for env provider
    "fallback": "0.1.0",
    "tagPrefix": "v",                 // for minver provider
    "minimumMajorMinor": "1.0"        // for minver provider
  }
}
```

| Provider | Tool | Notes |
| --- | --- | --- |
| `fixed` | — | Returns `settings.version` |
| `env` | — | Reads `settings.variable` env var; falls back to `settings.fallback` |
| `gitversion` | `gitversion /output json` | Parses SemVer2 fields from JSON output |
| `minver` | `dotnet minver` | Single-line SemVer output; supports `tagPrefix`, `minimumMajorMinor` |
| `nbgv` | `nbgv get-version -f json` | Parses `SemVer2`, `Version`, `GitCommitId` from JSON |

---

## `artifacts`

```jsonc
"artifacts": [
  {
    "type": "docker",
    "name": "my-image",
    "settings": {
      "dockerfile": "Dockerfile",
      "context": ".",
      "registry": "ghcr.io/org"
    }
  },
  {
    "type": "nuget",
    "name": "MyPackage",
    "settings": {
      "project": "src/MyLib/MyLib.csproj",
      "source": "https://api.nuget.org/v3/index.json"
    }
  }
]
```

After `builtin:push-artifacts` completes, a manifest is written to
`artifacts/manifest.json` listing each artifact's type, name, push status, and
published references.

### Docker artifact settings (`type: "docker"`)

The schema now validates Docker settings explicitly. Supported keys:

| Key | Type | Purpose |
| --- | --- | --- |
| `image` | `string` | Fully-qualified image name (e.g. `ghcr.io/org/app`) |
| `dockerfile` / `file` | `string` | Dockerfile path |
| `context` | `string` | Build context path |
| `runner` | `build \| buildx \| auto` | Docker build runner selection |
| `platform` | `string` | Build platform (e.g. `linux/amd64`) |
| `buildTarget` | `string` | Build stage target |
| `buildOutput` | `string \| string[]` | Build output flags |
| `buildArgs` | `string \| object \| array` | Build args (`KEY=VALUE`) |
| `secrets` | `object` | BuildKit secrets map (`env` or `file`) |
| `registry` / `repository` | `string` | Image target composition fallback |
| `target.registry` / `target.repository` | `string` | Nested target settings |
| `loginRegistry` / `login.registry` | `string` | Docker login registry override |
| `cleanupLocal` / `cleanup.local` | `bool \| auto` | Local image cleanup mode |
| `pushEnabled` / `push.enabled` | `bool` | Provider push enable/disable |
| `pushBranches` / `push.branches` | `string \| string[]` | Branch eligibility patterns |
| `push.branchesShortcut` | `string` | Delimited branch shortcut |
| `denyNonPublicPush` / `push.denyNonPublicPush` | `bool` | Block non-public branch push |
| `latest` / `tags.latest` | `bool` | Add `latest` tag |
| `tags` | `string \| string[]` | Explicit tag strategy kinds |
| `publicBuild` / `build.public` | `bool` | Explicit classification override |
| `publicBranches*`, `nonPublicBranches*` | `string \| string[]` | Branch classification rules |
| `classification.*` | `object` | Nested branch classification settings |
| `tagPolicy.public` / `tagPolicy.nonPublic` | `string \| string[]` | Tag strategy policy by classification |
| `nonPublicMode` | `string` | Non-public behavior mode (e.g. `full-only`) |
| `aliases.*` | `object` | Branch alias generation settings |
| `aliases.rules[]` | `array` | Match/template alias rules |
| `stages` | `object` | Named stage definitions |
| `stageFallback` | `bool` | Fallback behavior for stage requests |

`secrets` shape:

```jsonc
"secrets": {
  "npm_token": { "env": "NPM_TOKEN" },
  "cert": { "file": "./cert.pem" }
}
```

Each secret must include exactly one of `env` or `file`.

`stages` shape:

```jsonc
"stages": {
  "publish": {
    "target": "publish",
    "output": ["type=local,dest=./publish"],
    "runner": "buildx",
    "platform": "linux/amd64"
  }
}
```

### NuGet artifact settings (`type: "nuget"`)

Supported keys:

| Key | Type | Purpose |
| --- | --- | --- |
| `project` | `string` | Project path for `dotnet pack` |
| `output` | `string` | Output directory |
| `source` | `string` | NuGet feed URL |
| `apiKeyEnv` | `string` | Environment variable containing API key |

### Artifact workflow procedures

General multi-artifact procedure (all artifact types):

1. `builtin:plan` (or `builtin:plan-artifacts`) to inspect planned artifacts and key build settings.
2. `builtin:build-artifacts` to build all configured artifacts.
3. `builtin:tag-artifacts` to apply artifact tags/versions.
4. `builtin:push-artifacts` to publish artifacts (with global and per-artifact push policy enforcement).

Shortcut procedures:

- `builtin:ship` (or `builtin:ship-artifacts`) runs `tag + push` across all configured artifacts.
- `builtin:all` (or `builtin:all-artifacts`) runs `build + tag + push` across all configured artifacts.

Example command wiring:

```jsonc
"commands": {
  "plan": { "steps": [{ "id": "plan", "uses": "builtin:plan" }] },
  "ship": { "steps": [{ "id": "ship", "uses": "builtin:ship" }] },
  "all": { "steps": [{ "id": "all", "uses": "builtin:all" }] }
}
```

Docker-specific procedure:

1. `builtin:docker-plan` to inspect only Docker artifacts.
2. `builtin:docker-ship` to tag and push only Docker artifacts.
3. `builtin:docker-all` to build, tag, and push only Docker artifacts.
4. `builtin:docker-stage` to build one named stage from `settings.stages`.

Example stage invocation patterns:

```jsonc
"commands": {
  "docker stage": {
    "args": {
      "stage": { "required": true, "description": "Stage name from artifacts[].settings.stages" }
    },
    "steps": [{ "id": "docker-stage", "uses": "builtin:docker-stage" }]
  }
}
```

The stage value can be provided via `args.stage` or `--stage`.

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

---

## `pushRulesJson`

A JSON string encoding push policy rules enforced by `builtin:push-artifacts`:

```json
"pushRulesJson": "{\"noPushInPullRequest\": true, \"requireCleanWorkingTree\": true}"
```

| Rule | Effect |
| --- | --- |
| `noPushInPullRequest` | Rejects push when the CI environment reports a PR build |
| `requireCleanWorkingTree` | Rejects push when the git working tree has uncommitted changes |

---

## Template Variables

Available in any `run` step string:

| Path | Source |
| --- | --- |
| `{{args.<name>}}` | Positional/named args from CLI |
| `{{options.<name>}}` | Option flags from CLI |
| `{{env.<VAR>}}` | Environment variables |
| `{{repo.<field>}}` | Top-level config fields (name, version, …) |
| `{{version.<field>}}` | Resolved version after `builtin:resolve-version` |
| `{{steps.<id>.output.<key>}}` | Output from a completed step |

**Filters**: `{{value \| slug}}`, `{{value \| upper}}`, `{{value \| lower}}`,
`{{value \| default(fallback)}}`

---

## Built-in Primitives

Use as `uses: builtin:<name>` in a step:

| Primitive | Purpose |
| --- | --- |
| `builtin:validate` | Validate the loaded config |
| `builtin:resolve-version` | Run the version provider; populate `context.Version` |
| `builtin:test` | Run `dotnet test`; enforce coverage threshold if configured |
| `builtin:analyze` | Run `dotnet format --verify-no-changes` |
| `builtin:verify` | Run test + analyze |
| `builtin:build-artifacts` | Build all configured artifacts |
| `builtin:tag-artifacts` | Tag all artifacts |
| `builtin:push-artifacts` | Push artifacts; enforce push rules; write `artifacts/manifest.json` |
| `builtin:plan-artifacts` | Plan all configured artifacts and emit plan JSON |
| `builtin:ship-artifacts` | Tag + push all configured artifacts |
| `builtin:all-artifacts` | Build + tag + push all configured artifacts |
| `builtin:plan` | Alias of `builtin:plan-artifacts` |
| `builtin:ship` | Alias of `builtin:ship-artifacts` |
| `builtin:all` | Alias of `builtin:all-artifacts` |
| `builtin:docker-plan` | Plan Docker artifacts only |
| `builtin:docker-ship` | Tag + push Docker artifacts only |
| `builtin:docker-all` | Build + tag + push Docker artifacts only |
| `builtin:docker-stage` | Build a named Docker stage (`args.stage` or `--stage`) |
| `builtin:config-resolved` | Print the effective merged config as JSON |
| `builtin:config-materialize` | Write provider config files (e.g. `GitVersion.yml`) if absent |

---

## Built-in CLI Commands

| Command | Purpose |
| --- | --- |
| `rx version` | Print tool version |
| `rx list` | List all available commands |
| `rx explain <cmd>` | Show command description, args, options, steps |
| `rx doctor` | Check tool and provider availability |
| `rx init` | Create a starter `rexo.json` (defaults to `.rexo/rexo.json`) |
| `rx config resolved` | Print the effective merged config (`rexo.json`) as JSON |
| `rx config sources` | Show config file path and load status |

`rx init` also supports interactive policy setup and these non-interactive flags:

- `--template auto|dotnet|node|generic`
- `--schema-source local|remote`
- `--with-policy`
- `--policy-template <name>` (e.g. `standard`, `dotnet`)
- `--yes` and `--force`

`rx init` always scaffolds under `.rexo/`. Root config files are still supported when authored manually.

### Config Inspection Reference

Use these commands to inspect exactly what Rexo loaded and what it will execute:

| Command | What you get |
| --- | --- |
| `rx config sources` | Source file path(s) and load status for config resolution |
| `rx config resolved` | Final merged config JSON after `extends`, policy, and overlays |
| `rx run config materialize` | Writes provider files like `GitVersion.yml` when missing |

Examples:

```bash
# Show where configuration came from
rx config sources

# Inspect final merged configuration
rx config resolved --json

# Write provider files that are required by configured tooling
rx run config materialize
```

## Global Flags

| Flag | Effect |
| --- | --- |
| `--json` | Output result as JSON to stdout |
| `--json-file <path>` | Write JSON result to file |
| `--verbose` | Print step output and extra detail |
| `--debug` | Enable debug output (implies `--verbose`) |
| `--quiet` / `-q` | Suppress all non-error console output |

For full functional requirements see [scope.md](scope.md).

---

## Version Provider Output Contract

Every version provider returns a `VersionResult` with the following fields:

**Required fields:**

| Field | Type | Description |
| --- | --- | --- |
| `semver` | `string` | Full SemVer 2.0 string (e.g. `1.2.3-beta.1`) |
| `major` | `int` | Major version number |
| `minor` | `int` | Minor version number |
| `patch` | `int` | Patch version number |
| `preRelease` | `string?` | Pre-release identifier (e.g. `beta.1`) |
| `buildMetadata` | `string?` | Build metadata (the part after `+` in SemVer 2.0) |
| `branch` | `string?` | Repository branch at resolution time |
| `commitSha` | `string` | Full commit SHA |
| `shortSha` | `string` | Abbreviated commit SHA |
| `isPreRelease` | `bool` | True if `preRelease` is non-empty |
| `isStable` | `bool` | True if `isPreRelease` is false |

**Optional / derived fields:**

| Field | Type | Description |
| --- | --- | --- |
| `assemblyVersion` | `string` | `Major.Minor.Patch.0` format |
| `fileVersion` | `string` | `Major.Minor.Patch.0` format |
| `informationalVersion` | `string` | `semver+buildMetadata` (or just `semver`) |
| `nugetVersion` | `string?` | NuGet-compatible version (dots instead of hyphens) |
| `dockerVersion` | `string?` | Docker tag–safe version (alphanumeric, dots, hyphens) |
| `commitsSinceVersionSource` | `int?` | Commits since the version source tag |
| `weightedPreReleaseNumber` | `int?` | Sort weight: alpha=1, beta=2, rc=3, preview=4, other=0 |

These fields are all available in templates as `{{version.<field>}}` after `builtin:resolve-version` runs.

---

## Known Limitations and Unresolved Features

The following features are defined in the product scope but not yet implemented:

### Policy Sources

- Only **local file** policy sources are supported (`policy.json` alongside `rexo.json`, or files in `.rexo/`; `.repo/` is still accepted for backward compatibility).
- **Remote policy sources** (HTTP, Git, NuGet package) are not yet implemented.
- **Policy caching, version pinning, and trust models** are not yet implemented.

### Parallel Step Execution

- Basic parallel step execution works via the `parallel: true` flag on steps, with a `maxParallel` concurrency cap.
- Dependency-aware fan-in is supported via `dependsOn` (step IDs) within parallel groups.

### Run Manifest

- The run manifest (written via `--json-file`) includes steps, version, CI context, git context, and errors.
- **Push decisions and artifact entries** are propagated from `builtin:push-artifacts` into command results and JSON output.
- The artifact manifest file `artifacts/manifest.json` is still written separately by `builtin:push-artifacts` for file-based consumption.

### Config Inspection Commands

- `rx config resolved` and `rx config sources` are registered but display basic output only.
- `rx config materialize` writes provider config files (e.g. `GitVersion.yml`) but does not yet have a rich interactive UI.

### UI and Interactive Features

- No TUI (terminal user interface) project picker or interactive workflow is implemented yet.
- The rich command picker (`rx` with no arguments) shows a list but does not support keyboard navigation.
