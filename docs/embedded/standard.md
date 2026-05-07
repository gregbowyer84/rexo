# Embedded Policy: standard

Purpose:

- General lifecycle policy for most repositories.
- Works well with artifact-only config when explicitly added via `extends`.

## Commands

### plan

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

### validate

Description: Validate repository configuration.

Options: none.

Steps:

1. `builtin:validate`

### version

Description: Resolve repository version.

Options: none.

Steps:

1. `builtin:resolve-version`

### test

Description: Run configured tests.

Options: none.

Steps:

1. `builtin:test`

### analyze

Description: Run configured analysis.

Options: none.

Steps:

1. `builtin:analyze`

### verify

Description: Run validation, tests, and analysis.

Options: none.

Steps:

1. `builtin:validate`
2. `builtin:test`
3. `builtin:analyze`

Contract note:

- User-facing `verify` command includes validate.
- `builtin:verify` (used by `release`) runs test + analyze.

### build

Description: Build and tag configured artifacts locally.

Options: none.

Steps:

1. `builtin:validate`
2. `builtin:resolve-version`
3. `builtin:build-artifacts`
4. `builtin:tag-artifacts`

### tag

Description: Tag configured artifacts.

Options: none.

Steps:

1. `builtin:resolve-version`
2. `builtin:tag-artifacts`

### push

Description: Push configured artifacts when explicitly confirmed.

Options:

- `--confirm` (`bool`, default `false`)

Steps:

1. `builtin:push-artifacts` with `with.confirm = {{options.confirm}}`

Behavior notes:

- Push is opt-in everywhere (local and CI) — `--confirm` is always required.
- `rx push` succeeds but skips push with clear guidance when `--confirm` is not passed.
- `rx push --confirm` attempts actual push subject to policy/provider gates.

### release

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

### clean

Description: Remove generated Rexo output.

Options: none.

Steps:

1. `builtin:clean`

Behavior notes:

- Explicit utility command.
- Not run automatically by release/build/verify.

## Aliases

- `all` -> `release`
- `ship` -> `push`
