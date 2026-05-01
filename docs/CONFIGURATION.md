# Configuration Model (Draft)

Default repository configuration file:

- `repo.json`

Schema contract (required):

- `$schema`: must point to the canonical schema URI (remote) or `schemas/1.0/schema.json` (local)
- `schemaVersion`: must be `1.0`

The configuration loader validates `repo.json` against the versioned schema file at:

- `schemas/1.0/schema.json`

If either schema metadata is missing/unsupported, or the config does not conform to the schema,
loading fails with a validation error.

Planned top-level sections include:

- metadata
- extends/policies
- commands
- aliases
- versioning
- artifacts
- tests
- analysis
- push rules

For detailed functional requirements, see [scope.md](scope.md).
