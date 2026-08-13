namespace GMSoft.Application.Common.Exceptions;

/// <summary>
/// Se lanza cuando la operación choca con el estado actual del recurso
/// (duplicados, transiciones de estado inválidas). HTTP 409 Conflict.
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
