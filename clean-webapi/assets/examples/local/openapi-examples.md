# Local OpenAPI examples (Project)

This is the `local/openapi` layer for the `Project` worked example. Copy the catalog and the body/response
files into `local/openapi/`; the sidecar reads `examples.json` and the document on every request, and reads
`users.json` at startup.

The ids, emails and statuses match `database/seed/0002__projects.sql`. The catalog must stay consistent with
the seed and with `local/openapi/users.json`; do not invent a second fixture.

## Seed rows

| Project | ULID | Owner | Status |
|---|---|---|---|
| Release 1.0 | `01HWXHRB80000G000000000001` | `local.user@example.com` | `2` Active |
| Write the runbook | `01HWXHRB80000G000000000002` | `local.user@example.com` | `1` Draft |
| Migrate the database | `01HWXHRB80000G000000000003` | `local.operator@example.com` | `3` Completed |
| Quarterly audit | `01HWXHRB80000G000000000004` | `local.admin@example.com` | `4` Archived |

## `examples.json`

```json
{
  "operations": {
    "POST /api/Project/filter": {
      "requestBody": {
        "examples": {
          "user": {
            "summary": "Projects of the local user",
            "description": "Matches Release 1.0 and Write the runbook. Authorize with the user profile.",
            "body": "bodies/filter-projects-user.json"
          },
          "admin": {
            "summary": "Projects of several users",
            "description": "Matches every seeded project. Authorize with the admin profile.",
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
          },
          "duplicate-name": {
            "summary": "Create a project with an existing name",
            "description": "Use the user profile: Release 1.0 already exists for local.user@example.com. Returns 409.",
            "body": "bodies/create-project-duplicate.json"
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

## `bodies/filter-projects-user.json`

```json
{
  "sortProperties": [
    { "key": "createdAt", "value": true }
  ],
  "pageNumber": 1,
  "pageSize": 10
}
```

## `bodies/filter-projects-admin.json`

```json
{
  "createdBys": [
    "local.user@example.com",
    "local.operator@example.com",
    "local.admin@example.com"
  ],
  "sortProperties": [
    { "key": "createdAt", "value": true }
  ]
}
```

## `bodies/create-project.json`

```json
{
  "name": "Created from the local Swagger examples",
  "description": "Example request against the Aspire seed."
}
```

## `bodies/create-project-duplicate.json`

```json
{
  "name": "Release 1.0",
  "description": "This name already exists for local.user@example.com."
}
```

## `responses/project-user.json`

```json
{
  "id": "01HWXHRB80000G000000000001",
  "name": "Release 1.0",
  "description": "Ship the first public release.",
  "createdAt": "2026-10-20T09:00:00Z",
  "status": 2,
  "createdBy": "local.user@example.com",
  "updatedBy": "local.user@example.com"
}
```

## `responses/projects-user.json`

```json
[
  {
    "id": "01HWXHRB80000G000000000001",
    "name": "Release 1.0",
    "description": "Ship the first public release.",
    "createdAt": "2026-10-20T09:00:00Z",
    "status": 2,
    "createdBy": "local.user@example.com",
    "updatedBy": "local.user@example.com"
  },
  {
    "id": "01HWXHRB80000G000000000002",
    "name": "Write the runbook",
    "description": "Document the local environment and the deployment steps.",
    "createdAt": "2026-10-21T10:15:00Z",
    "status": 1,
    "createdBy": "local.user@example.com",
    "updatedBy": "local.user@example.com"
  }
]
```

## `responses/projects.json`

```json
[
  {
    "id": "01HWXHRB80000G000000000001",
    "name": "Release 1.0",
    "description": "Ship the first public release.",
    "createdAt": "2026-10-20T09:00:00Z",
    "status": 2,
    "createdBy": "local.user@example.com",
    "updatedBy": "local.user@example.com"
  },
  {
    "id": "01HWXHRB80000G000000000002",
    "name": "Write the runbook",
    "description": "Document the local environment and the deployment steps.",
    "createdAt": "2026-10-21T10:15:00Z",
    "status": 1,
    "createdBy": "local.user@example.com",
    "updatedBy": "local.user@example.com"
  },
  {
    "id": "01HWXHRB80000G000000000003",
    "name": "Migrate the database",
    "description": "Apply the new schema to every environment.",
    "createdAt": "2026-10-18T08:00:00Z",
    "status": 3,
    "createdBy": "local.operator@example.com",
    "updatedBy": "local.operator@example.com"
  },
  {
    "id": "01HWXHRB80000G000000000004",
    "name": "Quarterly audit",
    "description": "Archive the findings and the remediation plan.",
    "createdAt": "2026-10-01T07:45:00Z",
    "status": 4,
    "createdBy": "local.admin@example.com",
    "updatedBy": "local.admin@example.com"
  }
]
```

## `responses/project-created.json`

```json
{
  "id": "01HWXHRB80000G000000000005",
  "name": "Created from the local Swagger examples",
  "description": "Example request against the Aspire seed.",
  "createdAt": "2026-10-28T09:30:00Z",
  "status": 1,
  "createdBy": "local.user@example.com",
  "updatedBy": "local.user@example.com"
}
```

The id in `responses/project-created.json` is illustrative: a create generates a new ULID, so the response id
changes on every call. To make the "GET by id" example callable against the seed, use the id of a seeded row
returned by the filter operation.

## `users.json` profiles

The seed emails map to the profiles the sidecar mints:

```json
{
  "users": {
    "user": {
      "name": "local.user",
      "email": "local.user@example.com",
      "roles": ["User"],
      "subject": "0b7c2f4e-0000-4000-8000-000000000001"
    },
    "operator": {
      "name": "local.operator",
      "email": "local.operator@example.com",
      "roles": ["Operator"],
      "subject": "0b7c2f4e-0000-4000-8000-000000000002"
    },
    "admin": {
      "name": "local.admin",
      "email": "local.admin@example.com",
      "roles": ["Admin"],
      "subject": "0b7c2f4e-0000-4000-8000-000000000003"
    }
  }
}
```

The template's full `users.json` also has `external` and `no-role` for `403` cases. The emails and roles must
match `local/tools/devtoken.json`.

## Variants

- `parameters` is always an array. Each entry needs `name` and `value`; `in` is optional unless the same name
  exists in more than one location.
- Use `body` for a file under `local/openapi`; use `value` for inline JSON. Do not use both in the same
  example.
- The default content type is `application/json`. Add `contentType` inside `requestBody` or a response when
  the operation uses another one.
- A `204 No Content` response has no content; do not add a response example for it. `GET` by id returns
  `204` when the id is missing or not visible; the `200` example uses a seeded row.
- Responses without an explicit example get a generic `ProblemDetails` example from the sidecar.

## Checklist

- [ ] Every operation of the feature has a `METHOD /path` entry.
- [ ] Every write operation has a request body example; every route parameter has a `parameters` entry; every
      operation has a success response example.
- [ ] `parameters` uses the array form, never an object keyed by name.
- [ ] Ids, emails and statuses match `database/seed/0002__projects.sql`; profiles match `users.json`.
- [ ] A create example says it is not idempotent (edit the name or restart Aspire).
- [ ] The sidecar merges the catalog without `500`/problems; refresh Swagger after an edit and read
      `aspire logs swagger` if it fails.
