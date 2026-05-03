# Configuration Reference

For embedded templates, built-in lifecycle defaults, options, and usage examples,
see [Embedded Items Reference](EMBEDDED.md).

For runtime builtin contracts (inputs, calls, outputs, exit behavior),
see [Builtins Reference](BUILTINS.md).

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
  "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/rexo.schema.json",
  "schemaVersion": "1.0",
  ...
}
```

- `$schema`: the canonical URL `https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/rexo.schema.json` (recommended), or the relative `rexo.schema.json` / `../rexo.schema.json` for local-only use
- `schemaVersion`: must be `"1.0"`

The loader validates against the embedded schema (or a local `rexo.schema.json`) via NJsonSchema before
deserializing. Missing/unsupported metadata or schema violations cause a hard failure.

Policy files (`policy.json`/`policy.yml`) follow the same contract, using:

- `$schema`: `https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/policy.schema.json` (recommended), or `policy.schema.json` / `../policy.schema.json`
- `schemaVersion`: must be `"1.0"`

When `rx init --schema-source local --with-policy` is used, both schema files are written to `.rexo/`:

- `.rexo/rexo.schema.json`
- `.rexo/policy.schema.json`

---

## Top-level Structure

```jsonc
{
  "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/rexo.schema.json",
  "schemaVersion": "1.0",
  "name": "my-repo",
  "description": "Optional description",

  // Inherit from one or more base configs (local paths, resolved relative to this file)
  "extends": ["./base/rexo.json"],

  "commands": { ... },
  "aliases": { ... },
  "versioning": { ... },
  "artifacts": [ ... ],
  "runtime": { ... },
  "tests": { ... },
  "analysis": { ... }
}
```

## Fully Emitted Effective Defaults

When optional fields are omitted, runtime behavior applies defaults. The example below
shows the effective values used by built-ins (not a requirement to persist every field in your file).

```jsonc
{
  "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/rexo.schema.json",
  "schemaVersion": "1.0",
  "name": "my-repo",

  "commands": {},
  "aliases": {},
  "artifacts": [],

  "versioning": {
    "provider": "auto",
    "fallback": "0.1.0-local",
    "settings": {}
  },

  "runtime": {
    "output": {
      "emitRuntimeFiles": true,
      "root": "artifacts"
    },
    "push": {
      "enabled": true,
      "noPushInPullRequest": false,
      "requireCleanWorkingTree": false,
      "branches": []
    }
  },

  "tests": {
    "enabled": true,
    "projects": null,
    "configuration": "Release",
    "resultsOutput": "<runtime.output.root>/tests",
    "coverageOutput": null,
    "collectCoverage": null,
    "coverageThreshold": null
  },

  "analysis": {
    "enabled": true,
    "failOnIssues": true,
    "tools": [],
    "configuration": "<runtime.output.root>/analysis.sarif.json"
  }
}
```

Notes:

- `versioning` defaults are used by `builtin:resolve-version` when `versioning` is omitted.
- `tests.resultsOutput` and `analysis.configuration` are computed from `runtime.output.root` when omitted.
- `collectCoverage` only becomes active when coverage output collection is configured.
- `commands`, `aliases`, and `artifacts` are shown as empty here for completeness; they are optional in config files.

---

## `extends` — Config Merge Pipeline

`extends` accepts an array containing either local file paths or embedded policy
template references.

Supported entry types:

- Local path: `./base/rexo.json`
- Embedded template: `embedded:standard`, `embedded:dotnet`, `embedded:none`, …

Merge behavior:

- Circular references are detected and rejected.
- Configs are merged breadth-first.
- Child properties win over base properties.
- Commands and aliases are merged (child additions take priority).

### Minimal-by-default lifecycle

Rexo now uses an explicit lifecycle model. If you do not set `extends`, no embedded policy
commands are added automatically. An artifacts-only config remains minimal until you opt in
to a policy template.

This is intentional so Rexo can be used as a lightweight command/alias runtime without becoming a build/release tool unless a policy is selected.

To keep a config explicitly minimal, include `embedded:none` in `extends`:

```json
{ "extends": ["embedded:none"] }
```

`embedded:none` is an empty policy that carries no commands or aliases. It is useful for
making minimal intent explicit in shared templates.

### Policy template stacking

When a project-specific embedded policy is selected (e.g. `dotnet-api`) it should be
stacked *on top of* `embedded:standard` so both the shared lifecycle commands (`build`,
`test`, `verify`, `release`) and the project-specific commands (`ci`, `restore`,
`format`, `stage`) are available together:

```json
{ "extends": ["embedded:standard", "embedded:dotnet-api"] }
```

This is what `rx init` generates automatically when `--with-policy` is used with a
recognized policy template that is not `standard`.

### Command naming convention

Lifecycle commands provided by policy templates (`build`, `test`, `verify`, `release`,
etc.) are designed to be the primary entry points. Project-specific developer convenience
commands should use a `local` prefix to avoid name collisions:

- **Policy lifecycle**: `build`, `test`, `verify`, `release` (from `embedded:standard`)
- **Project-specific**: `local build`, `local test` (scaffolded by `rx init`)

This allows both sets of commands to coexist cleanly. `rx local build` runs your
project's custom build step; `rx build` runs the full policy-driven lifecycle.

Examples:

```json
{ "extends": ["embedded:standard"] }
```

```json
{ "extends": ["embedded:standard", "embedded:dotnet-api"] }
```

```json
{ "extends": ["embedded:none"] }
```

```json
{ "extends": ["embedded:dotnet", "../../shared/rexo.json"] }
```

```json
{
  "name": "orders-api",
  "artifacts": [
    {
      "type": "docker",
      "name": "api",
      "settings": { "image": "ghcr.io/acme/orders-api" }
    }
  ]
}
```

The last example remains minimal until you explicitly add an `extends` entry.

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

### Command option typing (strongly defined)

For `commands.<name>.options.<option>.type`, allowed values are:

- `string`
- `bool`
- `boolean`
- `int`
- `integer`
- `number`

Schema default:

- `type` defaults to `string` when omitted

`default` may be a string, boolean, integer, or number value.

### Steps

Each step has one of `run`, `uses`, or `command`:

```jsonc
{
  "id": "my-step",             // optional; enables output referencing
  "run": "echo {{args.name}}", // shell command (template-expanded)
  "when": "{{options.flag}}",  // skip step if value is falsey after rendering
  "with": {                      // optional; per-step option overrides
    "push": "{{options.push}}"
  },
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

`with` is most useful when invoking reusable built-ins. It lets a command map its
own option names into step-local option names consumed by that builtin.

Resolution precedence for values consumed by built-ins is:

1. `step.with`
2. command options/args
3. execution context defaults
4. provider-specific defaults

Example:

```json
{
  "id": "push",
  "uses": "builtin:push-artifacts",
  "with": {
    "confirm": "{{options.push}}"
  }
}
```

This makes intent explicit without forcing the builtin to understand every command-specific
option name.

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
  "provider": "auto",   // auto | fixed | env | gitversion | minver | nbgv | git
  "fallback": "0.1.0-local",
  "settings": {
    "variable": "MY_VERSION_VAR",     // for env provider
    "tagPrefix": "v",                 // for minver provider
    "minimumMajorMinor": "1.0",       // for minver provider
    "useDocker": "true",              // gitversion/nbgv/minver — enable Docker fallback (default true)
    "dockerImage": "gittools/gitversion:6.0.0"  // override the Docker image for this provider
  }
}
```

| Provider | Tool | Notes |
| --- | --- | --- |
| `auto` | — | **Default.** Detects provider by config file evidence (see below). |
| `fixed` | — | Returns the configured `fallback` version string |
| `env` | — | Reads `settings.variable` env var; falls back to `fallback` |
| `git` | `git describe` | Resolves from the most recent SemVer git tag |
| `gitversion` | `gitversion /output json` | Parses SemVer2 fields from JSON output; Docker fallback via `gittools/gitversion:6.0.0` |
| `minver` | `dotnet minver` | Single-line SemVer output; Docker fallback via .NET SDK image |
| `nbgv` | `nbgv get-version -f json` | Parses `SemVer2`, `Version`, `GitCommitId`; Docker fallback via .NET SDK image |

### `auto` detection order

When `provider` is `auto`, Rexo scans the repository root for versioning config file evidence in this order:

1. `version.json` or `nbgv.json` present → uses **nbgv**
2. `GitVersion.yml` or `GitVersion.yaml` present → uses **gitversion**
3. `.minverrc` present → uses **minver**
4. `.git` directory present → uses **git** (tag-based)
5. None of the above → uses **fixed** with the configured `fallback` version

### Docker fallback

`gitversion`, `nbgv`, and `minver` all support a Docker fallback for environments where
the CLI tool is not installed. The fallback is **enabled by default** and is tried after
the host tool fails (tool not found, non-zero exit, or empty output).

| Provider | Default image | Container command |
| --- | --- | --- |
| `gitversion` | `gittools/gitversion:6.0.0` | `docker run --rm -v <repo>:/repo -w /repo <image> /output json` |
| `nbgv` | `mcr.microsoft.com/dotnet/sdk:latest` | `dotnet tool restore && dotnet nbgv get-version --format json` |
| `minver` | `mcr.microsoft.com/dotnet/sdk:latest` | `dotnet tool restore && dotnet minver [args]` |

For `nbgv` and `minver` Docker, the tool must be present in the repo's `.config/dotnet-tools.json` manifest so `dotnet tool restore` can install it inside the container.

Disable Docker for a specific provider with:

```jsonc
{ "versioning": { "provider": "nbgv", "settings": { "useDocker": "false" } } }
```

Override the Docker image:

```jsonc
{ "versioning": { "provider": "gitversion", "settings": { "dockerImage": "gittools/gitversion:5.12.0" } } }
```

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
  },
  {
    "type": "helm-oci",
    "name": "my-chart",
    "settings": {
      "chartPath": "deploy/charts/my-chart",
      "registry": "ghcr.io",
      "repository": "org/charts"
    }
  }
]
```

