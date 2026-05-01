# repoOS Implementation TODO

Last updated: 2026-05-01

This checklist maps the current implementation against the project scope in `docs/scope.md`.

Legend:
- [x] Done
- [ ] Not done
- [~] Partial / implemented in basic form only

## 1) CLI Surface and Routing

- [x] `repo version`
- [x] `repo help`
- [x] `repo doctor`
- [x] `repo list`
- [x] `repo explain <command>`
- [x] `repo run <command>`
- [x] Direct configured command dispatch (`repo <configured-command>`)
- [x] Multi-word command resolution (`repo branch feature my-change`)
- [x] Global flags: `--json`, `--json-file`, `--verbose`
- [x] Global flags: `--debug`, `--quiet`
- [x] Exit code mapping for common failures

## 2) Configuration Engine

- [x] Load `repo.json`
- [x] Config model includes commands, aliases, args, options, steps
- [x] Config model includes versioning, artifacts, tests, analysis
- [~] Validation: basic structural deserialization only (no schema validation)
- [x] Resolve `extends` from local file paths (breadth-first merge, circular detection)
- [~] Config merge order pipeline (defaults -> policies -> repo -> overlays -> CLI)
- [ ] Environment overlays
- [ ] Merge strategy customization for arrays/objects
- [ ] Alternative config file names (`repo.yaml`, `.repo/repo.json`, etc.)

## 3) Command Resolution Order

- [x] Built-in command match
- [x] Exact config command match
- [x] Config alias match
- [ ] Policy-provided command match
- [~] Not-found suggestions (basic not-found message exists; no suggestion engine)

## 4) Execution Engine

- [x] Sequential step execution
- [x] Step types: `run`, `uses`, `command`
- [x] `when` condition evaluation (truthy/falsey rendering)
- [x] Continue-on-error support (`continueOnError`)
- [x] Step output propagation into execution context
- [x] `parallel` step groups (consecutive parallel steps batched via Task.WhenAll)
- [x] Output capture via `outputPattern` (regex named groups) and `outputFile` (write stdout to file)
- [ ] Command-level parallel settings (`parallel`, `maxParallel`)
- [ ] Advanced dependency and fan-in behavior for parallel groups

## 5) Templating

- [x] Variable replacement
- [x] Context references: `args`, `options`, `repo`, `version`, `steps`, `env`
- [x] Basic filters: `slug`, `default(...)`, `upper`, `lower`
- [~] Simple conditions via truthy rendered values
- [ ] Expression operators/comparisons in templates (e.g. `==` expressions)
- [ ] Path helper functions beyond current filters

## 6) Built-in Primitives

- [x] `builtin:validate`
- [x] `builtin:resolve-version`
- [x] `builtin:test`
- [x] `builtin:analyze`
- [x] `builtin:verify`
- [x] `builtin:build-artifacts`
- [x] `builtin:tag-artifacts`
- [x] `builtin:push-artifacts`
- [x] `builtin:config-resolved`
- [x] `builtin:config-materialize`
- [x] `doctor` built-in command path

## 7) Versioning

- [x] Fixed provider
- [x] Environment variable provider
- [x] GitVersion provider (basic shell + parse + fallback)
- [x] MinVer provider (shells out to `dotnet minver`, registered as `minver`)
- [x] NBGV provider (shells out to `nbgv get-version -f json`, registered as `nbgv`)
- [ ] Basic git provider (as distinct provider from gitversion/env/fixed)
- [~] Version contract fields from scope are partially present
- [ ] Full output contract fields (build metadata, branch, assembly/file versions, etc.)

## 8) Artifacts

- [x] Artifact provider registry
- [x] Docker provider: build/tag/push
- [x] NuGet provider: pack/push
- [x] Tag strategy support (semver/branch/sha/latest-on-main variants)
- [x] Push policy rules enforced via `pushRulesJson` (`noPushInPullRequest`, `requireCleanWorkingTree`)
- [x] Artifact manifest file output (`artifacts/manifest.json` written after push)
- [~] Rich artifact metadata capture (manifest written; full run manifest integration partial)

## 9) Verification and Analysis

- [x] `dotnet test` orchestration
- [x] Basic test result parsing (total/passed/failed/skipped)
- [x] Basic analysis hook (`dotnet format --verify-no-changes` / build analysis path)
- [x] Verify primitive (`test + analyze`)
- [~] TRX and coverage output support is partial/basic
- [x] Coverage thresholds enforcement (Cobertura XML parsed; lineCoverageThreshold checked)
- [ ] Extended analysis toolchain (SARIF/security scanners)

