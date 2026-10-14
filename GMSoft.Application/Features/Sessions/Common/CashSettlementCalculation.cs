namespace GMSoft.Application.Features.Sessions.Common;

public static class CashSettlementCalculation
{
    public static decimal? Difference(decimal cashCollected, decimal? amountReceived)
        => cashCollected - amountReceived;
}
