# Convenience Builtins

Composite builtins that chain core lifecycle operations for reusable shorthand behavior.

## builtin:ship-artifacts

Calls:

1. `tag-artifacts`
2. `push-artifacts`

Purpose:

- Tag and push all matching artifacts in one operation.

## builtin:all-artifacts

Calls:

1. `build-artifacts`
2. `tag-artifacts`
3. `push-artifacts`

Purpose:

- Complete full artifact lifecycle (build, tag, push) in one operation.

## builtin:plan

Calls:

- `plan-artifacts` (all artifact types)

Purpose:

- Alias for `plan-artifacts` — produce a plan for all artifacts.

## builtin:ship

Calls:

- `ship-artifacts`

Purpose:

- Alias for `ship-artifacts` — tag and push all artifacts.

## builtin:all

Calls:

- `all-artifacts`

Purpose:

- Alias for `all-artifacts` — build, tag, and push all artifacts.
