# Pull Request

<!--
Title format (required):
type(scope): summary

Example:
feat(cli): add parallel step execution
-->

## Summary

Describe the change and why it is needed.

## Checklist

- [ ] Tests added or updated
- [ ] Docs updated if behavior changed
- [ ] No inline package versions in project files
- [ ] Build and tests pass locally

## Validation

Include the exact commands used to validate your change:

```bash
dotnet build solution.slnx -c Release
dotnet test solution.slnx -c Release --no-build
```
