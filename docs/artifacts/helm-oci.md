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
| `oci` | `string` | Full destination (`oci://registry/repository`). |
| `registry` | `string` | Registry host used with `repository` if `oci` not set. |
| `repository` | `string` | Repository path used with `registry` if `oci` not set. |
| `loginRegistry` | `string` | Optional override for `helm registry login`. |
| `useDocker` | `boolean` | Docker fallback toggle (default `true`). |
| `dockerImage` | `string` | Fallback image override (default `alpine/helm:3.17.3`). |

## Destination resolution

1. Use `settings.oci` when present (normalized to `oci://...`)
2. Else compose from `settings.registry` + `settings.repository`

## Auth resolution

Credential resolution order:

1. `HELM_REGISTRY_USERNAME` + `HELM_REGISTRY_PASSWORD`
2. `GITHUB_ACTOR` + `GITHUB_TOKEN` for `ghcr.io` when explicit creds are absent

Registry endpoint resolution:

1. `HELM_REGISTRY`
2. `settings.loginRegistry`
3. `settings.registry`

## Example

```json
{
  "type": "helm-oci",
  "name": "my-chart",
  "settings": {
    "chartPath": "deploy/charts/my-chart",
    "registry": "ghcr.io",
    "repository": "org/charts"
  }
}
```
