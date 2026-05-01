# Rexo

Rexo is a config-driven repository runtime for build, verification, versioning, artifact production, and release orchestration.

## Status

Early implementation bootstrap based on [docs/scope.md](docs/scope.md).

## Goals

- Keep the CLI contract minimal and stable
- Drive behavior through repository configuration and policies
- Keep local and CI behavior identical
- Produce inspectable run and release artifacts

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

You can also run Rexo without installing it globally by using `dnx`:

```bash
dotnet dnx Rexo.Cli -- --help
dotnet dnx Rexo.Cli -- init --yes --template auto
```

## Versioning

Versioning uses GitVersion (mainline) via [GitVersion.yml](GitVersion.yml).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## Security

See [SECURITY.md](SECURITY.md).
