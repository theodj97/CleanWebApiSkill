using {App}.Application.Dtos.Project;
using {App}.Application.Project.Abstractions;
using {App}.Application.Results;
using {App}.Application.Validation;
using {App}.Domain.Models.Entities;
using {App}.Domain.Models.Enums.Project;
using FluentValidation;

namespace {App}.Application.Project.Create;

public sealed class CreateProjectService(IProjectRepository projectRepository,
                                         IEnumerable<IValidator<CreateProjectInput>> validators)
    : ValidationService, ICreateProjectService
{
    public Task<Result<ProjectDto?>> CreateAsync(CreateProjectInput input, CancellationToken cancellationToken) =>
        ExecuteAsync(input, validators, async () =>
        {
            ProjectEntity project = input.ToEntity();

            DateTime now = DateTime.UtcNow;
            project.CreatedAt = now;
            project.UpdatedAt = now;
            project.Status = (int)EProjectStatus.Draft;

            // Uniqueness is a business rule, not input validation, so the service owns it and
            // reports a conflict. The unique index still protects the race below.
            if (await projectRepository.NameExistsAsync(project.Name,
                                                        project.CreatedBy,
                                                        cancellationToken: cancellationToken))
            {
                return Result<ProjectDto?>.Failure(
                    new ConflictError("A project with this name already exists for this owner."));
            }

            Result<ProjectEntity> insertion = await projectRepository.InsertAsync(project, cancellationToken);

            return insertion.IsFailure
                ? Result<ProjectDto?>.Failure(insertion.Error!)
                : Result<ProjectDto?>.Success(insertion.Value!.ToDto());
        }, cancellationToken);
}
