using GMSoft.Data.Context;
using GMSoft.Domain.Common;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;

namespace ActivitySimulation;

public static class SeedBuilder
{
    public const string Prefix = "[GMSoft.ActivitySimulation/v1]";

    public static List<BaseEntity> Build(AppDbContext db, ActivityPlan plan, Driver driver, Guid admin,
        Product product, IReadOnlyDictionary<Guid, decimal> prices,
        IReadOnlyDictionary<DateTime, (int Open, int Close)> kilometers, Guid run,
        IReadOnlyDictionary<Guid, int>? retainedQuantities = null)
    {
        var rows = new List<BaseEntity>();
        var note = $"{(retainedQuantities is null ? Prefix : FirstVisit.Prefix)} {run}";
        int Quantity(PlannedCustomer buyer) => retainedQuantities?[buyer.Customer.Id] ?? 1;
        T Add<T>(T row, DateTime at) where T : BaseEntity
        {
            row.Id = Guid.NewGuid();
            row.CreatedAt = row.UpdatedAt = at;
            rows.Add(row);
            db.Add(row);
            return row;
        }
        foreach (var trip in plan.Trips)
        {
            var buyers = plan.Customers.Where(p => p.Seeded && p.Customer.ZoneId == trip.ZoneId
                && p.SaleAt == trip.OpenedAt.AddHours(1)).ToArray();
            var km = kilometers[trip.OpenedAt];
            var session = Add(new DeliverySession { DriverId = driver.Id, VehicleId = driver.VehicleId!.Value,
                ZoneId = trip.ZoneId, OpenedAt = trip.OpenedAt, ClosedAt = trip.ClosedAt,
                RouteDays = [trip.Day], DeferredCustomerIds = [], Status = SessionStatus.Closed,
                KilometersAtOpen = km.Open, KilometersAtClose = km.Close }, trip.OpenedAt);
            session.UpdatedAt = trip.ClosedAt;
            // One extra full unit always returns, including trips without any sale.
            var loaded = buyers.Sum(Quantity) + 1;
            Add(new VehicleLoad { VehicleId = session.VehicleId, ProductId = product.Id,
                Quantity = loaded, RouteDays = [trip.Day], LoadedAt = trip.OpenedAt.AddMinutes(-30),
                RegisteredByUserId = admin, ConsumedBySessionId = session.Id, ClientRequestId = Guid.NewGuid() },
                trip.OpenedAt.AddMinutes(-30));
            void Stock(int quantity, ContainerState state, SessionStockMovementType type, DateTime at,
                Guid actor, Guid? delivery = null) => Add(new SessionStockMovement {
                    DeliverySessionId = session.Id, ProductId = product.Id, Quantity = quantity,
                    State = state, Type = type, OccurredAt = at, RegisteredByUserId = actor,
                    DeliveryId = delivery, Notes = note }, at);
            Stock(loaded, ContainerState.Full, SessionStockMovementType.InitialLoad, trip.OpenedAt, admin);
            foreach (var buyer in buyers)
            {
                var sold = Quantity(buyer);
                var amount = prices[buyer.Customer.Id] * sold;
                var delivery = Add(new Delivery { DeliverySessionId = session.Id, CustomerId = buyer.Customer.Id,
                    Type = DeliveryType.Sale, DeliveredAt = buyer.SaleAt, Total = amount,
                    ClientRequestId = Guid.NewGuid(), Notes = note }, buyer.SaleAt);
                Add(new DeliveryItem { DeliveryId = delivery.Id, ProductId = product.Id,
                    Quantity = sold, UnitPrice = prices[buyer.Customer.Id] }, buyer.SaleAt);
                Add(new Payment { DeliverySessionId = session.Id, CustomerId = buyer.Customer.Id,
                    Amount = amount, Method = PaymentMethod.Cash, PaidAt = buyer.SaleAt, Notes = note }, buyer.SaleAt);
                Stock(-sold, ContainerState.Full, SessionStockMovementType.Delivered, buyer.SaleAt,
                    driver.ApplicationUserId!.Value, delivery.Id);
                if (product.Tracking == ContainerTracking.ByBalance)
                {
                    foreach (var quantity in retainedQuantities is null ? new[] { sold, -sold } : new[] { sold })
                        Add(new ContainerMovement { DeliveryId = delivery.Id, CustomerId = buyer.Customer.Id,
                            ProductId = product.Id, Quantity = quantity, OccurredAt = buyer.SaleAt,
                            Type = quantity > 0 ? ContainerMovementType.DeliveredToCustomer : ContainerMovementType.ReturnedFromCustomer,
                            RegisteredByUserId = driver.ApplicationUserId, Notes = note }, buyer.SaleAt);
                    if (retainedQuantities is null)
                        Stock(sold, ContainerState.Empty, SessionStockMovementType.CollectedEmpty, buyer.SaleAt,
                            driver.ApplicationUserId!.Value, delivery.Id);
                    else
                        Add(new CustomerContainerBalance { CustomerId = buyer.Customer.Id,
                            ProductId = product.Id, Quantity = sold }, buyer.SaleAt);
                }
            }
            Stock(-1, ContainerState.Full, SessionStockMovementType.ReturnedAtClose, trip.ClosedAt, admin);
            if (retainedQuantities is null && product.Tracking == ContainerTracking.ByBalance && buyers.Length > 0)
                Stock(-buyers.Length, ContainerState.Empty, SessionStockMovementType.ReturnedAtClose, trip.ClosedAt, admin);
            Add(new SessionCashSettlement { DeliverySessionId = session.Id,
                AmountReceived = buyers.Sum(b => prices[b.Customer.Id] * Quantity(b)), ReceivedAt = trip.ClosedAt,
                ReceivedByUserId = admin, Notes = note }, trip.ClosedAt);
        }
        return rows;
    }
}
