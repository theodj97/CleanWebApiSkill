using FluentValidation;

namespace {App}.Application.Project.GetById;

public class GetProjectByIdInputValidator : ProjectValidator<GetProjectByIdInput>
{
    public GetProjectByIdInputValidator()
    {
        RuleFor(x => x.Id)
            .Custom(NotNullNotEmpty)
            .Custom(ValidateUlid);
    }
}
