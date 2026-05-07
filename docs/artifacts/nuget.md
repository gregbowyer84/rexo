# NuGet Artifact Provider (`type: "nuget"`)

## Command mapping

- Build: `dotnet pack`
- Push: `dotnet nuget push`

## Settings

| Key | Type | Notes |
| --- | --- | --- |
| `project` | `string` | Project path for `dotnet pack`. |
| `output` | `string` | Package output directory. |
| `target.source` | `string` | Feed URL/name for push. |
| `target.sourceEnv` | `string` | Env var name containing feed URL/name (default env key `NUGET_TARGET_SOURCE`). |
| `target.apiKeyEnv` | `string` | Env var name containing API key/token (default env key `NUGET_API_KEY`). |

## Auth resolution

Credential resolution order:

1. `settings.target.apiKeyEnv` (or `NUGET_API_KEY`)
2. `NUGET_AUTH_TOKEN`
3. `GITHUB_TOKEN` for `nuget.pkg.github.com`
4. `SYSTEM_ACCESSTOKEN` for non-GitHub CI fallback (for example Azure Artifacts)

Source resolution order:

1. Env value from `settings.target.sourceEnv` (or `NUGET_TARGET_SOURCE`)
2. `settings.target.source`
3. Default `https://api.nuget.org/v3/index.json`

## Example

```json
{
  "type": "nuget",
  "name": "Rexo.Core",
  "settings": {
    "project": "src/Core/Core.csproj",
    "output": "artifacts/packages",
    "target": {
      "source": "https://api.nuget.org/v3/index.json",
      "sourceEnv": "NUGET_TARGET_SOURCE",
      "apiKeyEnv": "NUGET_API_KEY"
    }
  }
}
```
