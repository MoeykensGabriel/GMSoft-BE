using FluentValidation;

namespace GMSoft.Application.Features.Drivers.Update;

public class UpdateDriverCommandValidator : AbstractValidator<UpdateDriverCommand>
{
    public UpdateDriverCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DocumentNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
    }
}