Artifact name fallback:

- `artifacts[].name` is optional.
- When omitted, Rexo uses the root `name` value from the config as the artifact name.
- This fallback applies consistently to plan/build/tag/push output and manifests.

After `builtin:push-artifacts` completes, a manifest is written to
`<runtime.output.root>/manifest.json` (default: `artifacts/manifest.json`) listing each
artifact's type, name, push status, and published references.

### Artifact provider references

Use provider-specific documentation for settings, auth, fallback, and examples:

| Type | Provider doc |
| --- | --- |
| `docker` | [artifacts/docker.md](artifacts/docker.md) |
| `docker-compose` | [artifacts/docker-compose.md](artifacts/docker-compose.md) |
| `nuget` | [artifacts/nuget.md](artifacts/nuget.md) |
| `helm-oci` | [artifacts/helm-oci.md](artifacts/helm-oci.md) |
| `helm` | [artifacts/helm.md](artifacts/helm.md) |
| `npm` | [artifacts/npm.md](artifacts/npm.md) |
| `pypi` | [artifacts/pypi.md](artifacts/pypi.md) |
| `maven` | [artifacts/maven.md](artifacts/maven.md) |
| `gradle` | [artifacts/gradle.md](artifacts/gradle.md) |
| `rubygems` | [artifacts/rubygems.md](artifacts/rubygems.md) |
| `terraform` | [artifacts/terraform.md](artifacts/terraform.md) |
| custom provider (`generic`) | [artifacts/generic.md](artifacts/generic.md) |

