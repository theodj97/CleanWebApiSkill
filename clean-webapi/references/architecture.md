# Architecture

## The five runtime projects

```text
src/
├── {App}.Api              Minimal API: routes, authentication, ProblemDetails, OpenAPI, JSON contracts, composition root
├── {App}.Application      Use cases, validators, Result<T>/Error, DTOs, repository ports, query filters
├── {App}.Domain           Entities, enums, constraints; no framework references
├── {App}.Infrastructure   Npgsql repositories, connection factory, OpenBao configuration source
└── {App}.ServiceDefaults  OpenTelemetry and base health checks, shared by every host

local/                     AppHost, DB initializer, OpenBao initializer, Swagger sidecar, devtoken
tests/                     Unit, component, functional and architecture tests, plus Tests.Shared
```

## Dependency rule

```text
Api ──────────► Application ──────────► Domain
 │                   ▲
 │                   │
 └──────────► Infrastructure ──────────► Domain
```

| Project | May reference | Must not reference |
|---|---|---|
| `Domain` | `System.*` only | Application, Infrastructure, Api, ServiceDefaults, ASP.NET Core |
| `Application` | Domain | Infrastructure, Api, ServiceDefaults, ASP.NET Core |
| `Infrastructure` | Application, Domain | Api, ServiceDefaults |
| `Api` | Application, Infrastructure, ServiceDefaults | — |
| `ServiceDefaults` | ASP.NET Core and OpenTelemetry packages | Api, Application, Infrastructure, Domain |
| `tests/*` | every runtime project and `local/` (the AppHost) | — |
| `local/*` | runtime projects | — |

The rule is enforced twice:

- `tests/{App}.ArchitectureTests/DependencyRulesTests.cs` (ArchUnitNET) checks namespaces and assembly
  references.
- `BannedSymbols.txt` + `Microsoft.CodeAnalysis.BannedApiAnalyzers` block reflection and reflection-based
  JSON.

When a change makes a new project or namespace, add the matching architecture test. When a change needs a
type from an outer layer, the design is wrong: move the abstraction inward or the implementation outward.

## Layer responsibilities

| Layer | Put here | Never here |
|---|---|---|
| **Domain** | `{Feature}Entity`, enums (`E{Feature}Status`), `{Feature}Constraints`, value objects, pure domain rules | DTOs, HTTP status codes, configuration, SQL, attributes from ASP.NET, `Result<T>` |
| **Application** | Inputs, validators, service interfaces and services, `Result<T>`/`Error`, DTOs, DTO mapping, repository ports, query filters | `HttpContext`, `IResult`, `IActionResult`, `Npgsql*`, connection strings, OpenBao, `ILogger` as a business dependency |
| **Infrastructure** | Repository implementations, SQL text, row mapping, connection factory, OpenBao configuration provider | Business rules, validation, DTO-to-response mapping, HTTP concepts |
| **Api** | Request/response contracts, `ToInput`, route groups, endpoint metadata, authentication/authorization, ProblemDetails, OpenAPI, JSON context, composition root | Business rules, SQL, direct `NpgsqlConnection` use, entity persistence |
| **ServiceDefaults** | OpenTelemetry, health check defaults, host-level cross-cutting setup | Feature code, domain logic |

## Request lifecycle

1. `Program.Main` creates a slim builder, loads `AppSettings`, calls `AddApiServices`, builds, configures the
   pipeline, maps routes and runs.
2. `ConfigureApplication` adds the exception handler, response compression, HTTPS redirection, status-code
   pages, CORS, authentication and authorization, then maps OpenAPI and the route groups.
3. A route group (`IGroupMap`) maps endpoints with a policy. The delegate receives typed parameters, including
   `CancellationToken` and `ICurrentUser`.
4. The request contract maps to an application input: `request.ToInput(currentUser.Email)` or
   `request.ToInput(currentUser.OwnerScope)`. The owner always comes from the token.
5. The application service calls `ValidationService.ExecuteAsync`, which runs every registered
   `IValidator<TInput>` through `ValidationRunner`. A validation failure becomes a `BadRequestError`; the
   action is skipped.
6. The service applies business rules, calls the repository port, and returns `Result<T>` with a `Dto` or an
   `Error`.
7. `ApiResultExtensions` maps the result: success to `200`/`204`/`201`, failure to `400`/`401`/`403`/`404`/
   `409` ProblemDetails with a `traceId`.
8. `GlobalExceptionHandler` catches unexpected exceptions and malformed JSON; `UseStatusCodePages` gives
   unmatched routes and methods the same ProblemDetails contract.

## Patterns and their reasons

### Result and Error

`Result<T>` and the `Error` hierarchy live in `Application/Results`, not Domain. A service returns
`Result<T>.Success(value)` or `Result<T>.Failure(new NotFoundError(...))`. The API translates each error type
to an HTTP status in `ApiResultExtensions.ResolveError`.

Why: business failures are expected outcomes, not exceptional control flow; the translation to HTTP belongs to
the API, so Application stays free of ASP.NET.

Rules:

- Add a new error type only when it maps to a new HTTP status. Extend `ResolveError` and the OpenAPI
  `ProducesProblem` metadata in the same change.
- Do not throw for validation, not-found or conflict. Throw only for programmer errors (null where the
  contract forbids it, impossible state).
- Repositories return `Result<T>` when a database constraint is a business outcome (duplicate title/name);
  otherwise `null` or `bool`.

### Validation

- Input shape: `AbstractValidator<TInput>` in Application, usually deriving from the feature's shared
  `{Feature}Validator<TInput>`.
- `BaseAbstractValidator<TRequest>` provides `NotNullNotEmpty`, `ValidateUlid`, `ValidateDateTime`,
  `ValidateStartDateAndEndDate`, `ValidateSortBy`, `IsValidEmail` and `AddFailure`.
