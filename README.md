# Rexo

Rexo is a config-driven repository runtime for build, verification, versioning, artifact production, and release orchestration.

## Quickstart

```bash
# Install Rexo globally
dotnet tool install --global Rexo.Cli

# Initialize a new repository with auto-detection
rx init --template auto --yes

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
- [Builtins Reference](docs/BUILTINS.md)
- [Embedded Items Reference](docs/EMBEDDED.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Development Guide](docs/DEVELOPMENT.md)

## Minimal Configuration

Rexo applies the standard lifecycle policy automatically when a config does not define commands. In most repos you only need to declare what the repository emits:

```json
{
  "$schema": "https://raw.githubusercontent.com/agile-north/rexo/schema-v1.0/rexo.schema.json",
  "schemaVersion": "1.0",
  "name": "my-api",
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

Rexo will automatically provide these commands:

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