Index page: [artifacts/README.md](artifacts/README.md)

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
- `builtin:test` defaults `resultsOutput` to `<runtime.output.root>/tests` when not explicitly set.
- NuGet artifacts default `settings.output` to `<runtime.output.root>/packages` when not explicitly set.

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
| `tags` | `tagKind \| tagKind[]` | Explicit tag strategy kinds (`full`, `semver`, `majorMinor`, `majorminor`, `major-minor`, `major`, `branch`, `sha`, `latest-on-main`) |
| `publicBuild` / `build.public` | `bool` | Explicit classification override |
| `publicBranches*`, `nonPublicBranches*` | `string \| string[]` | Branch classification rules |
| `classification.*` | `object` | Nested branch classification settings |
| `tagPolicy.public` / `tagPolicy.nonPublic` | `tagKind \| tagKind[]` | Tag strategy policy by classification (same allowed values as `tags`) |
| `nonPublicMode` | `full-only` | Optional stricter non-public behavior mode |
| `aliases.*` | `object` | Branch alias generation settings |
| `aliases.rules[]` | `array` | Match/template alias rules |
| `stages` | `object` | Named stage definitions |
| `stageFallback` | `bool` | Fallback behavior for stage requests |

### Docker option defaults (when omitted)

- `runner`: `build`
- `push.enabled` / `pushEnabled`: `true`
- `push.denyNonPublicPush` / `denyNonPublicPush`: `false`
- `latest`: `false`
- `stageFallback`: `true`

