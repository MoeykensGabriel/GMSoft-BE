namespace GMSoft.Application.Common.Exceptions;

/// <summary>
/// Se lanza cuando el request es inválido por una regla de negocio que no
/// cubre FluentValidation. HTTP 400 Bad Request.
/// </summary>
public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message) { }
}
