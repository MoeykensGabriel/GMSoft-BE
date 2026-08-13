namespace GMSoft.Application.Common.Exceptions;

/// <summary>
/// Se lanza cuando una entidad buscada no existe o fue soft-deleted.
/// HTTP 404 Not Found.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"'{entityName}' with key '{key}' was not found.") { }
}