Tag policy defaults are intentionally symmetric:

- Public and non-public both default to: `full + majorMinor + major`
- You can diverge behavior by setting `tagPolicy.public` and `tagPolicy.nonPublic` differently

### Tag defaults and strong options

Supported tag strategy options are strongly constrained to:

- `full`
- `semver`
- `majorMinor` (canonical)
- `majorminor` (compatibility alias)
- `major-minor` (compatibility alias)
- `major`
- `branch`
- `sha`
- `latest-on-main`

If `tags` is omitted:

- effective default strategy is `full + majorMinor + major`
- for pre-release versions, major and major.minor tags are emitted with the prerelease suffix appended
  (example: `0.1.0-local` -> `0.1-local`, `0-local`)

If `tagPolicy` is omitted:

- the same default strategy behavior above is applied by classification fallback

If you want stricter non-public behavior:

- set `nonPublicMode: "full-only"` to force non-public builds to emit only the `full` tag

### Docker environment variable overrides

The Docker provider supports environment-driven configuration so repositories can keep
artifact settings minimal.

Resolution order:

1. Process environment variables.
2. `.rexo/.env` values.
3. `.env` values in repository root.
4. Artifact settings from `artifacts[].settings`.
5. Provider defaults.

Important scope note:

- Docker environment variables are global for the process. If you have multiple Docker
  artifacts in one run, the same environment values are applied while resolving each
  artifact.

Image target resolution behavior:

- If `settings.image` is provided, it is used directly.
- Otherwise image is composed from registry and repository (`DOCKER_TARGET_REGISTRY` +
  `DOCKER_TARGET_REPOSITORY`, then settings fallbacks).

Supported Docker environment variables:

| Environment variable | Purpose | Overrides / interacts with |
| --- | --- | --- |
| `DOCKER_TARGET_REGISTRY` | Target registry host | `settings.target.registry`, `settings.registry` |
| `DOCKER_TARGET_REPOSITORY` | Target repository name/path | `settings.target.repository`, `settings.repository` |
| `DOCKERFILE_PATH` | Dockerfile path | `settings.dockerfile`, `settings.file` |
| `DOCKER_CONTEXT` | Docker build context | `settings.context` |
| `DOCKER_RUNNER` | Build runner (`build`, `buildx`, `auto`) | `settings.runner` |
| `DOCKER_PLATFORM` | Build platform value | `settings.platform` |
| `DOCKER_BUILD_TARGET` | Build target/stage | `settings.buildTarget` |
| `DOCKER_BUILD_OUTPUT` | Build output flags | `settings.buildOutput` |
| `DOCKER_BUILD_ARGS` | Build args (string/object syntax) | `settings.buildArgs` |
| `DOCKER_BUILD_ARGS_FILE` | Path to JSON file containing build args object | `settings.buildArgs` |
| `DOCKER_SECRETS` | JSON object for BuildKit secrets | `settings.secrets` |
| `DOCKER_SECRET_<ID>` | Secret env shortcut, one var per secret id | Merged with `DOCKER_SECRETS`/`settings.secrets` |
| `DOCKER_PUSH_ENABLED` | Enable/disable push (`true`/`false`) | `settings.push.enabled`, `settings.pushEnabled` |
| `DOCKER_PUSH_BRANCHES` | Delimited branch patterns for push eligibility | `settings.push.branches`, `settings.pushBranches`, `settings.push.branchesShortcut` |
| `DOCKER_CLEANUP_LOCAL` | Cleanup mode (`true`, `false`, `auto`) | `settings.cleanup.local`, `settings.cleanupLocal` |
| `DOCKER_TAG_LATEST` | Add `latest` tag (`true`/`false`) | `settings.tags.latest`, `settings.latest` |
| `DOCKER_LOGIN_USERNAME` | Registry login username | Falls back to `DOCKER_AUTH_USERNAME` |
| `DOCKER_LOGIN_PASSWORD` | Registry login password/token | Falls back to `DOCKER_AUTH_PASSWORD` |
| `DOCKER_LOGIN_REGISTRY` | Registry host for login | Falls back to `DOCKER_AUTH_REGISTRY`, then settings/inferred image registry |
| `DOCKER_AUTH_USERNAME` | Alternate alias for login username | Used if `DOCKER_LOGIN_USERNAME` not set |
| `DOCKER_AUTH_PASSWORD` | Alternate alias for login password/token | Used if `DOCKER_LOGIN_PASSWORD` not set |
| `DOCKER_AUTH_REGISTRY` | Alternate alias for login registry | Used if `DOCKER_LOGIN_REGISTRY` not set |

