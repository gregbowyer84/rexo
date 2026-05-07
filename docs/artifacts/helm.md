# Helm Artifact Provider (`type: "helm"`)

This provider is for non-OCI Helm publishing (Chart Museum style or filesystem destination).

## Command mapping

- Build: `helm package`
- Push:

1. `helm cm-push` when resolved `settings.target.repository` is an HTTP(S) URL
2. Copy `.tgz` files to destination when resolved `settings.target.repository` is a filesystem path

## Settings

| Key | Type | Notes |
| --- | --- | --- |
| `chart` | `string` | Compatibility alias for chart directory path. |
| `chart-directory` | `string` | Chart directory path (default `.`). |
| `output-directory` | `string` | Output folder for packaged charts (default repo root). |
| `target.repository` | `string` | Chart Museum URL or filesystem path. |
| `target.repositoryEnv` | `string` | Env var name containing repository URL/path (default env key `HELM_TARGET_REPOSITORY`). |
| `target.usernameEnv` | `string` | Env var name containing username (default env key `HELM_REPO_USERNAME`). |
| `target.passwordEnv` | `string` | Env var name containing password/token (default env key `HELM_REPO_PASSWORD`). |
| `useDocker` | `boolean` | Docker fallback toggle (default `true`). |
| `dockerImage` | `string` | Fallback image override (default `alpine/helm:3.17.3`). |
| `extra-build-args` | `string` | Additional args appended to `helm package`. |
| `extra-push-args` | `string` | Additional args appended to `helm cm-push`. |

## Auth resolution (Chart Museum URL mode)

Credential resolution order:

1. Env values from `settings.target.usernameEnv` + `settings.target.passwordEnv` (defaults `HELM_REPO_USERNAME` + `HELM_REPO_PASSWORD`)
2. No credentials

Repository resolution order:

1. Env value from `settings.target.repositoryEnv` (or `HELM_TARGET_REPOSITORY`)
2. `settings.target.repository`

Legacy compatibility fallback: `HELM_REPO_URL` can still override an HTTP endpoint.

Credentials are passed on the command line as `--username` and `--password` for `helm cm-push`.

## Example

```json
{
  "type": "helm",
  "name": "my-chart",
  "settings": {
    "chart-directory": "deploy/chart",
    "output-directory": "artifacts/charts",
    "target": {
      "repository": "https://charts.example.com",
      "repositoryEnv": "HELM_TARGET_REPOSITORY",
      "usernameEnv": "HELM_REPO_USERNAME",
      "passwordEnv": "HELM_REPO_PASSWORD"
    },
    "extra-push-args": "--context-path=/"
  }
}
```
