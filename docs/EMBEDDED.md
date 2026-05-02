# Embedded Items Reference

This guide documents every embedded policy template currently shipped with Rexo,
what each command does, which options it accepts, and common use cases.

Use this alongside `docs/CONFIGURATION.md`.

For per-builtin runtime contract details (inputs, outputs, internal calls, exit behavior),
see `docs/BUILTINS.md`.

For command-line mental models of each builtin, see the "Approximate Shell Equivalents"
section in `docs/BUILTINS.md`.

## What "Embedded" Means

Rexo ships policy templates as embedded resources in the CLI assembly
(see `Rexo.Policies.EmbeddedTemplates`).

Current embedded templates:

- `standard`
- `dotnet`

In configuration, use them through `extends`:

```json
{
  "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/rexo.schema.json",
  "schemaVersion": "1.0",
  "name": "orders-api",
  "extends": ["embedded:standard"],
  "artifacts": [
    {
      "type": "docker",
      "name": "api",
      "settings": {
        "image": "ghcr.io/agile-north/orders-api"
      }
    }
  ]
}
```

Design intent:

- Repo config says what this repo emits (artifacts, versioning, analysis/test details).
- Embedded policy says how this repo behaves (lifecycle command shape).

## Embedded Template: standard

Purpose:

- General lifecycle policy for most repositories.
- Works well with artifact-only config.

### Commands

#### plan

Description: Validate config and print a build/push plan.

Options:

- `--push` (`bool`, default `false`)

Steps:

1. `builtin:validate`
2. `builtin:resolve-version`
3. `builtin:plan-artifacts` with `with.push = {{options.push}}`

Behavior notes:

- `rx plan` reports artifact build plan and push as "not requested".
- `rx plan --push` reports push eligibility and skip reasons.

#### validate

Description: Validate repository configuration.

Options: none.

Steps:

1. `builtin:validate`

#### version

Description: Resolve repository version.

Options: none.

Steps:

1. `builtin:resolve-version`

#### test

Description: Run configured tests.

Options: none.

Steps:

1. `builtin:test`

#### analyze

Description: Run configured analysis.

Options: none.

Steps:

1. `builtin:analyze`

#### verify

Description: Run validation, tests, and analysis.

Options: none.

Steps:

1. `builtin:validate`
2. `builtin:test`
3. `builtin:analyze`

Contract note:

- User-facing `verify` command includes validate.
- `builtin:verify` (used by `release`) runs test + analyze.

#### build

Description: Build and tag configured artifacts locally.

Options: none.

Steps:

1. `builtin:validate`
2. `builtin:resolve-version`
3. `builtin:build-artifacts`
4. `builtin:tag-artifacts`

#### tag

Description: Tag configured artifacts.

Options: none.

Steps:

1. `builtin:resolve-version`
2. `builtin:tag-artifacts`

#### push

Description: Push configured artifacts when explicitly confirmed.

Options:

- `--confirm` (`bool`, default `false`)

Steps:

1. `builtin:push-artifacts` with `with.confirm = {{options.confirm}}`

Behavior notes:

- Local (non-CI) push requires explicit confirmation.
- `rx push` succeeds but skips push with clear guidance.
- `rx push --confirm` attempts actual push subject to policy/provider gates.

#### release

Description: Validate, verify, build, tag, and optionally push.

Options:

- `--push` (`bool`, default `false`)

Steps:

1. `builtin:validate`
2. `builtin:resolve-version`
3. `builtin:verify`
4. `builtin:build-artifacts`
5. `builtin:tag-artifacts`
6. `builtin:push-artifacts` when `{{options.push}}`, with `with.confirm = {{options.push}}`

Behavior notes:

- `rx release` does not push.
- `rx release --push` passes explicit push intent into builtin push logic.

#### clean

Description: Remove generated Rexo output.

Options: none.

Steps:

1. `builtin:clean`

Behavior notes:

- Explicit utility command.
- Not run automatically by release/build/verify.

### Aliases

- `all` -> `release`
- `ship` -> `push`

## Embedded Template: dotnet

Purpose:

- Dotnet-focused command surface.
- Adds restore/format/pack-centric workflows.

### Commands

#### ci

Description: Validate, resolve version, test, analyze, and build configured artifacts.

