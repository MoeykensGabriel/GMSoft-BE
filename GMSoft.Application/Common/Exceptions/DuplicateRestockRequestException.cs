namespace GMSoft.Application.Common.Exceptions;

/// <summary>Otra solicitud ya guardo esta tanda. Se resuelve leyendo la original.</summary>
public class DuplicateRestockRequestException(Exception innerException)
    : Exception("La recarga ya fue registrada.", innerException);
