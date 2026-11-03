using System.Globalization;
using {App}.Application.Dtos.Project;
using {App}.Application.Project.Abstractions;
using {App}.Application.Results;
using {App}.Application.Validation;
using {App}.Domain.Models.Entities;
using FluentValidation;

namespace {App}.Application.Project.GetById;

public sealed class GetProjectByIdService(IProjectRepository projectRepository,
                                          IEnumerable<IValidator<GetProjectByIdInput>> validators)
    : ValidationService, IGetProjectByIdService
{
    public Task<Result<ProjectDto?>> GetAsync(GetProjectByIdInput input, CancellationToken cancellationToken) =>
        ExecuteAsync(input, validators, async () =>
        {
            ProjectEntity? project = await projectRepository.GetByIdAsync(
                Ulid.Parse(input.Id, CultureInfo.InvariantCulture),
                input.CreatedBy,
                cancellationToken);

            return Result<ProjectDto?>.Success(project?.ToDto());
        }, cancellationToken);
}
