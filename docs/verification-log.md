# Verification log

Maintainer-only. Not loaded by agents.

## 2026-11-03 — Local Swagger contract and mandatory endpoint examples

**Method:** compared the skill with `CleanWebApiTemplate` `HEAD` (`local/local-development.md`,
`local/openapi/README.md`, `local/aspire/{App}.AppHost/AppHost.cs`, `OpenApiExamplesMerger.cs` and
`local/openapi/Program.cs`). Added `references/local-swagger.md`, made the endpoint/document/example contract
a non-negotiable, wired it into Phases 5 and 6, the review checklist, the quality gates, the glossary, the
`Project` local example and the evals.

**Findings that shaped the change:**

1. The skill described the sidecar but not the local loop: start, dashboard, `swagger` resource, profile
   picker, execution and verification.
2. The catalog contract was incomplete: `parameters` is a JSON array, not an object; `body` and `value` are
   mutually exclusive; the sidecar re-reads the catalog on every request and returns `500` with problems when
   an entry does not match the document.
3. The `Project` example used the object form for `parameters`, which the merger rejects. It now uses the
   array form and includes the `409` `value` example.
4. The previous verification log recorded that no manual `aspire start` + Swagger walkthrough had been run;
   the new reference and eval #8 make that check explicit and require reporting when it cannot run.

**Verification:** JSON syntax of `evals.json` and every fenced catalog snippet checked with a JSON parser.
`dotnet test --project local/openapi/CleanWebApiTemplate.OpenApiExamples.UnitTests/CleanWebApiTemplate.OpenApiExamples.UnitTests.csproj`
passed (33/33) against the template. A manual `aspire start` + Swagger UI walkthrough was not run: the local
HTTPS development certificate is not trusted in this environment, and the check would need `dotnet dev-certs
https --trust` (a machine-level change).

## 2026-11-03 — Architecture refresh of the skill

**Method:** aligned the skill with `CleanWebApiTemplate` `HEAD` after the architecture review applied
`ValidatorSet<T>`, `OwnerScope`, `{Feature}SortCatalog`, projection types, `ProblemDetailsFactory` as the only
problem-details builder, and the `{Feature}Endpoints` adapter. Updated `SKILL.md`, all affected references, the
`Project` example, the evals and the registration snippets.

**Results (template repository):** build 0 warnings; 324 tests passed; Native AOT publish 0 warnings;
coverage 98.1% line / 96.2% branch; `npm run verify` passed.

**Pattern changes the skill now teaches:** a missing validator set fails at startup; sort keys live in one
Application catalog and one Infrastructure column map with an alignment test; `OwnerScope` is a value type with
`VisibleOwner`/`OwnerForCreate`/`FilterOwners`; partial reads use a projection type instead of `!` casts; the
route group keeps metadata and delegates plumbing to `{Feature}Endpoints`.

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
  `../clean-webapi/references/quality-gates.md`; the individual build/publish/test gates above were run instead.
- A manual `aspire start` + dashboard/Swagger UI walkthrough. The component and functional suites start the same
  AppHost and were run end to end.
