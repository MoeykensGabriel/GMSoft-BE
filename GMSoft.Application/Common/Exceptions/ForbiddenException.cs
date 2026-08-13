namespace GMSoft.Application.Common.Exceptions;

/// <summary>
/// Se lanza cuando el usuario está autenticado pero no tiene permiso
/// sobre el recurso. HTTP 403 Forbidden.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}
