# Local run and Swagger

The local loop is how an endpoint is proved end to end: `aspire start` brings up PostgreSQL, OpenBao, the
API and the Swagger sidecar; the sidecar fetches the API's OpenAPI document, merges the examples catalog and
serves Swagger UI; a profile from `users.json` mints a local JWT; and each example runs against the seeded
database.

This reference covers that loop and the examples every new endpoint must ship. For the document shape itself,
read `api.md`; for the AppHost topology and run modes, read `aspire.md`. The repository's own
`local/local-development.md` and `local/openapi/README.md` are the detailed local docs; when they differ from
this reference, the repository wins.

**Terms:** the API produces an **OpenAPI document** at `/openapi/v1.json`; **Swagger UI** is the local viewer
the sidecar serves. The examples catalog is not part of the API document: the sidecar merges it at request
time.

## What the loop proves

| Check | What it proves |
|---|---|
| The route is registered | The endpoint is reachable at the documented path and method. |
| The API's `/openapi/v1.json` contains the operation | `WithName`, `WithSummary`, `WithDescription`, `Produces` and `ProducesProblem` describe the contract. |
| The JSON context serializes the contracts | Request and response types are in `AppJsonSerializerContext`; no reflection fallback. |
| `{Feature}OpenApiSchemas` describes the schema | Descriptions, formats, max lengths, enums and examples match the feature, not the Todo sample. |
| The sidecar merges the catalog | `local/openapi/examples.json` matches the document and its `bodies/` and `responses/` files. |
| The example runs with a profile | The seed, the profile email and the policy produce the documented status and body. |

An endpoint that returns a value but is absent from the document or has no runnable example is not finished.

## Prerequisites

- .NET SDK 10, Aspire CLI 13.x and Docker or Podman.
- A trusted HTTPS development certificate: `dotnet dev-certs https --check --trust`.
- On Linux, export the certificate directory in the shell that runs Aspire:

  ```bash
  export SSL_CERT_DIR="$HOME/.aspnet/dev-certs/trust:/usr/lib/ssl/certs"
  ```

- The local JWT signing key, once per machine:

  ```bash
  dotnet local/tools/devtoken.cs init
  ```

  The key is stored in the AppHost user secrets, outside the repository. Never commit it or print it.

## Start the environment

```bash
dotnet local/tools/devtoken.cs init       # once per machine
aspire start                              # background; prints the dashboard URL
aspire ps --format Json                   # resource states + dashboardUrl
aspire logs swagger                       # catalog/document problems
aspire stop                               # stops and removes containers and local data
```

Wait until `postgres`, `db-init`, `openbao`, `openbao-init`, `api` and `swagger` are running or healthy.
`db-init` exits 0 only after the schema and the Local seed are applied; `openbao-init` exits 0 only after the
secrets are written.

