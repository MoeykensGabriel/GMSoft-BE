using FluentValidation.Results;

namespace GMSoft.Application.Common.Exceptions;

/// <summary>
/// Se lanza cuando un Command falla las validaciones de FluentValidation.
/// HTTP 400 Bad Request con el detalle de cada campo.
/// </summary>
public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base("One or more validation errors occurred.")
    {
        Errors = failures
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());
    }
}
