namespace GMSoft.Application.Common.Interfaces;

/// <summary>Comprueba si la base responde. Nunca lanza por un corte: devuelve false.</summary>
public interface IDatabaseProbe
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
}
