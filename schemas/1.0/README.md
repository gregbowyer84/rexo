# Rexo Schema Version 1.0

**Schema file:** `rexo.schema.json` (repo root)
**Schema ID:** `https://raw.githubusercontent.com/agile-north/rexo/schema/v1.0/rexo.schema.json`
**JSON Schema draft:** 2020-12

---

## Stability Contract

Schema version 1.0 is the initial stable version of the Rexo configuration contract.

- **Backwards compatibility**: New optional fields may be added without incrementing the version.
- **Breaking changes**: Removing required fields, renaming fields, or changing field types will produce a new schema version (e.g. `2.0`).
- **`schemaVersion`**: Must be the string `"1.0"`. The loader rejects configs with an unsupported version.

---

## Required Fields

Every `repo.json` must declare:

```json
{
  "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema/v1.0/rexo.schema.json",
  "schemaVersion": "1.0",
  "name": "my-repo",
  "commands": {},
  "aliases": {}
}
```

| Field | Type | Description |
| --- | --- | --- |
| `$schema` | `string` | Remote URL or `rexo.schema.json` / `./rexo.schema.json` / `../rexo.schema.json` |
| `schemaVersion` | `string` | Must be `"1.0"` |
| `name` | `string` (min 1 char) | Repository name |
| `commands` | `object` | Map of command name → command config (may be empty) |
| `aliases` | `object` | Map of alias name → target command name (may be empty) |

---

## Optional Top-Level Fields

| Field | Type | Description |
| --- | --- | --- |
| `description` | `string` | Human-readable description of this repository |
| `version` | `string` | Static version string (overrides version provider when set) |
| `extends` | `string[]` | Paths to base config files to merge (breadth-first, circular detection) |
| `versioning` | `object` | Version provider configuration |
| `artifacts` | `array` | Artifact definitions (docker, nuget) |
| `tests` | `object` | Test configuration |
| `analysis` | `object` | Analysis configuration |
| `pushRulesJson` | `string` | JSON-encoded push policy rules |

---

## Command Config

```json
"commands": {
  "build": {
    "description": "Build the project",
    "args": [],
    "options": [],
    "steps": [
      { "run": "dotnet build" }
    ]
  }
}
```

### Step Types

| Field | Type | Description |
| --- | --- | --- |
| `id` | `string?` | Optional step ID (used in template `{{steps.<id>.output.<key>}}`) |
| `run` | `string?` | Shell command to execute (mutually exclusive with `uses`/`command`) |
| `uses` | `string?` | Built-in primitive name (e.g. `builtin:test`) |
| `command` | `string?` | Invoke another named command |
| `when` | `string?` | Condition — step is skipped if this template expression renders falsy |
| `parallel` | `bool` | Run this step in parallel with adjacent parallel steps |
| `continueOnError` | `bool` | Continue even if this step fails |
| `outputPattern` | `string?` | Regex with named groups to extract from stdout |
| `outputFile` | `string?` | Write stdout to this file path |

### Falsy Values for `when`

The following values are treated as falsy (step skipped): `false`, `0`, `no`, empty string.
All other non-empty values are truthy.

---

## Versioning Config

```json
"versioning": {
  "provider": "gitversion",
  "fallback": "0.1.0"
}
```

| Provider | Key | Notes |
| --- | --- | --- |
| Fixed | `fixed` | Returns a static version from `version` field |
| Environment | `env` | Reads an env var; falls back to `fallback` |
| GitVersion | `gitversion` | Runs `gitversion /output json` |
| MinVer | `minver` | Runs `dotnet minver` |
| NBGV | `nbgv` | Runs `nbgv get-version -f json` |
| Git | `git` | Parses most recent tag as SemVer |

---

## Artifacts Config

```json
"artifacts": [
  {
    "type": "docker",
    "name": "my-app",
    "settings": {
      "dockerfile": "Dockerfile",
      "context": ".",
      "registry": "ghcr.io/org/my-app"
    }
  },
  {
    "type": "nuget",
    "name": "MyLib",
    "settings": {
      "project": "src/MyLib/MyLib.csproj",
      "source": "https://api.nuget.org/v3/index.json"
    }
  }
]
```

---

## Tests Config

```json
"tests": {
  "enabled": true,
  "projects": ["tests/**/*.Tests.csproj"],
  "configuration": "Release",
  "lineCoverageThreshold": 80
}
```

---

## Analysis Config

```json
"analysis": {
  "enabled": true,
  "configuration": "Release"
}
```

---

## Push Rules

`pushRulesJson` is a JSON string (not an object) containing push policy rules:

```json
"pushRulesJson": "{\"noPushInPullRequest\": true, \"requireCleanWorkingTree\": true}"
```

| Rule | Effect |
| --- | --- |
| `noPushInPullRequest` | Deny push when CI detects a pull request context |
| `requireCleanWorkingTree` | Deny push when the git working tree is not clean |

---

## Changelog

| Version | Date | Changes |
| --- | --- | --- |
| `1.0` | Initial | Initial stable release |



