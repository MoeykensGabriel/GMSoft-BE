using FluentValidation;
using MediatR;
using ValidationException = GMSoft.Application.Common.Exceptions.ValidationException;

namespace GMSoft.Application.Common.Behaviours;

/// <summary>
/// Pipeline de MediatR que intercepta todos los Commands/Queries antes de llegar al Handler.
/// Si hay validators registrados para el request, los ejecuta. Si alguno falla → ValidationException → HTTP 400.
/// No hace falta llamar a Validate() manualmente en ningún Handler.
/// </summary>
public class ValidationBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next(cancellationToken);
    }
}
