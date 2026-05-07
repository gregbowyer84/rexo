# Docker-Scoped Builtins

Builtins filtered to Docker artifacts only, with Docker-specific stage build support.

## builtin:docker-plan

Calls:

- `plan-artifacts` filtered to docker artifacts only

Purpose:

- Produce plan for Docker artifacts only.

## builtin:docker-ship

Calls:

1. `tag-artifacts` (docker only)
2. `push-artifacts` (docker only)

Purpose:

- Tag and push Docker artifacts only.

## builtin:docker-all

Calls:

1. `build-artifacts` (docker only)
2. `tag-artifacts` (docker only)
3. `push-artifacts` (docker only)

Purpose:

- Complete Docker artifact lifecycle (build, tag, push).

## builtin:docker-stage

Purpose:

- Build a single named docker stage from artifact `settings.stages`.

Calls:

- For each docker artifact, provider `BuildAsync(...)` with stage-focused settings

Inputs:

- Stage name from either:
  - `ctx.Args.stage`
  - `ctx.Options.stage`
- Artifact setting object: `settings.stages.<stageName>`

Outputs:

- Success: `message = "Docker stage '<name>' completed."`
- Failure: `error`

Exit behavior:

- Missing stage argument: exit code `2`
- Missing stage config for artifact: exit code `2`
- Build failure: exit code `5`
