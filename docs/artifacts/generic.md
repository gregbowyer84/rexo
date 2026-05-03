# Generic Artifact Provider (`generic` shape)

Use this shape for custom provider types that are not explicitly modeled in the schema.

## Shape

```json
{
  "type": "<custom-provider-type>",
  "name": "optional-name",
  "settings": {
    "...": "provider-specific settings"
  }
}
```

## Notes

- `type` must not be one of built-in modeled providers (`docker`, `nuget`, `helm-oci`, `npm`, `pypi`, `maven`, `gradle`, `rubygems`, `terraform`, `helm`, `docker-compose`).
- `settings` is an arbitrary map and is interpreted by the custom provider implementation.
- Keep provider contracts and validation rules documented in the custom provider project/repo.
