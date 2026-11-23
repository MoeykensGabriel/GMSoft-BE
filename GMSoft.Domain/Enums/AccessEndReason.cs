namespace GMSoft.Domain.Enums;

/// <summary>Por qué terminó una sesión de la app.</summary>
public enum AccessEndReason
{
    /// <summary>El chofer tocó "Cerrar sesión".</summary>
    Manual = 1,

    /// <summary>La oficina recibió el camión y la app volvió al login.</summary>
    ReceptionCompleted = 2,

    /// <summary>La oficina anuló la salida.</summary>
    DepartureCancelled = 3,

    /// <summary>El acceso venció sin renovarse.</summary>
    Expired = 4,

    /// <summary>Cambió la contraseña o el usuario de la cuenta.</summary>
    CredentialsChanged = 5,

    /// <summary>La cuenta se desactivó.</summary>
    Deactivated = 6
}
