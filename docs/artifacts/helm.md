# Helm Artifact Provider (`type: "helm"`)

This provider is for non-OCI Helm publishing (Chart Museum style or filesystem destination).

## Command mapping

- Build: `helm package`
- Push:
1. `helm cm-push` when `settings.repository` is an HTTP(S) URL
2. Copy `.tgz` files to destination when `settings.repository` is a filesystem path

## Settings

| Key | Type | Notes |
| --- | --- | --- |
| `chart` | `string` | Compatibility alias for chart directory path. |
| `chart-directory` | `string` | Chart directory path (default `.`). |
| `output-directory` | `string` | Output folder for packaged charts (default repo root). |
| `repository` | `string` | Chart Museum URL or filesystem path. |
| `useDocker` | `boolean` | Docker fallback toggle (default `true`). |
| `dockerImage` | `string` | Fallback image override (default `alpine/helm:3.17.3`). |
| `extra-build-args` | `string` | Additional args appended to `helm package`. |
| `extra-push-args` | `string` | Additional args appended to `helm cm-push`. |

## Auth resolution (Chart Museum URL mode)

Credential resolution order:

1. `HELM_REPO_USERNAME` + `HELM_REPO_PASSWORD`
2. No credentials

Optional endpoint override: `HELM_REPO_URL`.

Credentials are passed on the command line as `--username` and `--password` for `helm cm-push`.

## Example

```json
{
  "type": "helm",
  "name": "my-chart",
  "settings": {
    "chart-directory": "deploy/chart",
    "output-directory": "artifacts/charts",
    "repository": "https://charts.example.com",
    "extra-push-args": "--context-path=/"
  }
}
```
