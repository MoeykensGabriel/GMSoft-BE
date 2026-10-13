using GMSoft.Domain.Enums;

namespace GMSoft.Application.Features.Customers.Common;

public record LastNoPurchaseDto(DateTime Date, NoPurchaseReason Reason, string? Notes);
