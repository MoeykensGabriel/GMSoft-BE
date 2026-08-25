namespace GMSoft.Domain.Enums;

/// <summary>
/// Cómo se sigue el envase de un producto. El seguimiento es híbrido: se elige
/// por producto según cuánto cuesta perderlo.
/// </summary>
public enum ContainerTracking
{
    /// <summary>No vuelve. Se vende y se termina ahí.</summary>
    None = 0,

    /// <summary>Se sigue por saldo por cliente. Bidones y sifones.</summary>
    ByBalance = 1,

    /// <summary>Se sigue unidad por unidad, cada una con su número. Dispensers.</summary>
    ByUnit = 2
}
