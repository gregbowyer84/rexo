# npm Artifact Provider (`type: "npm"`)

## Command mapping

- Build: `npm pack`
- Push: `npm publish`

## Settings

| Key | Type | Notes |
| --- | --- | --- |
| `directory` | `string` | Working directory containing `package.json` (default repo root). |
| `registry` | `string` | Registry URL (default npmjs). |
| `access` | `public` \| `restricted` | Publish access level. |
| `tag` | `string` | Dist-tag for publish (default resolved version). |
| `tokenEnv` | `string` | Env var containing auth token (default `NPM_TOKEN`). |
| `useDocker` | `boolean` | Docker fallback toggle (default `true`). |
| `dockerImage` | `string` | Fallback image override (default `node:lts-alpine`). |
| `extra-build-args` | `string` | Additional args appended to `npm pack`. |
| `extra-push-args` | `string` | Additional args appended to `npm publish`. |

## Auth resolution

Token resolution order:

1. `settings.tokenEnv` (or `NPM_TOKEN` when not set)
2. `NODE_AUTH_TOKEN`
3. `GITHUB_TOKEN` when `settings.registry` targets `npm.pkg.github.com`

The resolved token is passed to the publish process as `NPM_TOKEN`.

## Example

```json
{
  "type": "npm",
  "name": "web-sdk",
  "settings": {
    "directory": "src/sdk",
    "registry": "https://npm.pkg.github.com",
    "access": "restricted",
    "tag": "next",
    "extra-push-args": "--provenance"
  }
}
```