| Thing | Where |
|---|---|
| API | `https://localhost:5001` (the launch profile's HTTPS endpoint) |
| OpenAPI document (API) | `https://localhost:5001/openapi/v1.json` |
| Swagger UI | the `swagger` resource in the dashboard; the sidecar serves `/swagger` |
| Merged document | the sidecar's `/openapi/v1.json` |
| Health | `/health`, `/alive`, `/health/api`, `/health/postgresDb` |

The dashboard URL contains a personal login token. Do not commit it, paste it into a report or share it.

The API serves the OpenAPI document at `/openapi/v1.json`; it does not serve Swagger UI. The only Swagger UI
is the local sidecar; it lives under `local/openapi/`, is not published, and the Component/Functional tests
never start it.

`dotnet local/tools/devtoken.cs get --profile user` prints the same HS256 token the sidecar mints, for `curl`
or a script. Use the profile picker in the browser and the CLI outside it; never commit the token.

## How the sidecar is wired in the AppHost

The API generates the document from its endpoint metadata; Aspire does not generate it. Aspire starts the
sidecar and passes the API URL; the sidecar merges the examples and serves the UI.

The AppHost adds the sidecar only in Local mode (inside `if (!isTest)`), after the API exists:

```csharp
builder.AddProject<Projects.{App}_OpenApiExamples>(Resources.Swagger, launchProfileName: null)
    .WithHttpEndpoint()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("API_BASE_URL", api.GetEndpoint("https"))
    .WithEnvironment("EXAMPLES_PATH",
        Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "../../openapi")))
    .WithEnvironment(LocalLogin.SigningKey, localJwt.SigningKey)
    .WithEnvironment(LocalLogin.Issuer, localJwt.Issuer)
    .WithEnvironment(LocalLogin.Audience, localJwt.AudienceFor(Resources.Api))
    .WithEnvironment(LocalLogin.RoleClaim,
        localJwt.Configuration[LocalAuth.RoleClaimKey(Resources.Api)] ?? "role")
    .WaitFor(api)
    .WithHttpHealthCheck("/health");
```

| Value | Meaning |
|---|---|
| `API_BASE_URL` | The API's HTTPS endpoint. The sidecar fetches `/openapi/v1.json` from it. |
| `EXAMPLES_PATH` | The `local/openapi` directory that contains `examples.json`, `bodies/` and `responses/`. |
| `LocalLogin.SigningKey` | The same text `openbao-init` writes as `API_KEY`; the sidecar signs the profile tokens. |
| `LocalLogin.Issuer` / `Audience` / `RoleClaim` | The values the API validates; they must match `devtoken.json`. |

`WaitFor(api)` prevents the sidecar from starting before the document exists. The health check is `/health`,
a sidecar endpoint that returns 200 without touching the API.

## What the sidecar does

1. Fetches `GET {API_BASE_URL}/openapi/v1.json`.
2. Merges the examples from `examples.json`: request bodies, parameter examples and response examples from
   `bodies/` and `responses/`.
3. Adds the `Bearer` security scheme and a local-explorer note to the document, and points `servers` at the
   sidecar so "Try it out" calls `/api/*` through the proxy (one origin, no CORS).
4. Serves Swagger UI at `/swagger` with a header profile picker built from `users.json`. Selecting a profile
   calls the internal `POST /local/auth/token/{profile}` (not an OpenAPI operation), mints an HS256 token with
   the same issuer/audience/role claim as `devtoken`, and applies it with `ui.preauthorizeApiKey`.
5. Proxies `/api/{**path}` to the API, passing the `Authorization` header untouched.
6. Re-reads the catalog and the document on every request; `users.json` is read at startup. Nothing is cached
   or persisted.

If the catalog references an operation, response or parameter that the document does not have, the merged
document returns `500` with the list of problems and the same list appears in `aspire logs swagger`.

## Document every new endpoint (mandatory)

A new endpoint ships its API contract and its local example in the same change. This is a non-negotiable in
`SKILL.md`, not a documentation follow-up.

### API document

- Unique `WithName` (the operation id), `WithSummary`, `WithDescription`.
- `Produces<T>(status)` for every success shape and `ProducesProblem(status)` for every error the endpoint
  returns, including the ones added by `CreateAuthorizedRouteGroupBuilder`.
- Request, response, collection and scalar types in `AppJsonSerializerContext`.
- Schema descriptors in `{Feature}OpenApiSchemas`; the feature tag in `OpenApiConfiguration`.
- `OpenApiContractTests` includes the new operation ids and status codes.

A missing entry fails the contract test; do not weaken the test to make it pass.

### Local example

- An entry in `local/openapi/examples.json` keyed by `METHOD /path` for every new operation.
- A request body example for every `POST`/`PUT`/`PATCH`.
- A `parameters` entry for every route or query parameter the caller must fill.
- A success response example, and an error example when the error is part of what the endpoint demonstrates
  (for example `409` on a duplicate name).
- Values that match `local/database/seed/*.sql` and the profiles in `local/openapi/users.json`.
- The example must run after `aspire start` with the profile named in the example.

The catalog contract and troubleshooting are below.

## Catalog format

`local/openapi/examples.json`:

```json
{
  "operations": {
    "POST /api/Project/filter": {
      "requestBody": {
        "examples": {
          "user": {
            "summary": "Projects of the local user",
            "description": "Matches the two seeded projects owned by local.user@example.com.",
            "body": "bodies/filter-projects-user.json"
          },
          "admin": {
            "summary": "Projects of several users",
            "description": "Matches every seeded project.",
            "body": "bodies/filter-projects-admin.json"
          }
        }
      },
      "responses": {
        "200": {
          "examples": {
            "user": {
              "summary": "Two projects owned by the user",
              "body": "responses/projects-user.json"
            },
            "admin": {
              "summary": "All seeded projects",
              "body": "responses/projects.json"
            }
          }
        }
      }
    },
    "POST /api/Project": {
      "requestBody": {
        "examples": {
          "new-project": {
            "summary": "Create a project",
            "description": "Returns 201 once. Edit the name to create another one, or restart Aspire to rebuild the seed.",
            "body": "bodies/create-project.json"
          }
        }
      },
      "responses": {
        "201": {
          "examples": {
            "created": {
              "summary": "The stored project",
              "body": "responses/project-created.json"
            }
          }
        },
        "409": {
          "examples": {
            "duplicate": {
              "summary": "The name already exists for this owner",
              "value": {
                "title": "Conflict",
                "status": 409,
                "detail": "A project with this name already exists for this owner."
              }
            }
          }
        }
      }
    },
    "GET /api/Project/{id}": {
      "parameters": [
        {
          "name": "id",
          "in": "path",
          "value": "01HWXHRB80000G000000000001"
        }
      ],
      "responses": {
        "200": {
          "examples": {
            "seeded": {
              "summary": "A project owned by the local user",
              "body": "responses/project-user.json"
            }
          }
        }
      }
    }
  }
}
```

The complete `Project` catalog, including the body and response files, is in
`../assets/examples/local/openapi-examples.md`.

### Rules

- The root is `operations`; each key is `METHOD /path` with a space, for example `GET /api/Project/{id}`.
- Use `{id}` in the key, not `{id:ulid}`. The sidecar normalizes route constraints and trailing slashes
  before matching.
- `requestBody.examples` and `responses.<status>.examples` accept `summary`, `description` and **one of**
  `body` or `value`. Using both is an error.
- `body` is a path relative to `local/openapi`; it must stay inside that directory. `value` is inline JSON,
  useful for a short parameter or a `ProblemDetails` example.
- `parameters` is a **JSON array**. Each entry needs `name` and `value`; `in` is optional unless the same name
  exists in more than one location. Never use an object keyed by parameter name.
- The default content type is `application/json`. Use `contentType` inside `requestBody` or a response to pick
  another one.
- Body files are plain JSON, not templates.
- Responses without an explicit example get a generic `ProblemDetails` example from the sidecar. Add an
  explicit example only when the error is part of the endpoint's contract.
- `204 No Content` has no content; do not add a response example for it. The operation metadata is the
  contract.
- The sidecar re-reads `examples.json` on every request: refresh Swagger after an edit. `users.json` is read at
  startup: restart the `swagger` resource after changing it.
- Edit the catalog against the current document. A `500` from the sidecar lists the unmatched operation,
  response or parameter; the same list is in `aspire logs swagger`.

## Example matrix

At minimum:

| Operation | Request body | Parameter | Success response | Profiles |
|---|---|---|---|---|
| `POST /api/{Feature}` (create) | Yes | — | `201` + body | The owning role; add `admin` if the result differs |
| `POST /api/{Feature}/filter` | Yes | — | `200` + body | `user`; `admin` when `createdBys` crosses owners |
| `GET /api/{Feature}/{id}` | — | `id` from the seed | `200` + body | `user`; `admin` for another owner's row |
| `PUT`/`PATCH /api/{Feature}/{id}` | Yes | `id` from the seed | `200` + body | The policy's role; `admin` when allowed |
| `DELETE /api/{Feature}/{id}` | — | `id` from the seed | `200`/`204` | The narrowest policy that admits the role |

- One example per distinct state or role the endpoint is supposed to demonstrate. Do not duplicate the seed
  in the catalog; reference the seeded ids, emails and statuses.
- Use `user`, `operator`, `admin`, `external` and `no-role` from `users.json` deliberately: `external` and
  `no-role` exercise `403` on `User`/`Operator`/`Admin` routes; no profile is for `401`.
- Create is not idempotent: a `201` example returns `409` on the second run. Edit the unique name or restart
  Aspire to rebuild the seed.
- A created id is illustrative; the next create returns a different ULID. To exercise `GET` by id, use the id
  of a seeded row returned by the filter.
- Add an error example only when the status is a documented contract the caller must handle, not for every
  `400`/`500`.

## Verify in Swagger

1. `dotnet local/tools/devtoken.cs init` (once), then `aspire start`.
2. Wait for `api` and `swagger`; if `api` is unhealthy, query `/health/api` and `/health/postgresDb`.
3. Open the `swagger` resource from the dashboard. Do not open the API's port looking for a UI.
4. Select the profile named in the example.
5. Execute every new operation with its example; assert the status and the body against the documented
   contract.
6. Check `aspire logs swagger` for catalog problems.
7. After editing `examples.json`, refresh the UI. After editing `users.json`, restart the `swagger` resource.
8. If Docker or Aspire is unavailable, run the sidecar unit tests (they need no containers); if Docker is
   available, also run `OpenApiContractTests`. Report exactly which local checks could not run. Never claim a
   local check that did not run.

   ```bash
   dotnet test --project local/openapi/{App}.OpenApiExamples.UnitTests/{App}.OpenApiExamples.UnitTests.csproj
   ```

## Troubleshooting

| Symptom | Fix |
|---|---|
| `The local JWT signing key is missing` | `dotnet local/tools/devtoken.cs init`, then start again. |
| `'API_KEY' must be at least 32 UTF-8 bytes` | `dotnet local/tools/devtoken.cs init --rotate`. |
| `db-init` exits 1 | Read `aspire logs db-init`; a SQL script failed, the directory is empty, or the readiness query references a removed table. |
| `openbao-init` exits 1 | Read `aspire logs openbao-init`; a required `OpenBao__*` value or the secret is missing. |
| TLS error / `SSL certificate problem` | `dotnet dev-certs https --trust`; on Linux export `SSL_CERT_DIR`. |
| Sidecar: `Could not read the OpenAPI document` / `502`/`504` | The API is not ready, timed out, or `OpenApi:Enabled` is false; wait for the API and refresh. |
| Sidecar: `500` with problems | `examples.json` references an operation, response or parameter the document does not have; fix the catalog and read `aspire logs swagger`. |
| Swagger returns `401` | The profile token expired or the key rotated; select the profile again or press "New token". |
| Swagger returns `403` | The profile lacks the policy's role; select a role the policy admits. |
| Sidecar: `Unknown profile` | `users.json` changed while the sidecar was running; restart the `swagger` resource. |
| `swagger` resource is missing | The AppHost is in a test mode; the sidecar is added only in Local mode. |
| Sidecar has no healthy state | Read `aspire logs swagger`; a missing `API_BASE_URL`, `EXAMPLES_PATH` or `LocalLogin.*` value fails startup. |

## Done

- [ ] The new operation appears in the API's `/openapi/v1.json` with its operation id, tag, summary,
      description, success and error status codes, and schema.
- [ ] `OpenApiContractTests` includes the new operation ids and status codes.
- [ ] `local/openapi/examples.json` has an entry for every new operation, with a body and/or parameters and a
      success response; `parameters` uses the array form.
- [ ] The example values match the seed and `users.json`.
- [ ] `aspire start` + the `swagger` resource loads the merged document without problems, and every new
      example runs with its documented profile.
- [ ] If the local check could not run, the report names the reason and the automated checks that ran instead.

## Related references

- `api.md` — OpenAPI document shape, schema descriptors, JSON context and endpoint metadata.
- `aspire.md` — AppHost topology, modes, `db-init`, `openbao-init`, `devtoken` and dashboard.
- `feature-slice.md` — the ordered touch list, including the local environment step.
- `security.md` — local profiles, policies and the authorization matrix.
- `native-aot.md` — why the API uses `Microsoft.AspNetCore.OpenApi`, not Swashbuckle.
- `../assets/examples/local/openapi-examples.md` — a complete `Project` catalog with bodies and responses.
