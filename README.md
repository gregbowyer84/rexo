# Rexo

Rexo is a config-driven repository command runtime for local and CI workflows. Use it as a lightweight command/alias system, or opt into policy templates for build, verification, artifact production, and release orchestration.

## Quickstart

### Minimal command runtime

```bash
# Install Rexo globally
dotnet tool install --global Rexo.Cli

# Initialize a minimal repository command runtime
rx init --template minimal --yes

# Run a configured command
rx hello
```

### Standard lifecycle (opt-in)

```bash
# Initialize with an explicit policy template
rx init --template auto --with-policy --yes

# See what would happen
rx plan

# Run the full release pipeline
rx release

# Push artifacts (with confirmation on local machine)
rx release --push
```

## Goals

- Keep the CLI contract minimal and stable
- Drive behavior through repository configuration and policies
- Keep local and CI behavior identical
- Produce inspectable run and release artifacts

## Documentation

- [Configuration Reference](docs/CONFIGURATION.md)
- [Artifact Providers](docs/artifacts/README.md)
- [Builtins Reference](docs/BUILTINS.md)
- [Embedded Items Reference](docs/EMBEDDED.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Development Guide](docs/DEVELOPMENT.md)

## Minimal Configuration

Rexo does not apply lifecycle commands automatically.

No extends means no policy commands.

A config can be as small as a command and alias map:

```json
{
  "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/rexo.schema.json",
  "schemaVersion": "1.0",
  "name": "my-repo",
  "commands": {
    "hello": {
      "steps": [
        {
          "run": "echo hello"
        }
      ]
    },
    "local build": {
      "steps": [
        {
          "run": "dotnet build"
        }
      ]
    }
  },
  "aliases": {
    "b": "local build"
  }
}
```

To opt into the standard lifecycle, extend embedded:standard:

```json
{
  "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/rexo.schema.json",
  "schemaVersion": "1.0",
  "name": "my-api",
  "extends": [
    "embedded:standard"
  ],
  "artifacts": [
    {
      "type": "docker",
      "name": "api",
      "settings": {
        "image": "ghcr.io/my-org/my-api",
        "dockerfile": "Dockerfile",
        "context": "."
      }
    }
  ]
}
```

With embedded:standard, Rexo provides these commands:

```bash
rx plan                 # What would happen
rx validate             # Check config
rx version              # Show resolved version
rx test                 # Run tests
rx analyze              # Run analysis
rx verify               # Quality gate (test + analyze)
rx build                # Build and tag artifacts locally
rx tag                  # Tag artifacts
rx push                 # Push artifacts (requires --confirm locally)
rx release              # Full pipeline (validate → verify → build → tag)
rx release --push       # Full pipeline + push
rx clean                # Remove generated output
```

## Current Structure

- `src/` implementation projects
- `tests/` unit and integration test projects
- `docs/` architecture and design docs

## Prerequisites

- .NET SDK 10.0.203+
- Git

## Build

```bash
dotnet restore
dotnet build solution.slnx -c Release
dotnet test solution.slnx -c Release
```

## CLI

The packaged tool command is `rx`.

You can also run Rexo without installing it globally by using `dotnet`:

```bash
dotnet tool run rx -- --help
dotnet tool run rx -- init --yes --template auto
```

## Versioning

Versioning uses GitVersion (mainline) via [GitVersion.yml](GitVersion.yml).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## Security

See [SECURITY.md](SECURITY.md).
