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
| `target.registry` | `string` | Optional registry for pre-push `docker login`. |
| `target.registryEnv` | `string` | Env var name containing target registry (default env key `DOCKER_COMPOSE_TARGET_REGISTRY`). |
| `target.usernameEnv` | `string` | Env var name containing Docker username (default env key `DOCKER_LOGIN_USERNAME`). |
| `target.passwordEnv` | `string` | Env var name containing Docker password/token (default env key `DOCKER_LOGIN_PASSWORD`). |
| `extra-build-args` | `string` | Additional args appended to `docker compose build`. |
| `extra-push-args` | `string` | Additional args appended to `docker compose push`. |

## Auth resolution

When resolved target registry is set, provider performs `docker login` before push.

Credential resolution uses shared Docker login rules:

1. Env values from `settings.target.usernameEnv` + `settings.target.passwordEnv` (defaults `DOCKER_LOGIN_USERNAME` + `DOCKER_LOGIN_PASSWORD`)
2. `GITHUB_ACTOR` + `GITHUB_TOKEN` for `ghcr.io` when explicit creds are absent

Registry resolution order:

1. Env value from `settings.target.registryEnv` (or `DOCKER_COMPOSE_TARGET_REGISTRY`)
2. `settings.target.registry`

## Notes

- No `useDocker` or `dockerImage` settings: this provider is already Docker-based.
- If `target.registry` is omitted, push runs without pre-login.

## Example

```json
{
  "type": "docker-compose",
  "name": "stack",
  "settings": {
    "file": "deploy/compose.yml",
    "project-name": "my-stack",
    "services": "api worker",
    "target": {
      "registry": "ghcr.io",
      "registryEnv": "DOCKER_COMPOSE_TARGET_REGISTRY",
      "usernameEnv": "DOCKER_LOGIN_USERNAME",
      "passwordEnv": "DOCKER_LOGIN_PASSWORD"
    },
    "extra-push-args": "--quiet"
  }
}
```
