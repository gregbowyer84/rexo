# repoOS Implementation TODO

Last updated: 2026-05-02

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
- [x] Validation: full JSON Schema validation via NJsonSchema (`ValidateSchemaAsync`)
- [x] Resolve `extends` from local file paths (breadth-first merge, circular detection)
- [~] Config merge order pipeline (defaults -> policies -> repo -> overlays -> CLI)
- [x] Environment overlays (REXO_OVERLAY env var)
- [x] Merge strategy customization for arrays/objects
- [x] Alternative config file names (`repo.yaml`, `.repo/repo.json`, `.repo/repo.yaml`)

## 3) Command Resolution Order

- [x] Built-in command match
- [x] Exact config command match
- [x] Config alias match
- [x] Policy-provided command match (loads policy.json alongside repo.json)
- [x] Not-found suggestions (Levenshtein distance suggestion engine with structured error code)

## 4) Execution Engine

- [x] Sequential step execution
- [x] Step types: `run`, `uses`, `command`
- [x] `when` condition evaluation (truthy/falsey rendering)
- [x] Continue-on-error support (`continueOnError`)
- [x] Step output propagation into execution context
- [x] `parallel` step groups (consecutive parallel steps batched via Task.WhenAll)
- [x] Output capture via `outputPattern` (regex named groups) and `outputFile` (write stdout to file)
- [x] Command-level parallel settings (`parallel`, `maxParallel`) — SemaphoreSlim concurrency cap
- [ ] Advanced dependency and fan-in behavior for parallel groups

## 5) Templating

- [x] Variable replacement
- [x] Context references: `args`, `options`, `repo`, `version`, `steps`, `env`
- [x] Basic filters: `slug`, `default(...)`, `upper`, `lower`
- [~] Simple conditions via truthy rendered values
- [x] Expression operators/comparisons in templates (`==` and `!=` equality operators)
- [x] Path helper functions: `basename`, `dirname`, `filestem`, `fileext`, `urlencode`, `sha256`, `trim`, `replace(old,new)`, `truncate(n)`, `first(n)`

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
- [x] Basic git provider (`git` key — parses most recent git tag as SemVer)
- [~] Version contract fields from scope are partially present
- [x] Full output contract fields (build metadata, branch, assembly/file/nuget/docker versions, informationalVersion, commitsSinceVersionSource)

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
- [x] TRX and coverage output support (TRX file parsing via `TryParseTrxFiles`, Cobertura coverage thresholds)
- [x] Coverage thresholds enforcement (Cobertura XML parsed; lineCoverageThreshold checked)
- [x] Extended analysis toolchain (SARIF/security scanners)

## 10) CI and Git Context

- [x] Git context detection (branch, commit, short sha, remote, clean state)
- [x] CI detection for common providers (at least core providers)
- [x] CI context attached to execution context
- [x] CI metadata depth (run number, tag, buildUrl, actor, workflowName, runAttempt — all providers)

## 11) Manifests and JSON Output

- [x] JSON output for command execution
- [x] JSON file output via `--json-file`
- [x] Run manifest model exists
- [x] Manifest writing path exists for run invocations
- [~] Manifest content is partial versus scope (config hash/artifacts/push decisions/warnings/errors detail)
- [ ] Stable versioned JSON schema contract documentation

## 12) Policy and Template Sources

- [x] Local file policy source class exists
- [x] Policy source integration in runtime config load path (policy.json loaded alongside repo.json)
- [x] Embedded policy templates
- [ ] Remote policy sources (HTTP/Git/NuGet/company registry)
- [ ] Policy caching/version pinning/trust model

## 13) Config Inspection and Explainability

- [x] `list` includes built-ins, config commands, aliases
- [x] `explain <command>` includes args/options/steps
- [x] `repo config resolved`
- [x] `repo config sources`
- [x] `repo config materialize` (CLI sub-command + builtin:config-materialize)
- [x] `repo explain version`
- [x] Explain depth enhanced (step graph with flags, push eligibility, provider config details)

## 14) Safety and Governance

- [x] Push rule engine enforced in `builtin:push-artifacts` via `pushRulesJson`
- [x] Skip push on PR enforcement (noPushInPullRequest rule)
- [x] Require clean working tree enforcement (requireCleanWorkingTree rule)
- [x] Secret masking/redaction in logs and outputs (auto-masks env vars containing SECRET, TOKEN, PASSWORD, KEY, APIKEY)
- [x] Structured error taxonomy: `RexoError` record with `Code`/`Message`/`Detail`/`SuggestedFix`/`Source`; `ErrorCodes` constants (CFG/CMD/STP/VER/ART/POL/GIT)

## 15) UI/TUI

- [x] Spectre.Console renderer exists
- [x] Basic `repo ui` command path
- [x] Interactive command picker (Spectre.Console SelectionPrompt, `rx ui` or `rx` with no args)
- [x] Rich command picker with command descriptions and execution dashboard
- [ ] Config/policy/resolution browsing UI
- [ ] TUI project/features (future phase)

## 16) Testing and Quality Gates

- [x] Build passes (`dotnet build`)
- [x] Tests pass (52 total — added tests for secret masking, template expressions, versioning, builtin commands)
- [x] Added tests for template rendering behavior
- [x] Added tests for built-in command registration paths
- [~] Coverage breadth is still limited for config merge/policy/runtime edge cases
- [x] Add tests for run manifest completeness (ErrorTaxonomyAndManifestTests)
- [~] Add tests for policy resolution + merge semantics (partial — command registry tests cover policy load path)
- [x] Add integration tests for branch workflows and alias resolution edge cases (`AliasAndBranchWorkflowTests`)

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

- [x] Wire policy sources into runtime load path (policy.json/.yaml/.yml + .repo/ sub-folder candidates)
- [x] Implement `config materialize` standalone CLI sub-command
- [x] Command-level parallel settings (`maxParallel`)
- [x] Expand run manifest with config hash and full version fields
- [x] Secret masking/redaction in logs and outputs
- [x] Not-found command suggestion engine
- [x] Add focused tests for merge/policy/parallel/manifest edge cases (80 tests total)
