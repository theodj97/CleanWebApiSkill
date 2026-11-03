# API

The API is a Minimal API with one route group per feature. It is published as Native AOT, so every contract is
explicit: request delegates are source-generated, JSON metadata is source-generated, OpenAPI is shaped by static
descriptors.

## Route groups

A feature implements `IGroupMap`:

```csharp
public sealed class ProjectRoutes(ICurrentUser currentUser) : IGroupMap
{
    private const string RouteName = "Project";

    public void MapGroup(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder userGroup = app.CreateAuthorizedRouteGroupBuilder(RouteName, [Constants.USER_POLICY]);
        RouteGroupBuilder operatorGroup = app.CreateAuthorizedRouteGroupBuilder(RouteName, [Constants.OPERATOR_POLICY]);

        userGroup.MapGet("/{id}", async (string id,
                                         IGetProjectByIdService service,
                                         CancellationToken cancellationToken) =>
        {
            GetProjectByIdInput input = new() { Id = id, CreatedBy = currentUser.OwnerScope };
            Result<ProjectDto?> result = await service.GetAsync(input, cancellationToken);
            return result.ToResponse<ProjectDto?, ProjectResponse>();
        })
        .WithName("GetProjectById")
        .WithSummary("Get a project by id")
        .WithDescription("...")
        .Produces<ProjectResponse>((int)HttpStatusCode.OK)
        .Produces((int)HttpStatusCode.NoContent, typeof(void));
    }
}
```

`CreateAuthorizedRouteGroupBuilder` (in `Api/Common/Abstractions/EndpointRouteBuilderExtensions.cs`):

- maps `/api/{routeName}` and sets the OpenAPI tag;
- adds `400` and `500` ProblemDetails;
- adds `401` and `403` ProblemDetails and requires authorization;
- applies the supplied policy array, or the default policy when the array is empty.

Choose the narrowest policy that admits the intended roles: `User` (User, Operator, Admin), `Operator`
(Operator, Admin), `Admin` (Admin), `External` (External, Operator, Admin; no route uses it yet). Every policy
also requires an email claim (`ClaimTypes.Email` or `"email"`), so a valid token without email gets `403`.

## Endpoint delegate

- Parameters are typed: route/query/body contracts, services, `ICurrentUser`, `CancellationToken`.
- The route parameter `string id` is validated by the input validator (`ValidateUlid`), not by model binding.
- The body contract maps to the application input in the delegate: `request.ToInput(currentUser.Email)` or
  `request.ToInput(currentUser.OwnerScope)`.
- The service returns `Result<T>`; the delegate returns `result.ToResponse<...>()`.
- Do not catch business exceptions in the delegate; `GlobalExceptionHandler` handles unexpected ones.

## Result → HTTP

`ApiResultExtensions` is the only place that translates `Error` to HTTP:

| Result | HTTP |
|---|---|
| `Success(value)` with a value | `200 OK` |
| `Success(null)` or empty collection | `204 No Content` |
| `Success` from `ToCreatedResponse` | `201 Created` + `Location` |
| `BadRequestError` | `400` ProblemDetails |
| `DomainError` | `400` ProblemDetails |
| `UnauthorizedError` | `401` |
| `ForbiddenError` | `403` |
| `NotFoundError` | `404` ProblemDetails |
| `ConflictError` | `409` ProblemDetails |
| unexpected exception | `500` ProblemDetails |

Use `ToResponse<TDto, TResponse>()` for an object, `ToResponse<TDto, TResponse>()` for a collection
(the same overload resolves `IEnumerable<TDto>`), `ToResponse<T>()` for a scalar (for example `bool`), and
`ToCreatedResponse<TDto, TResponse>(dto => $"/api/Project/{dto!.Id}")` for a create.

`IsNoContent` treats `null` and an empty `IEnumerable` as no content; an empty string is a value.

## ProblemDetails

- `GlobalExceptionHandler` handles malformed JSON, bad requests and unexpected exceptions; it never leaks the
  exception message for a 500.
- `UseStatusCodePages` gives 404/405 and other status-only responses the same contract.
- `ProblemDetailsFactory` writes `application/problem+json` with `status`, `title`, `type`, `detail` and a
  `traceId` extension.
- New error types must be added to `ResolveError` and to the OpenAPI `ProducesProblem` metadata; otherwise the
  document lies.

## OpenAPI

- The API uses `Microsoft.AspNetCore.OpenApi` (`AddApiOpenApi`, `MapOpenApi`), never Swashbuckle SwaggerGen.
- `OpenApiConfiguration` sets the document title, version, description, tags, Bearer security and calls the
  schema descriptors.