Login behavior:

- Login is attempted when username or password env values are provided.
- Both username and password must be set together.
- Login registry is resolved from login env vars, then `settings.loginRegistry`, then
  inferred from image.
- **GHCR zero-config (GitHub Actions)**: when no login credentials are configured and the
  target registry contains `ghcr.io`, Rexo automatically uses `GITHUB_ACTOR` and
  `GITHUB_TOKEN` (both injected by GitHub Actions). No `secrets:` binding required —
  only `permissions: packages: write` in the workflow.

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
| `apiKeyEnv` | `string` | Environment variable containing API key (defaults to `NUGET_API_KEY`) |

#### NuGet authentication

Resolution order: process env → `.rexo/.env` → `.env` → CI-native fallback.

Scope note: NuGet environment variables are global for the process. Multiple `nuget` artifacts in one run all use the same credential resolution — which is the normal case when a repo publishes several NuGet packages to the same feed.

| Environment variable | Purpose |
| --- | --- |
| `NUGET_API_KEY` | API key for push (default; overridden by `settings.apiKeyEnv`) |
| `NUGET_AUTH_TOKEN` | Alternative API key alias, used if `NUGET_API_KEY` is not set |
| `GITHUB_TOKEN` | Auto-used for `nuget.pkg.github.com` when no explicit key is set |
| `SYSTEM_ACCESSTOKEN` | Auto-used for Azure Artifacts feeds when no explicit key is set |

- **GHCR/GitHub Packages zero-config**: when `settings.source` contains `nuget.pkg.github.com` and no API key is configured, `GITHUB_TOKEN` is used automatically. Requires `permissions: packages: write` in the workflow.
- **Azure Artifacts zero-config**: when the source is an Azure Artifacts feed and no key is configured, `SYSTEM_ACCESSTOKEN` is used automatically (must be enabled in the pipeline job).

### Helm OCI artifact settings (`type: "helm-oci"`)

Supported keys:

| Key | Type | Purpose |
| --- | --- | --- |
| `chart` | `string` | Chart name used to resolve packaged archive names (defaults to artifact name) |
| `chartPath` | `string` | Path to chart root containing `Chart.yaml` (default `chart`) |
| `output` | `string` | Output directory for packaged `.tgz` files (default `artifacts/charts`) |
| `oci` | `string` | Full OCI destination (`oci://registry/repository`) |
| `registry` | `string` | OCI registry host (used with `repository` when `oci` is not set) |
| `repository` | `string` | OCI repository path (used with `registry` when `oci` is not set) |
| `loginRegistry` | `string` | Optional registry host override for `helm registry login` |
| `useDocker` | `boolean` | Set to `false` to disable the Docker fallback when host `helm` CLI is unavailable (default `true`) |
| `dockerImage` | `string` | Override the Helm container image used when host `helm` CLI is unavailable |

Push destination resolution:

- If `settings.oci` is set, it is used directly (with `oci://` normalized when omitted).
- Otherwise destination is composed from `settings.registry` + `settings.repository`.

#### Helm OCI authentication

Resolution order: process env → `.rexo/.env` → `.env` → CI-native fallback.

Scope note: Helm OCI environment variables are global for the process. Multiple `helm-oci` artifacts in one run all use the same credential resolution — which is the normal case when a repo packages several charts to the same OCI registry.

| Environment variable | Purpose |
| --- | --- |
| `HELM_REGISTRY_USERNAME` | Registry login username |
| `HELM_REGISTRY_PASSWORD` | Registry login password/token |
| `HELM_REGISTRY` | Registry host for login (falls back to `settings.loginRegistry`, then `settings.registry`) |

- **GHCR zero-config (GitHub Actions)**: when no credentials are configured and the registry contains `ghcr.io`, `GITHUB_ACTOR` and `GITHUB_TOKEN` are used automatically. Requires `permissions: packages: write`.
- Both `HELM_REGISTRY_USERNAME` and `HELM_REGISTRY_PASSWORD` must be set together; partial credentials are an error.

Helm runtime behavior:

