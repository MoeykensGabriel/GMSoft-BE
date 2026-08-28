namespace GMSoft.Domain.Enums;

/// <summary>
/// Estado de un envase dentro del camion. El mismo producto ocupa dos saldos
/// distintos: sale con llenos y vuelve con llenos sin vender mas los vacios que
/// junto en la calle.
/// </summary>
public enum ContainerState
{
    Full = 0,
    Empty = 1
}