Options: none.

#### release

Description: Run CI flow, tag artifacts, optionally push.

Options:

- `--push` (`bool`, default currently set as string `"false"` in template)

Behavior notes:

- Push step is guarded by a `when` expression.

#### restore

Description: Run `dotnet restore`.

Options: none.

#### format

Description: Verify or apply `dotnet format`.

Options:

- `--fix` (`bool`, default currently set as string `"false"` in template)

Behavior notes:

- `--fix` false: `dotnet format --verify-no-changes`
- `--fix` true: `dotnet format`

#### pack

Description: Resolve version and build artifacts intended for package workflows.

Options:

- `--configuration` (`string`, default `"Release"`)

### Aliases

- `build` -> `ci`
- `publish` -> `release`
- `r` -> `restore`
- `f` -> `format`

## Builtins Used By Embedded Templates

Core lifecycle builtins:

- `builtin:validate`: Validate loaded configuration.
- `builtin:resolve-version`: Resolve version and place it in execution context.
- `builtin:test`: Execute configured tests.
- `builtin:analyze`: Execute configured analysis.
- `builtin:verify`: Execute test + analyze as quality gate primitive.
- `builtin:build-artifacts`: Build all matching artifacts.
- `builtin:tag-artifacts`: Tag all matching artifacts.
- `builtin:push-artifacts`: Push artifacts, apply push gates, write artifact manifest.
- `builtin:plan-artifacts`: Print/emit plan model for build and push eligibility.
- `builtin:clean`: Remove generated output (`artifacts/`).

Related utility builtins available for custom commands:

- `builtin:config-resolved`
- `builtin:config-materialize`
- `builtin:plan`, `builtin:ship`, `builtin:all`
- `builtin:docker-plan`, `builtin:docker-ship`, `builtin:docker-all`, `builtin:docker-stage`

## Common Use Cases

### 1. Artifact-only repo with standard lifecycle

Use when you want immediate lifecycle commands with minimal config.

```json
{
  "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/rexo.schema.json",
  "schemaVersion": "1.0",
  "name": "orders-api",
  "extends": ["embedded:standard"],
  "artifacts": [
    {
      "type": "docker",
      "name": "api",
      "settings": {
        "image": "ghcr.io/agile-north/orders-api"
      }
    }
  ]
}
```

Common flow:

```bash
rx plan
rx verify
rx build
rx release
rx release --push
```

### 2. Publish already-built artifacts explicitly

Use when build/tag happened earlier and you only want publish phase.

```bash
rx push --confirm
```

Equivalent alias:

```bash
rx ship --confirm
```

### 3. Show push eligibility before release

Use when validating branch/PR/clean-tree gates before running full release.

```bash
rx plan --push
```

### 4. Dotnet developer convenience workflow

Use when you want dotnet-centric command aliases and formatting helpers.

```json
{
  "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/rexo.schema.json",
  "schemaVersion": "1.0",
  "name": "billing-service",
  "extends": ["embedded:dotnet"],
  "artifacts": [
    {
      "type": "nuget",
      "name": "Billing.Client",
      "settings": {
        "project": "src/Billing.Client/Billing.Client.csproj"
      }
    }
  ]
}
```

Common flow:

```bash
rx restore
rx ci
rx format --fix
rx release --push
```

## Option Mapping With Step with

Embedded templates now use step-local option mapping so command intent is explicit
and centralized in builtins.

Example from `standard` release:

```json
{
  "id": "push",
  "uses": "builtin:push-artifacts",
  "when": "{{options.push}}",
  "with": {
    "confirm": "{{options.push}}"
  }
}
```

This pattern is recommended for custom policies too.

## Policy Selection Guidance

Choose `embedded:standard` when:

- You want consistent cross-language lifecycle defaults.
- You want release and push semantics aligned with explicit local confirmation.

Choose `embedded:dotnet` when:

- You want restore/format/ci convenience commands out of the box.
- You are intentionally using dotnet-centric aliases.

## Practical Notes

- Local push without explicit confirmation is a successful skip, not a hard failure.
- Push eligibility is still governed by push rules and provider constraints.
- `clean` is intentionally explicit and not part of default release pipelines.
- Embedded templates can be overridden by repo commands/aliases as needed.