- `helm-oci` operations use the host `helm` CLI when available.
- If `helm` is not installed, runtime automatically falls back to `docker run` with a Helm image.
- Disable the Docker fallback with `settings.useDocker: false`.
- Fallback image resolution order: `HELM_CONTAINER_IMAGE` env var, then `settings.dockerImage`, then default `alpine/helm:3.17.3`.

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

---

## `runtime`

Cross-cutting runtime policy configuration.

```jsonc
"runtime": {
  "output": {
    "emitRuntimeFiles": true,
    "root": "artifacts"
  },
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

## Template Variables

Environment variable source behavior:

- Rexo resolves environment values from process environment first.
- If a value is not present in process env, Rexo falls back to repository env files:
  - `.rexo/.env` (higher file precedence)
  - `.env` (root)
- This applies to template `{{env.<VAR>}}` lookups and provider environment-driven behavior.

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
| `builtin:push-artifacts` | Push artifacts; enforce push rules; write `<runtime.output.root>/manifest.json` when `runtime.output.emitRuntimeFiles=true` |
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
| `rx new` | Alias for `rx init` |
| `rx init detect` | Preview auto-detection and policy recommendations without writing files |
| `rx config resolved` | Print the effective merged config (`rexo.json`) as JSON |
| `rx config sources` | Show config file path and load status |

`rx init` also supports interactive policy setup and these non-interactive flags:

- `--template auto|dotnet|node|python|go|generic|blank`
- `--detect` / `--dry-run` (preview only, no files written)
- `--schema-source local|remote`
- `--with-policy`
- `--policy-template <name>` (e.g. `standard`, `dotnet`, `dotnet-api`, `dotnet-library`)
- `--with-docker-artifact`
- `--without-docker-artifact` (non-interactive opt-out when Dockerfile is detected)
- `--yes` and `--force`

**Template descriptions:**

| Template | What it scaffolds |
| --- | --- |
| `auto` | Detects project type from files in the working directory |
| `dotnet` | .NET project (restore, build, test steps) |
| `node` | Node.js project (npm install, npm test steps) |
| `python` | Python project (compileall, pytest steps) |
| `go` | Go project (go build, go test steps) |
| `generic` | Generic shell-based commands |
| `blank` | Empty `rexo.json` with no `extends` — a pure command/alias runtime. Add everything manually. |

Detection behavior notes:

- `auto` detects project type (`dotnet`, `node`, `python`, `go`, `generic`) and also inspects `.csproj` metadata for .NET library vs app/service hints.
- Dockerfiles are detected and surfaced as packaging hints.
- During `rx init`, the wizard asks "Will this repo build and publish artifacts? (Docker images, packages, charts, etc.)". If you answer yes (or if artifacts are auto-detected in non-interactive mode), `embedded:standard` is added to `extends` automatically so lifecycle commands (`plan`, `build`, `release`, etc.) are available.
- When a Dockerfile is detected **and** the artifacts question is answered yes, docker artifact scaffolding defaults to `yes` as a follow-up question.
- Use `--without-docker-artifact` to keep a non-interactive run minimal even when Dockerfiles are present.
- When `--with-policy` is used with `--template auto` and a .NET repo is detected:
  - library-like repos prefer `dotnet-library` when available.
  - repos with Dockerfile signals prefer `dotnet-api` when available.
- `blank` never generates `extends` — it is always policy-free. Using `--with-policy` with `blank` is an error.
- Non-blank templates add `embedded:standard` automatically **only when artifacts are present** (detected or explicitly requested). A command-only scaffold omits `extends` entirely regardless of template.

Docker artifact scaffold behavior:

- The scaffolded artifact omits `name` so it inherits the repository root `name`.
- The scaffolded artifact omits `image`; providers resolve image defaults from artifact naming and environment settings.
- `settings.dockerfile` and `settings.context` are only emitted when the detected Dockerfile is not discoverable by provider defaults.

**Extends wiring rules:**

| Scenario | Generated `extends` |
| --- | --- |
| `blank` template | *(omitted)* — always policy-free |
| Artifacts detected or requested, no `--with-policy` | `["embedded:standard"]` — added automatically |
| `--with-policy --policy-template standard` | `["embedded:standard"]` |
| `--with-policy --policy-template dotnet-api` (or any non-standard) | `["embedded:standard", "embedded:dotnet-api"]` |
| No artifacts, no `--with-policy` | *(omitted)* — pure command/alias config |

Examples:

```bash
# dotnet-style experience
rx new --yes

# blank canvas — add your own commands
rx init --yes --template blank

