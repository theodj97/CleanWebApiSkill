using {App}.Application.Dtos.Project;
using {App}.Application.Results;

namespace {App}.Application.Project.GetById;

public interface IGetProjectByIdService
{
    public Task<Result<ProjectDto?>> GetAsync(GetProjectByIdInput input, CancellationToken cancellationToken);
}
