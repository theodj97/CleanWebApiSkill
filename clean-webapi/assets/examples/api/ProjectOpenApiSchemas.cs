using Microsoft.OpenApi;
using static {App}.Api.Configuration.OpenApiSchemaHelpers;

namespace {App}.Api.Configuration;

/// <summary>Schema descriptions for the Project contracts.</summary>
internal static class ProjectOpenApiSchemas
{
    public static void DescribeSchemas(OpenApiDocument document)
    {
        if (document.Components?.Schemas is not { } schemas)
            return;

        Patch(schemas, "CreateProjectRequest", schema =>
        {
            Describe(Property(schema, "name"),
                     "Project name. Trimmed; unique per owner.",
                     maxLength: 120,
                     example: "Release 1.0");
            Describe(Property(schema, "description"),
                     "Optional details for the project.",
                     maxLength: 1000,
                     example: "Ship the first public release.");
        });

        Patch(schemas, "FilteredProjectRequest", schema =>
        {
            Describe(ArrayItems(Property(schema, "ids")),
                     "Project ids to include.",
                     format: "ulid",
                     example: "01HWXHRB80000G000000000001");
            Describe(ArrayItems(Property(schema, "names")),
                     "Name fragments to match (contains).",
                     maxLength: 120,
                     example: "release");
            DescribeStatus(ArrayItems(Property(schema, "statuses")),
                           "Statuses to include: 1 Draft, 2 Active, 3 Completed, 4 Archived.",
                           withAllowedValues: true);
            Describe(ArrayItems(Property(schema, "createdBys")),
                     "Owner emails. Admin only; other roles are always scoped to their own email.",
                     format: "email",
                     example: "local.user@example.com");
            Describe(Property(schema, "startDate"),
                     "Inclusive lower bound for CreatedAt.",
                     format: "date-time",
                     example: "2026-01-01T00:00:00Z");
            Describe(Property(schema, "endDate"),
                     "Inclusive upper bound for CreatedAt.",
                     format: "date-time",
                     example: "2026-12-31T23:59:59Z");

            DescribePaging(schema);
            OpenApiSchema? sort = Property(schema, "sortProperties");
            DescribeSort(sort);
            if (sort is not null)
            {
                sort.Description =
                    "Sort keys and directions, applied in order. Supported keys: id, name, description, "
                    + "createdAt, updatedAt, status, createdBy, updatedBy.";
            }
        });

        Patch(schemas, "ProjectResponse", schema =>
        {
            Describe(Property(schema, "id"),
                     "Project identifier.",
                     format: "ulid",
                     example: "01HWXHRB80000G000000000001");
            Describe(Property(schema, "name"),
                     "Project name.",
                     maxLength: 120,
                     example: "Release 1.0");
            Describe(Property(schema, "description"),
                     "Project details.",
                     maxLength: 1000,
                     example: "Ship the first public release.");
            Describe(Property(schema, "createdAt"),
                     "Creation timestamp (UTC).",
                     format: "date-time",
                     example: "2026-10-26T09:15:00Z");
            DescribeStatus(Property(schema, "status"),
                           "Status: 1 Draft, 2 Active, 3 Completed, 4 Archived.",
                           withAllowedValues: true);
            Describe(Property(schema, "createdBy"),
                     "Owner email.",
                     format: "email",
                     example: "local.user@example.com");
            Describe(Property(schema, "updatedBy"),
                     "Email of the last writer.",
                     format: "email",
                     example: "local.user@example.com");
        });
    }
}
