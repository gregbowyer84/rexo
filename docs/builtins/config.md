# Configuration Utility Builtins

Builtins for configuration inspection and provider file materialization.

## builtin:config-resolved

Purpose:

- Emit effective loaded config as JSON.

Calls:

- `JsonSerializer.Serialize(config)`

Outputs:

- `json` (serialized config)

Exit behavior:

- Success: exit code `0`

## builtin:config-materialize

Purpose:

- Materialize provider-side files when needed (currently GitVersion bootstrap support).

Calls:

- If version provider is gitversion and file absent:
  - Write `GitVersion.yml`

Outputs:

- `message`
- `files` (comma-separated written file paths)

Exit behavior:

- Success: exit code `0`
