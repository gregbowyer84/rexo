# Terraform Artifact Provider (`type: "terraform"`)

## Command mapping

- Build: `terraform init` then `terraform plan -out=plan.tfplan`
- Push: optional `terraform workspace select` then `terraform apply plan.tfplan`

## Settings

| Key | Type | Notes |
| --- | --- | --- |
| `directory` | `string` | Directory containing `.tf` files (default `.`). |
| `target.workspace` | `string` | Workspace selected before apply. |
| `target.workspaceEnv` | `string` | Env var name containing workspace (default env key `TERRAFORM_TARGET_WORKSPACE`). |
| `target.varFile` | `string` | `.tfvars` file passed to plan. |
| `target.varFileEnv` | `string` | Env var name containing `.tfvars` path (default env key `TERRAFORM_TARGET_VAR_FILE`). |
| `useDocker` | `boolean` | Docker fallback toggle (default `true`). |
| `dockerImage` | `string` | Fallback image override (default `hashicorp/terraform:1.9`). |
| `extra-build-args` | `string` | Additional args appended to `terraform plan`. |
| `extra-push-args` | `string` | Additional args appended to `terraform apply`. |

## Auth model

This provider does not have a dedicated feed auth resolver. Terraform backend/provider auth is expected to come from normal Terraform environment variables and credentials files used in your environment.

Target value resolution order:

1. Env value from `settings.target.workspaceEnv` / `settings.target.varFileEnv` (or defaults)
2. `settings.target.workspace` / `settings.target.varFile`

## Example

```json
{
  "type": "terraform",
  "name": "infra",
  "settings": {
    "directory": "infra",
    "target": {
      "workspace": "prod",
      "workspaceEnv": "TERRAFORM_TARGET_WORKSPACE",
      "varFile": "prod.tfvars",
      "varFileEnv": "TERRAFORM_TARGET_VAR_FILE"
    },
    "extra-build-args": "-lock-timeout=60s"
  }
}
```
