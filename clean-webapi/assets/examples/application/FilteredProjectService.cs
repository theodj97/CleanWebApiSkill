using {App}.Application.Dtos.Project;
using {App}.Application.Project.Abstractions;
using {App}.Application.Results;
using {App}.Application.Validation;
using {App}.Domain.Models.Entities;
using FluentValidation;

namespace {App}.Application.Project.Filtered;

public sealed class FilteredProjectService(IProjectRepository projectRepository,
                                           IEnumerable<IValidator<FilteredProjectInput>> validators)
    : ValidationService, IFilteredProjectService
{
    public Task<Result<IEnumerable<ProjectDto?>>> FilterAsync(FilteredProjectInput input, CancellationToken cancellationToken) =>
        ExecuteAsync(input, validators, async () =>
        {
            IReadOnlyList<ProjectEntity> projects = await projectRepository.SearchAsync(input.ToFilter(), cancellationToken);

            return Result<IEnumerable<ProjectDto?>>.Success(projects.Select(x => x.ToDto()));
        }, cancellationToken);
}
