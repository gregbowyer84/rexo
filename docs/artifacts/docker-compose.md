# Docker Compose Artifact Provider (`type: "docker-compose"`)

## Command mapping

- Build: `docker compose build`
- Push: `docker compose push`

## Settings

| Key | Type | Notes |
| --- | --- | --- |
| `file` | `string` | Compose file path (default `docker-compose.yml`). |
| `project-name` | `string` | Compose project name (`-p`). |
| `services` | `string` | Space-separated service names (default all). |
| `registry` | `string` | Optional registry for pre-push `docker login`. |
| `extra-build-args` | `string` | Additional args appended to `docker compose build`. |
| `extra-push-args` | `string` | Additional args appended to `docker compose push`. |

## Auth resolution

When `settings.registry` is set, provider performs `docker login` before push.

Credential resolution uses shared Docker login rules:

1. `DOCKER_LOGIN_USERNAME` + `DOCKER_LOGIN_PASSWORD` (or aliases)
2. `GITHUB_ACTOR` + `GITHUB_TOKEN` for `ghcr.io` when explicit creds are absent

## Notes

- No `useDocker` or `dockerImage` settings: this provider is already Docker-based.
- If `registry` is omitted, push runs without pre-login.

## Example

```json
{
  "type": "docker-compose",
  "name": "stack",
  "settings": {
    "file": "deploy/compose.yml",
    "project-name": "my-stack",
    "services": "api worker",
    "registry": "ghcr.io",
    "extra-push-args": "--quiet"
  }
}
```
