# RubyGems Artifact Provider (`type: "rubygems"`)

## Command mapping

- Build: `gem build`
- Push: `gem push`

## Settings

| Key | Type | Notes |
| --- | --- | --- |
| `gemspec` | `string` | Gemspec path/glob (default `*.gemspec`). |
| `target.source` | `string` | Gem push source URL. |
| `target.sourceEnv` | `string` | Env var name containing source URL (default env key `RUBYGEMS_TARGET_SOURCE`). |
| `target.apiKeyEnv` | `string` | Env var name containing API key/token (default env key `GEM_HOST_API_KEY`). |
| `gem-pattern` | `string` | Built gem glob for push (default `*.gem`). |
| `directory` | `string` | Working directory (default repo root). |
| `useDocker` | `boolean` | Docker fallback toggle (default `true`). |
| `dockerImage` | `string` | Fallback image override (default `ruby:3-alpine`). |
| `extra-build-args` | `string` | Additional args appended to `gem build`. |
| `extra-push-args` | `string` | Additional args appended to `gem push`. |

## Auth resolution

Credential resolution order:

1. `settings.target.apiKeyEnv` (or `GEM_HOST_API_KEY`)
2. `RUBYGEMS_API_KEY`

The resolved value is injected as `GEM_HOST_API_KEY` for the push command.

Source resolution order:

1. Env value from `settings.target.sourceEnv` (or `RUBYGEMS_TARGET_SOURCE`)
2. `settings.target.source`

## Example

```json
{
  "type": "rubygems",
  "name": "my-gem",
  "settings": {
    "directory": "src/ruby",
    "gemspec": "my-gem.gemspec",
    "target": {
      "source": "https://rubygems.org",
      "sourceEnv": "RUBYGEMS_TARGET_SOURCE",
      "apiKeyEnv": "GEM_HOST_API_KEY"
    },
    "extra-push-args": "--host https://rubygems.org"
  }
}
```
