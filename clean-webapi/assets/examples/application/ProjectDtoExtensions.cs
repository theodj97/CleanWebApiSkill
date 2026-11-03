using {App}.Domain.Models.Entities;

namespace {App}.Application.Dtos.Project;

/// <summary>Entity to DTO mapping. It lives in Application because Application exposes DTOs.</summary>
internal static class ProjectDtoExtensions
{
    internal static ProjectDto ToDto(this ProjectEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Status = entity.Status,
            CreatedBy = entity.CreatedBy,
            UpdatedBy = entity.UpdatedBy
        };
    }

    /// <summary>Maps a projection without filling unrelated DTO fields.</summary>
    internal static ProjectDto ToNameDto(this ProjectEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new()
        {
            Id = entity.Id,
            Name = entity.Name
        };
    }
}
