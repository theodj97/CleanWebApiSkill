using {App}.Application.Dtos.Project;
using {App}.Application.Results;

namespace {App}.Application.Project.Create;

public interface ICreateProjectService
{
    public Task<Result<ProjectDto?>> CreateAsync(CreateProjectInput input, CancellationToken cancellationToken);
}