- `{Feature}OpenApiSchemas.DescribeSchemas(document)` patches the generated schemas: property descriptions,
  formats (`ulid`, `email`, `date-time`), max lengths, enums and examples. Use `OpenApiSchemaHelpers`.
- **Shared helper text is Todo-specific.** `DescribeSort` writes the Todo key list, `DescribeStatus` defaults to
  Todo statuses, and `CommonOpenApiSchemas` describes `KeyValuePairOfstringAndboolean` with Todo keys. For a
  second feature, pass a feature-specific description or override it after calling the helper (see
  `ProjectOpenApiSchemas`), or generalize the helper. A document that says `title` for a feature whose field is
  `name` is wrong even though the code compiles.
- Endpoint metadata: `WithName` (operation id), `WithSummary`, `WithDescription`, `Produces<T>(status)` and
  `ProducesProblem(status)`.
- Add a document transformer only if the descriptor cannot express the change; it must be static and
  reflection-free.
- `OpenApi:Enabled` is `true` in `appsettings.json` and `false` in `appsettings.PRODUCTION.json`; the local
  Swagger sidecar consumes `/openapi/v1.json`.
- `OpenApiContractTests` asserts the operation ids, status codes and schema constraints. Update it with the
  feature.

## JSON source generation

`Api/Configuration/AppJsonSerializerContext.cs` is the only JSON metadata source:

```csharp
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(CreateProjectRequest))]
[JsonSerializable(typeof(FilteredProjectRequest))]
[JsonSerializable(typeof(ProjectResponse))]
[JsonSerializable(typeof(IEnumerable<ProjectResponse>))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(ProblemDetails))]
internal partial class AppJsonSerializerContext : JsonSerializerContext;
```

- Add every request, response, collection and scalar the API serialises or deserialises.
- `ConfigureServices` inserts the context at the front of the resolver chain:
  `options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);`.
- Do not use `JsonSerializer.Serialize(object)` or the reflection-based overloads; `BannedSymbols.txt` blocks
  them.
- The local Swagger examples and the test response DTOs are separate types; they do not need entries unless the
  API serialises them.

## Authentication and authorization in the API

- `ConfigureServices.ConfigureAuth` registers the JWT bearer scheme and the four policies.
- `AuthenticationSettings.Load` selects `Symmetric` (HS256, local/tests) or `Oidc` (deployed) and validates
  the combination at startup: Symmetric requires `API_KEY` ≥ 32 UTF-8 bytes and an empty authority; Oidc
  requires an absolute HTTPS authority.
- `MapInboundClaims = true`, so `role` becomes `ClaimTypes.Role` and `email` becomes `ClaimTypes.Email`.
- The policies require both the role and an email claim. A token without email gets `403`, not `401`.
- The route group policy is the first authorization boundary; ownership inside the handler is the second.
  Non-admin callers pass `currentUser.OwnerScope` (their email) to the service; admins pass `null`.
- Never read `createdBy`/`owner` from the body for authorization; the JWT is the source.

## Validation and status codes

The input validator produces the `400` cases:

- required/empty/whitespace strings;
- max lengths from `{Feature}Constraints`;
- invalid email for owner/audit fields;
- invalid ULID for route ids;
- unknown status (`Enum.IsDefined`);
- invalid date format or a start date after the end date;
- paging: page number and page size must both be present and greater than 0;
- sort keys not in the allowed list, or duplicated keys.

The service produces the business cases:

- uniqueness → `409 Conflict` (pre-check and unique index);
- not found/not visible → `404` or `204` depending on the endpoint contract;
- update with no changes → `200` with the current row (do not treat as an error);
- delete with no matching row → `200` with `false`.

## Endpoint checklist

- [ ] Route group uses the narrowest policy and the feature tag.
- [ ] Every operation has `WithName`, `WithSummary`, `WithDescription`, success `Produces` and error
      `ProducesProblem`.
- [ ] Request → input mapping takes the owner from `ICurrentUser`.
- [ ] Service returns `Result<T>`; delegate uses `ToResponse`/`ToCreatedResponse`.
- [ ] New contracts are in `AppJsonSerializerContext`.
- [ ] New schemas are described in `{Feature}OpenApiSchemas`.
- [ ] New error types are mapped in `ApiResultExtensions` and documented.
- [ ] Component tests cover 200/201/204/400/401/403/404/409 as applicable.
- [ ] `OpenApiContractTests` includes the new operation ids and status codes.
- [ ] README API table updated.