## 10) CI and Git Context

- [x] Git context detection (branch, commit, short sha, remote, clean state)
- [x] CI detection for common providers (at least core providers)
- [x] CI context attached to execution context
- [~] CI metadata depth is partial compared to full scope (run number/tag/etc.)

## 11) Manifests and JSON Output

- [x] JSON output for command execution
- [x] JSON file output via `--json-file`
- [x] Run manifest model exists
- [x] Manifest writing path exists for run invocations
- [~] Manifest content is partial versus scope (config hash/artifacts/push decisions/warnings/errors detail)
- [ ] Stable versioned JSON schema contract documentation

## 12) Policy and Template Sources

- [x] Local file policy source class exists
- [ ] Policy source integration in runtime config load path
- [ ] Embedded policy templates
- [ ] Remote policy sources (HTTP/Git/NuGet/company registry)
- [ ] Policy caching/version pinning/trust model

## 13) Config Inspection and Explainability

- [x] `list` includes built-ins, config commands, aliases
- [x] `explain <command>` includes args/options/steps
- [x] `repo config resolved`
- [x] `repo config sources`
- [ ] `repo config materialize` (builtin:config-materialize exists; standalone CLI sub-command pending)
- [ ] `repo explain version`
- [~] Explain depth is basic (no full step graph/push eligibility/provider config details)

## 14) Safety and Governance

- [x] Push rule engine enforced in `builtin:push-artifacts` via `pushRulesJson`
- [x] Skip push on PR enforcement (noPushInPullRequest rule)
- [x] Require clean working tree enforcement (requireCleanWorkingTree rule)
- [ ] Secret masking/redaction in logs and outputs
- [ ] Structured error taxonomy with codes/details/suggested fixes

## 15) UI/TUI

- [x] Spectre.Console renderer exists
- [x] Basic `repo ui` command path
- [~] UI currently lists commands; interactive workflows are minimal
- [ ] Rich command picker and execution dashboard
- [ ] Config/policy/resolution browsing UI
- [ ] TUI project/features (future phase)

## 16) Testing and Quality Gates

- [x] Build passes (`dotnet build`)
- [x] Tests pass (33 total — added tests for config merge, parallel steps, config commands)
- [x] Added tests for template rendering behavior
- [x] Added tests for built-in command registration paths
- [~] Coverage breadth is still limited for config merge/policy/runtime edge cases
- [ ] Add tests for policy resolution + merge semantics
- [ ] Add tests for run manifest completeness
- [ ] Add integration tests for branch workflows and alias resolution edge cases

## 17) Documentation and Samples

- [x] `repo.json` example exists in repository root
- [x] Core docs exist (`ARCHITECTURE.md`, `CONFIGURATION.md`, `DEVELOPMENT.md`)
- [~] Docs do not yet fully document implemented JSON schema/version contract details
- [ ] Add explicit docs for unresolved features and current limitations
- [ ] Add docs for config inspection commands once implemented

## 18) MVP Completion Snapshot

Items expected in MVP (per scope section 56) and status:

- [x] .NET global tool structure
- [x] `repo.json` loading
- [x] Config commands / aliases / args / options
- [x] Sequential execution engine
- [x] Step types: run/command/uses
- [x] Basic templating
- [x] Version providers: fixed/env
- [x] Docker + NuGet artifact providers
- [x] Basic test command + analysis hook
- [x] `doctor`, `list`, `explain`
- [x] JSON output and `--json-file`
- [x] Run manifest (basic)
- [x] Local file extends/policies/merge (full merge pipeline implemented in RepoConfigurationLoader)
- [~] Basic config validation (deserialization-only today)

## 19) Next High-Impact Work (Recommended Order)

- [ ] Wire policy sources into runtime load path (start with local/embedded)
- [ ] Implement `config materialize` standalone CLI sub-command (builtin exists)
- [ ] Command-level parallel settings (`maxParallel`)
- [ ] Expand run manifest with config hash and full version fields
- [ ] Secret masking/redaction in logs and outputs
- [ ] Not-found command suggestion engine
- [ ] Add focused tests for merge/policy/parallel/manifest edge cases
