# Helm OCI Artifact Provider (`type: "helm-oci"`)

## Command mapping

- Build: `helm package`
- Push: `helm push <chart.tgz> oci://...`
- Optional pre-push auth: `helm registry login`

## Settings

| Key | Type | Notes |
| --- | --- | --- |
| `chart` | `string` | Chart name for archive resolution (default artifact name). |
| `chartPath` | `string` | Chart root path (default `chart`). |
| `output` | `string` | Package output directory (default `artifacts/charts`). |
| `target.oci` | `string` | Full destination (`oci://registry/repository`). |
| `target.ociEnv` | `string` | Env var name for OCI destination (default env key `HELM_OCI_TARGET`). |
| `target.registry` | `string` | Registry host used with `target.repository` if `target.oci` not set. |
| `target.registryEnv` | `string` | Env var name for registry host (default env key `HELM_OCI_TARGET_REGISTRY`). |
| `target.repository` | `string` | Repository path used with `target.registry` if `target.oci` not set. |
| `target.repositoryEnv` | `string` | Env var name for repository path (default env key `HELM_OCI_TARGET_REPOSITORY`). |
| `target.loginRegistry` | `string` | Optional override for `helm registry login`. |
| `target.loginRegistryEnv` | `string` | Env var name for login registry override (default env key `HELM_OCI_LOGIN_REGISTRY`). |
| `target.usernameEnv` | `string` | Env var name for username (default env key `HELM_REGISTRY_USERNAME`). |
| `target.passwordEnv` | `string` | Env var name for password/token (default env key `HELM_REGISTRY_PASSWORD`). |
| `useDocker` | `boolean` | Docker fallback toggle (default `true`). |
| `dockerImage` | `string` | Fallback image override (default `alpine/helm:3.17.3`). |

## Destination resolution

1. Use `settings.target.oci` when present (normalized to `oci://...`)
2. Else compose from resolved `settings.target.registry` + resolved `settings.target.repository`

Resolved values check environment first using the env var names in `settings.target.*Env` (or provider defaults).

## Auth resolution

Credential resolution order:

1. Env values from `settings.target.usernameEnv` + `settings.target.passwordEnv` (defaults `HELM_REGISTRY_USERNAME` + `HELM_REGISTRY_PASSWORD`)
2. `GITHUB_ACTOR` + `GITHUB_TOKEN` for `ghcr.io` when explicit creds are absent

Registry endpoint resolution:

1. `HELM_REGISTRY` (legacy compatibility fallback)
2. Env value from `settings.target.loginRegistryEnv` (or `HELM_OCI_LOGIN_REGISTRY`)
3. `settings.target.loginRegistry`
4. Resolved destination registry

## Example

```json
{
  "type": "helm-oci",
  "name": "my-chart",
  "settings": {
    "chartPath": "deploy/charts/my-chart",
    "target": {
      "registry": "ghcr.io",
      "repository": "org/charts",
      "registryEnv": "HELM_OCI_TARGET_REGISTRY",
      "repositoryEnv": "HELM_OCI_TARGET_REPOSITORY",
      "usernameEnv": "HELM_REGISTRY_USERNAME",
      "passwordEnv": "HELM_REGISTRY_PASSWORD"
    }
  }
}
```
