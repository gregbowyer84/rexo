# Builtins Reference

This page is intentionally concise. Use the split docs as source of truth.

- [Builtins Overview](builtins/README.md)
- [Lifecycle Builtins](builtins/lifecycle.md)
- [Artifact Builtins](builtins/artifacts.md)
- [Convenience Builtins](builtins/convenience.md)
- [Docker Builtins](builtins/docker.md)
- [Configuration Builtins](builtins/config.md)
- [Builtin Patterns](builtins/patterns.md)

Current runtime lifecycle builtins are:

- `builtin:resolve-version`
- `builtin:validate`
- `builtin:clean`

Toolchain-specific `test`, `analyze`, and `verify` behavior is provided by embedded policy command overlays (for example `embedded:dotnet` and `embedded:node`), not by dedicated core builtins.
