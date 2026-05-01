# Configuration Reference

Default repository configuration file: **`repo.json`**

---

## Schema Contract (required)

Every `repo.json` must begin with:

```json
{
  "$schema": "schemas/1.0/schema.json",
  "schemaVersion": "1.0",
  ...
}
```

- `$schema`: must be `schemas/1.0/schema.json` or `./schemas/1.0/schema.json`
- `schemaVersion`: must be `"1.0"`

The loader validates against `schemas/1.0/schema.json` via NJsonSchema before
deserializing. Missing/unsupported metadata or schema violations cause a hard failure.

---

## Top-level Structure

```jsonc
{
  "$schema": "schemas/1.0/schema.json",
  "schemaVersion": "1.0",
  "name": "my-repo",
  "description": "Optional description",

  // Inherit from one or more base configs (local paths, resolved relative to this file)
  "extends": ["./base/repo.json"],

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
{ "extends": ["../../shared/repo.json"] }
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
| `rx config resolved` | Print the effective merged `repo.json` as JSON |
| `rx config sources` | Show config file path and load status |

## Global Flags

| Flag | Effect |
| --- | --- |
| `--json` | Output result as JSON to stdout |
| `--json-file <path>` | Write JSON result to file |
| `--verbose` | Print step output and extra detail |
| `--debug` | Enable debug output (implies `--verbose`) |
| `--quiet` / `-q` | Suppress all non-error console output |

For full functional requirements see [scope.md](scope.md).
