using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace GMSoft.Data.Context;

/// <summary>
/// Deja en el log toda consulta que tarde más que el umbral (Database:SlowQueryMs,
/// 500 ms si no se configura). Sin esto, si producción se pone lenta no hay forma de
/// saber qué consulta fue. Se anota el SQL con sus parámetros como @p0, nunca los
/// valores: ahí van nombres, teléfonos y direcciones de clientes.
/// </summary>
public sealed class SlowQueryInterceptor(ILogger<SlowQueryInterceptor> logger, TimeSpan umbral)
    : DbCommandInterceptor
{
    private const int MaxSqlLength = 2000;

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        Anotar(command, eventData.Duration);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        Anotar(command, eventData.Duration);
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        Anotar(command, eventData.Duration);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        int result, CancellationToken cancellationToken = default)
    {
        Anotar(command, eventData.Duration);
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        Anotar(command, eventData.Duration);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        object? result, CancellationToken cancellationToken = default)
    {
        Anotar(command, eventData.Duration);
        return ValueTask.FromResult(result);
    }

    private void Anotar(DbCommand command, TimeSpan duracion)
    {
        if (duracion < umbral) return;
        var sql = command.CommandText.Length > MaxSqlLength
            ? command.CommandText[..MaxSqlLength] + " …"
            : command.CommandText;
        logger.LogWarning("Consulta lenta: {DurationMs} ms. {Sql}", (long)duracion.TotalMilliseconds, sql);
    }
}