# preview auto detection and recommendation
rx init detect

# preview with explicit auto template and JSON output
rx init --template auto --detect --json

# scaffold config and include starter docker artifact
rx init --yes --template auto --with-docker-artifact

# keep non-interactive init minimal even when Dockerfile is detected
rx init --yes --template auto --without-docker-artifact

# use a specific policy template (stacks standard + dotnet-api in extends)
rx init --yes --template dotnet --with-policy --policy-template dotnet-api
```

`rx init detect --json` returns a richer machine-readable contract that includes versioned detection metadata and ranked recommendations:

- `contractVersion`
- `detection`
- `recommendations[]` with `kind`, `value`, `confidence`, and `reasons[]`

Example (truncated):

```json
{
  "Command": "init detect",
  "Outputs": {
    "contractVersion": "1.1",
    "detection": {
      "DetectedTemplate": "dotnet",
      "ResolvedTemplate": "dotnet",
      "DotnetProjectKind": "app/service",
      "HasDockerfile": true,
      "Signals": ["template-detected:dotnet", "dockerfile:present"]
    },
    "recommendations": [
      {
        "Kind": "policy-template",
        "Value": "dotnet-api",
        "Confidence": 0.9,
        "Reasons": ["Dockerfile detected; API/service workflow likely."]
      },
      {
        "Kind": "docker-artifact",
        "Value": "consider-enable",
        "Confidence": 0.85,
        "Reasons": ["Dockerfile detected. Starter docker artifact can speed setup."]
      }
    ]
  }
}
```

CI scaffolding mode:

- `rx init ci --provider github|azdo|both`
- Generates thin wrapper pipeline templates:
- `.github/workflows/rexo-release.yml`
- `.azuredevops/rexo-release.yml`

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
| `--set <key.path=value>` | Override a single config property at runtime (repeatable) |

### `--set` overrides

`--set` is the highest-priority layer in the config merge pipeline. It runs after `REXO_OVERLAY` and wins over every other config source. Use it for one-off adjustments without touching config files:

```bash
# Change the version provider for a single run
rx build --set versioning.provider=env

# Disable push without editing rexo.json
rx release --set runtime.push.enabled=false

# Override a fallback version
rx version --set versioning.fallback=0.0.0-local

# Multiple overrides — repeat the flag
rx release --set versioning.provider=fixed --set versioning.fallback=1.2.3
```

The key path uses dot-notation that matches the `rexo.json` property hierarchy (case-insensitive). Values that look like JSON booleans, numbers, or `null` are parsed as their native types; everything else is treated as a string.

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

- Local policy files are still supported (`policy.json` alongside `rexo.json`, or files in `.rexo/`; `.repo/` remains backward compatible).
- Additional policy sources can be loaded via `REXO_POLICY_SOURCES` (semicolon/comma separated):
- HTTP/HTTPS URL: `https://.../policy.json` (optional pin: `#sha256=<hex>`)
- Git reference: `git+<repo>@<ref>#<path>`
- NuGet reference: `nuget:<packageId>@<version>#<pathInPackage>`
- Loaded remote policies are cached under `.rexo/cache/policies/`.
- Trust model is enforced via `REXO_POLICY_TRUST` (host allow-list, or `allow-all`).
- Pin enforcement can be enabled with `REXO_POLICY_REQUIRE_PINNED=true`.

### Parallel Step Execution

- Basic parallel step execution works via the `parallel: true` flag on steps, with a `maxParallel` concurrency cap.
- Dependency-aware fan-in is supported via `dependsOn` (step IDs) within parallel groups.

### Run Manifest

- The run manifest (written via `--json-file`) includes steps, version, CI context, git context, and errors.
- **Push decisions and artifact entries** are propagated from `builtin:push-artifacts` into command results and JSON output.
- The artifact manifest file `<runtime.output.root>/manifest.json` is written separately by `builtin:push-artifacts` when `runtime.output.emitRuntimeFiles=true` for file-based consumption.

### Config Inspection Commands

- `rx config resolved` and `rx config sources` are registered but display basic output only.
- `rx config materialize` writes provider config files (e.g. `GitVersion.yml`) but does not yet have a rich interactive UI.

### UI and Interactive Features

- Running `rx` with no arguments launches an interactive project picker when multiple config-bearing sibling directories are found, then shows the command list for the selected project.
- The command picker shows available commands but does not support keyboard navigation (arrow-key selection is not implemented).
