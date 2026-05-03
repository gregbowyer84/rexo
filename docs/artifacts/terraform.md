# Terraform Artifact Provider (`type: "terraform"`)

## Command mapping

- Build: `terraform init` then `terraform plan -out=plan.tfplan`
- Push: optional `terraform workspace select` then `terraform apply plan.tfplan`

## Settings

| Key | Type | Notes |
| --- | --- | --- |
| `directory` | `string` | Directory containing `.tf` files (default `.`). |
| `workspace` | `string` | Workspace selected before apply. |
| `vars-file` | `string` | `.tfvars` file passed to plan. |
| `var-file` | `string` | Alias for `vars-file`. |
| `useDocker` | `boolean` | Docker fallback toggle (default `true`). |
| `dockerImage` | `string` | Fallback image override (default `hashicorp/terraform:1.9`). |
| `extra-build-args` | `string` | Additional args appended to `terraform plan`. |
| `extra-push-args` | `string` | Additional args appended to `terraform apply`. |

## Auth model

This provider does not have a dedicated feed auth resolver. Terraform backend/provider auth is expected to come from normal Terraform environment variables and credentials files used in your environment.

## Example

```json
{
  "type": "terraform",
  "name": "infra",
  "settings": {
    "directory": "infra",
    "workspace": "prod",
    "vars-file": "prod.tfvars",
    "extra-build-args": "-lock-timeout=60s"
  }
}
```
