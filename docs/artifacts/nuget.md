# NuGet Artifact Provider (`type: "nuget"`)

## Command mapping

- Build: `dotnet pack`
- Push: `dotnet nuget push`

## Settings

| Key | Type | Notes |
| --- | --- | --- |
| `project` | `string` | Project path for `dotnet pack`. |
| `output` | `string` | Package output directory. |
| `source` | `string` | Feed URL/name for push. |
| `apiKeyEnv` | `string` | Env var containing API key (default `NUGET_API_KEY`). |

## Auth resolution

Credential resolution order:

1. `settings.apiKeyEnv` (or `NUGET_API_KEY`)
2. `NUGET_AUTH_TOKEN`
3. `GITHUB_TOKEN` for `nuget.pkg.github.com`
4. `SYSTEM_ACCESSTOKEN` for non-GitHub CI fallback (for example Azure Artifacts)

## Example

```json
{
  "type": "nuget",
  "name": "Rexo.Core",
  "settings": {
    "project": "src/Core/Core.csproj",
    "output": "artifacts/packages",
    "source": "https://api.nuget.org/v3/index.json"
  }
}
```
