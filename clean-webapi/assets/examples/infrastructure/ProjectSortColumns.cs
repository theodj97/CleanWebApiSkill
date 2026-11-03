using {App}.Domain.Models.Entities;

namespace {App}.Infrastructure.Repositories.Project.Helpers;

/// <summary>
/// Explicit (reflection-free, Native AOT friendly) whitelist of the properties that can be used to
/// sort Project queries, mapped to their physical column names. Only columns in this map can reach the
/// ORDER BY clause. Keep it in sync with the valid sort properties declared in the application-layer
/// filter validator.
/// </summary>
internal static class ProjectSortColumns
{
    public static readonly IReadOnlyDictionary<string, string> Map =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(ProjectEntity.Id)] = ProjectTable.IdColumn,
            [nameof(ProjectEntity.Name)] = ProjectTable.NameColumn,
            [nameof(ProjectEntity.Description)] = ProjectTable.DescriptionColumn,
            [nameof(ProjectEntity.CreatedAt)] = ProjectTable.CreatedAtColumn,
            [nameof(ProjectEntity.UpdatedAt)] = ProjectTable.UpdatedAtColumn,
            [nameof(ProjectEntity.Status)] = ProjectTable.StatusColumn,
            [nameof(ProjectEntity.CreatedBy)] = ProjectTable.CreatedByColumn,
            [nameof(ProjectEntity.UpdatedBy)] = ProjectTable.UpdatedByColumn,
        };
}
