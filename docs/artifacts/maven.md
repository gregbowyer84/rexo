# Maven Artifact Provider (`type: "maven"`)

## Command mapping

- Build: `mvn package -DskipTests`
- Push: `mvn deploy -DskipTests`

## Settings

| Key | Type | Notes |
| --- | --- | --- |
| `project` | `string` | Path to `pom.xml` (default root `pom.xml`). |
| `profiles` | `string` | Comma-separated Maven profiles (mapped to `-P...`). |
| `useDocker` | `boolean` | Docker fallback toggle (default `true`). |
| `dockerImage` | `string` | Fallback image override (default `maven:3-eclipse-temurin-21`). |
| `extra-build-args` | `string` | Additional args appended to `mvn package`. |
| `extra-push-args` | `string` | Additional args appended to `mvn deploy`. |

## Auth resolution

Credential resolution order:

1. `MAVEN_REPO_USERNAME` + `MAVEN_REPO_PASSWORD`
2. `SYSTEM_ACCESSTOKEN` (Azure Artifacts CI fallback; username is `VssSessionToken`)

Credentials are passed as process environment variables; reference them from `settings.xml` using `${env.MAVEN_REPO_USERNAME}` and `${env.MAVEN_REPO_PASSWORD}`.

## Example

```json
{
  "type": "maven",
  "name": "service-java",
  "settings": {
    "project": "services/catalog/pom.xml",
    "profiles": "release",
    "extra-push-args": "-DaltDeploymentRepository=internal::default::https://repo.example.com/maven2"
  }
}
```
