using {App}.Application.Dtos.Project;
using {App}.Application.Results;

namespace {App}.Application.Project.Filtered;

public interface IFilteredProjectService
{
    public Task<Result<IEnumerable<ProjectDto?>>> FilterAsync(FilteredProjectInput input, CancellationToken cancellationToken);
}
