namespace GMSoft.Domain.Enums;

/// <summary>Desde dónde abrió la app el chofer al entrar.</summary>
public enum AccessClient
{
    Unknown = 0,

    /// <summary>Una pestaña común del navegador.</summary>
    Browser = 1,

    /// <summary>El ícono instalado en el celular ("Instalar app").</summary>
    InstalledApp = 2,

    /// <summary>
    /// El navegador de adentro de otra app (WhatsApp, Facebook, Instagram) o una vista web.
    /// Suele perder la sesión porque no guarda los datos como el navegador.
    /// </summary>
    EmbeddedBrowser = 3
}
