# RubyGems Artifact Provider (`type: "rubygems"`)

## Command mapping

- Build: `gem build`
- Push: `gem push`

## Settings

| Key | Type | Notes |
| --- | --- | --- |
| `gemspec` | `string` | Gemspec path/glob (default `*.gemspec`). |
| `source` | `string` | Gem push source URL. |
| `gem-pattern` | `string` | Built gem glob for push (default `*.gem`). |
| `directory` | `string` | Working directory (default repo root). |
| `apiKeyEnv` | `string` | Env var containing API key (default `GEM_HOST_API_KEY`). |
| `useDocker` | `boolean` | Docker fallback toggle (default `true`). |
| `dockerImage` | `string` | Fallback image override (default `ruby:3-alpine`). |
| `extra-build-args` | `string` | Additional args appended to `gem build`. |
| `extra-push-args` | `string` | Additional args appended to `gem push`. |

## Auth resolution

Credential resolution order:

1. `settings.apiKeyEnv` (or `GEM_HOST_API_KEY`)
2. `RUBYGEMS_API_KEY`

The resolved value is injected as `GEM_HOST_API_KEY` for the push command.

## Example

```json
{
  "type": "rubygems",
  "name": "my-gem",
  "settings": {
    "directory": "src/ruby",
    "gemspec": "my-gem.gemspec",
    "source": "https://rubygems.org",
    "extra-push-args": "--host https://rubygems.org"
  }
}
```
