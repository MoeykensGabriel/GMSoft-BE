namespace GMSoft.Application.Common.Exceptions;

/// <summary>
/// Se lanza cuando falta autenticación o las credenciales son inválidas.
/// HTTP 401 Unauthorized.
/// </summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}