- `ValidationService.ExecuteAsync` runs every registered validator and returns the first failure as
  `BadRequestError`.
- Uniqueness, ownership and state transitions are **business rules** in the service, because they need the
  repository and can become `ConflictError`/`NotFoundError`.

Why no validation decorators or MediatR: the template keeps the call path explicit and AOT-safe. A service
inherits `ValidationService`; there is no pipeline, no assembly scan and no dynamic dispatch.

### Repository

- The port (`I{Feature}Repository`) and the query filter (`{Feature}Filter`) live in Application.
- The adapter (`{Feature}Repository`) lives in Infrastructure, uses `INpgsqlConnectionFactory` and plain
  `NpgsqlCommand`/`NpgsqlDataReader`.
- Every row is mapped manually (`Map{Feature}`), in the exact column order of the shared `{Feature}Columns`
  constant.
- A connection is created, opened and disposed per operation; Npgsql pools underneath.
- There is no EF Core, no Dapper, no expression trees, no reflection mapping. This is deliberate: Native AOT
  and predictable SQL.

### DTO and response mapping

- Entity → DTO: `Application/Dtos/{Feature}/{Feature}DtoExtensions.ToDto`, internal to Application.
- DTO → response: `Api/Models/Responses/{Feature}/{Feature}Response.ToResponseModel`, through
  `IBaseResponse<TDto, TResponse>`.
- The API never returns an entity or a DTO directly; the response type is the public contract and the OpenAPI
  schema.

### Composition root

`Api/ConfigureServices.cs` is the only place that knows concrete Application and Infrastructure types. It
registers validators, services and repositories one by one. `Api/Extensions/EndpointExtension.cs` registers
each `IGroupMap` with `TryAddEnumerable`. There is no assembly scanning, because the AOT analyzer must see
every type statically.

### Current user

`ICurrentUser` (Application) exposes `Email`, `IsAdmin` and `OwnerScope`. `HttpContextCurrentUser` (Api) reads
the claims from `IHttpContextAccessor`. `OwnerScope` is `null` for admins and the email for everyone else.
Application code uses `OwnerScope` for repository filters; it never reads claims or `HttpContext`.

### Configuration

`AppSettings` is bound manually in `AppConfigurationHelper.LoadAppSettings` (no reflection binder). A section
class derives from `SectionBase` and its name must end in `Section` (`ConnectionStringsSection` →
`ConnectionStrings`). `AppSettingsValidator` fails startup with a clear message.

### Secrets

`AppSettings` holds no secret. OpenBao is a configuration source added by
`Infrastructure/Secrets/OpenBaoConfigurationExtensions.AddOpenBaoSecrets`; the API reads `API_KEY` from it in
Symmetric mode. The AppHost seeds the store with `openbao-init`; the application only receives `OpenBao:*`
(address, token, mount, path), never the secret values as environment variables.

### Native AOT

The API declares `PublishAot`, `IsAotCompatible`, `EnableAotAnalyzer`, `EnableRequestDelegateGenerator` and
`GenerateDocumentationFile`. JSON goes through `AppJsonSerializerContext`; OpenAPI is shaped by static
descriptors; request delegates are source-generated. See `native-aot.md`.

## Where does this code go?

| You are adding... | Layer | Folder |
|---|---|---|
| A new field with validation | Domain (constraint/entity) + Application (input/validator) + API (request/response) + SQL | `Domain/Models`, `Application/{Feature}/{Verb}`, `Api/Routes/{Feature}/{Verb}` |
| A new business rule that needs the database | Application service | `Application/{Feature}/{Verb}/{Verb}{Feature}Service.cs` |
| A new SQL query | Application port + Infrastructure repository | `Application/{Feature}/Abstractions`, `Infrastructure/Repositories/{Feature}` |
| A new endpoint over an existing service | API only | `Api/Routes/{Feature}` |
| A new status code | Application `Error` + API `ResolveError` + OpenAPI metadata | `Application/Results/Error.cs`, `Api/Extensions/ApiResultExtensions.cs` |
| A new external service | Infrastructure adapter + Application port | `Infrastructure/{Concern}`, `Application/{Feature}/Abstractions` |
| A new configuration value | API `AppSettings` + validator + appsettings + AppHost | `Api/Configuration`, `local/aspire` |
| A new secret | OpenBao initializer + AppHost + docs | `local/aspire`, `local/local-development.md` |
| A new database table | SQL script + Infrastructure repository + tests | `local/database`, `Infrastructure/Repositories` |
| A new cross-cutting host concern | ServiceDefaults or API pipeline | `src/{App}.ServiceDefaults`, `Api/ConfigureApplication.cs` |

## Common mistakes

- Putting `Result<T>` or `HttpStatusCode` in Domain.
- Referencing `Microsoft.AspNetCore.*` from Application to reuse `IResult`.
- Using EF Core, Dapper or a mapper library because it is faster to write.
- Registering services with assembly scanning or `AddValidatorsFromAssembly`.
- Reading `HttpContext` or claims in Application.
- Accepting `createdBy`/`owner` from the request body.
- Building SQL with string interpolation of user input.
- Adding a sort key only to the repository or only to the validator.
- Adding repository helpers for a second feature without generalizing the hardcoded `Todo.Helpers` condition in
  `StructureTests`; the architecture test fails even though the layout is correct.
- Forgetting `AppJsonSerializerContext` (the endpoint compiles but fails at runtime under AOT).
- Forgetting the OpenAPI schema descriptor (the document loses descriptions and formats).
- Suppressing an AOT warning instead of fixing the cause.
- Adding a package without checking its AOT/trimming annotations.
