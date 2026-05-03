# PyPI Artifact Provider (`type: "pypi"`)

## Command mapping

- Build: `python -m build`
- Push: `python -m twine upload`

## Settings

| Key | Type | Notes |
| --- | --- | --- |
| `directory` | `string` | Working directory (default repo root). |
| `repository-url` | `string` | Twine repository URL. |
| `dist-dir` | `string` | Upload pattern (default `dist/*`). |
| `useDocker` | `boolean` | Docker fallback toggle (default `true`). |
| `dockerImage` | `string` | Fallback image override (default `python:3-slim`). |
| `extra-build-args` | `string` | Additional args appended to `python -m build`. |
| `extra-push-args` | `string` | Additional args appended to `python -m twine upload`. |

## Runtime notes

- Host execution attempts `python`, then `python3`, then Docker fallback.
- The default image may not include `build`/`twine`; use a custom image if needed.

## Auth resolution

Credential resolution order:

1. `TWINE_API_TOKEN` (username becomes `__token__`)
2. `TWINE_USERNAME` + `TWINE_PASSWORD`
3. `SYSTEM_ACCESSTOKEN` for Azure Artifacts URLs (`pkgs.dev.azure.com` / `.pkgs.visualstudio.com`)

Credentials are injected into the push process as `TWINE_USERNAME` and `TWINE_PASSWORD`.

## Example

```json
{
  "type": "pypi",
  "name": "data-utils",
  "settings": {
    "directory": "src/python",
    "repository-url": "https://upload.pypi.org/legacy/",
    "extra-build-args": "--sdist --wheel"
  }
}
```
