# Verification log

Maintainer-only. Not loaded by agents.

## 2026-11-03 — `Project` example injected into a clean template copy

**Method:** took a clean copy of `CleanWebApiTemplate` at `HEAD` (`git archive`), copied
`clean-webapi/assets/examples/` to the target project paths, replaced `{App}` with `CleanWebApiTemplate`, added
the explicit registrations, generalized the architecture helper condition, and updated the existing
`OpenApiContractTests` with the Project operations.

**Environment:** .NET SDK 10.0.1xx and Docker available. The component/functional fixtures started the AppHost
through `Aspire.Hosting.Testing`; the Aspire CLI was not exercised in this run.

### Results

| Gate | Result |
|---|---|
| `dotnet build CleanWebApiTemplate.slnx` | 0 warnings, 0 errors |
| `dotnet publish src/CleanWebApiTemplate.Api/... -c Release -r linux-x64 --self-contained` | success, 0 warnings |
| `dotnet test --project ...Api.UnitTests` | 155 passed |
| `dotnet test --project ...ArchitectureTests` | 9 passed |
| `dotnet test --project ...Api.ComponentTests` | 134 passed |
| `dotnet test --project ...Api.FunctionalTests` | 12 passed |

### Findings that shaped the skill

1. **Architecture helper condition is hardcoded.** `StructureTests` accepted only
   `Infrastructure.Repositories.Todo.Helpers`; adding `ProjectTable`/`ProjectSortColumns` under
   `Repositories.Project.Helpers` failed the test. The skill now says to generalize it to
   `.Contains(".Helpers")` in the same change.
2. **The OpenAPI contract test asserts the exact operation-id set.** Adding Project made
   `OpenApiContractTests.Document_ContainsEveryTodoOperation` fail until the Project ids and paths were added.
   The skill now names the file and the exact update.
3. **Shared OpenAPI helper text is Todo-specific.** `DescribeSort` and `CommonOpenApiSchemas` list `title` and
   the Todo statuses; `DescribeStatus` defaults to the Todo statuses. The example overrides them and the skill
   documents the trap.
4. **`db-init` readiness query is hardcoded to `Todo`.** Removing or renaming the sample without updating
   `local/aspire/{App}.DbInitializer/Program.cs` makes `db-init` exit 1 and the API wait forever.
5. **Test identities are named after the sample** (`TodoUser`, `TodoOperator`, ...). The example uses them and
   documents that they may be renamed if the sample is removed.

### Not run in this verification

- `npm run verify` and `npm run coverage` in the injected copy. The commands and thresholds are documented in
  `references/quality-gates.md`; the individual build/publish/test gates above were run instead.
- A manual `aspire start` + dashboard/Swagger UI walkthrough. The component and functional suites start the same
  AppHost and were run end to end.
