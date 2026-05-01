# Rexo CLI (`rx`)

Rexo is a config-driven repository automation CLI. It runs the same workflow model locally and in CI from a repository config file.

## Install

```bash
dotnet tool install --global Rexo.Cli
```

To update:

```bash
dotnet tool update --global Rexo.Cli
```

## Run Without Installing (dnx)

If you prefer not to install globally, run Rexo directly from NuGet:

```bash
dotnet dnx Rexo.Cli -- --help
dotnet dnx Rexo.Cli -- init --yes --template auto
```

Use `--` after `Rexo.Cli` so remaining arguments are passed to `rx`.

## Quick Start

1. Run `rx init` (defaults to `.rexo/rexo.json`).
2. Add commands and steps.
3. Run commands with `rx`.

Example:

```bash
rx init
rx list
```

Non-interactive example with policy template:

```bash
rx init --yes --location .rexo --template auto --with-policy --policy-template dotnet
```

Non-interactive example that also downloads AI instructions into the repo:

```bash
rx init --yes --with-instructions
```

Custom destination for instructions file:

```bash
rx init --yes --with-instructions --instructions-path .github/instructions/rexo.instructions.md
```

Minimal example:

```json
{
  "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema/v1.0/schema.json",
  "schemaVersion": "1.0",
  "name": "my-repo",
  "commands": {
    "build": {
      "description": "Build the project",
      "options": {},
      "steps": [
        { "run": "dotnet build -c Release" }
      ]
    }
  },
  "aliases": {}
}
```

Run it:

```bash
rx build
```

## Common Commands

```bash
rx list
rx explain build
rx config sources
rx config resolved --json
rx doctor
```

## Configuration Discovery

Rexo looks for configuration in this order:

1. `rexo.json`, `rexo.yaml`, `rexo.yml`
2. `.rexo/rexo.json`, `.rexo/rexo.yaml`, `.rexo/rexo.yml`
3. Backward-compatible fallback: `repo.json|yaml|yml` (root and `.repo/`)

Policy files are discovered in root, `.rexo/`, and legacy `.repo/` locations.

## Notes

- Use `rx` directly to run configured commands.
- Use `rx run <command>` if you want explicit run semantics.
- Use `--json` or `--json-file <path>` for machine-readable output.

## Documentation

| Resource | Link |
| --- | --- |
| GitHub repository | <https://github.com/agile-north/rexo> |
| Configuration reference | <https://github.com/agile-north/rexo/blob/release/next/docs/CONFIGURATION.md> |
| JSON Schema | <https://raw.githubusercontent.com/agile-north/rexo/schema/v1.0/schema.json> |
| Architecture overview | <https://github.com/agile-north/rexo/blob/release/next/docs/ARCHITECTURE.md> |
| Contributing / dev guide | <https://github.com/agile-north/rexo/blob/release/next/docs/DEVELOPMENT.md> |

> **AI assistant tip**: `rx init --with-instructions` downloads
> [`rexo.instructions.md`](https://github.com/agile-north/rexo/blob/release/next/docs/rexo.instructions.md)
> into `.github/instructions/rexo.instructions.md` by default. Use `--instructions-path` to choose
> another repo-relative destination.
