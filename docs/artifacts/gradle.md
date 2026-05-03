# Gradle Artifact Provider (`type: "gradle"`)

## Command mapping

- Build: `<gradle executable> <tasks>`
- Push: `<gradle executable> publish`

`<gradle executable>` resolves to wrapper (`gradlew` / `gradlew.bat`) when enabled and present; otherwise `gradle`.

## Settings

| Key | Type | Notes |
| --- | --- | --- |
| `tasks` | `string` | Build tasks (default `build`). |
| `wrapper` | `boolean` | Use Gradle wrapper when present (default `true`). |
| `directory` | `string` | Working directory containing build files (default repo root). |
| `useDocker` | `boolean` | Docker fallback toggle (default `true`). |
| `dockerImage` | `string` | Fallback image override (default `gradle:8-jdk21`). |
| `extra-build-args` | `string` | Additional args appended to build task invocation. |
| `extra-push-args` | `string` | Additional args appended to `publish`. |

## Auth resolution

Credential resolution order:

1. `ORG_GRADLE_PROJECT_mavenUsername` + `ORG_GRADLE_PROJECT_mavenPassword`
2. `GRADLE_PUBLISH_KEY` + `GRADLE_PUBLISH_SECRET`

Resolved values are passed as environment variables to Gradle.

## Example

```json
{
  "type": "gradle",
  "name": "platform-java",
  "settings": {
    "directory": "services/platform",
    "tasks": "clean build",
    "extra-push-args": "--info"
  }
}
```
