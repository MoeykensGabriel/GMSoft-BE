using System.Reflection;
using GMSoft.API.Controllers;
using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Features.Reports.RouteSheet;
using GMSoft.Domain.Entities;
using GMSoft.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.IntegrationTests;

[Collection("PostgreSQL")]
public class RouteSheetTests(PostgresFixture fixture)
{
    // Lunes: la hoja se arma con los clientes que tienen el día 1.
    private static readonly DateOnly Lunes = new(2026, 10, 26);

    [Fact]
    public async Task Sheet_lists_the_day_customers_by_agenda_zone_and_route_order_with_containers_and_balance()
    {
        var a = await new Scenario(fixture).Seed(open: false);
        var otro = await new Scenario(fixture).Seed(open: false);
        var segundaZona = new Zone { Id = Guid.NewGuid(), Name = "A primera por agenda" };
        var enSegunda = a.Customer(1, [1, 3]); enSegunda.ZoneId = segundaZona.Id; enSegunda.BusinessName = "Kiosco";
        var antes = a.Customer(2);
        var martes = a.Customer(3, [2]);
        var inactivo = a.Customer(4); inactivo.IsActive = false;
        var conBaja = a.Customer(5);
        await a.Write(db =>
        {
            db.Zones.Add(segundaZona);
            db.Customers.AddRange(enSegunda, antes, martes, inactivo, conBaja);
            // La agenda pone la zona nueva antes que la del escenario, aunque su nombre ordene distinto.
            db.Set<VehicleScheduleDay>().AddRange(
                new VehicleScheduleDay { VehicleId = a.VehicleId, Day = 1, Position = 0, ZoneId = segundaZona.Id },
                new VehicleScheduleDay { VehicleId = a.VehicleId, Day = 1, Position = 1, ZoneId = a.ZoneId });
            db.Set<CustomerRemovalRequest>().Add(new CustomerRemovalRequest { CustomerId = conBaja.Id, ClientRequestId = Guid.NewGuid(),
                RequestedByUserId = a.UserId, DriverId = a.DriverId, VehicleId = a.VehicleId, ZoneId = a.ZoneId,
                RequestedAt = DateTime.UtcNow, Status = RemovalStatus.Pending, Reason = RemovalReason.Other });
            var salida = new DeliverySession { Id = Guid.NewGuid(), VehicleId = a.VehicleId, DriverId = a.DriverId, ZoneId = a.ZoneId,
                OpenedAt = DateTime.UtcNow.AddDays(-1), ClosedAt = DateTime.UtcNow, Status = SessionStatus.Closed, KilometersAtOpen = 100 };
            db.DeliverySessions.Add(salida);
            db.Deliveries.Add(new Delivery { DeliverySessionId = salida.Id, CustomerId = a.CustomerId, Type = DeliveryType.Sale,
                Total = 300, DeliveredAt = salida.OpenedAt });
            db.Payments.Add(new Payment { CustomerId = a.CustomerId, Amount = 100, Method = PaymentMethod.Cash, PaidAt = salida.OpenedAt });
            db.Payments.Add(new Payment { CustomerId = antes.Id, Amount = 50, Method = PaymentMethod.Cash, PaidAt = salida.OpenedAt });
            db.CustomerContainerBalances.Add(new CustomerContainerBalance { CustomerId = a.CustomerId, ProductId = a.ProductId, Quantity = 2 });
            db.CustomerContainerBalances.Add(new CustomerContainerBalance { CustomerId = antes.Id, ProductId = a.ProductId, Quantity = 0 });
            db.ContainerUnits.Add(new ContainerUnit { ProductId = a.UnitId, SerialNumber = Guid.NewGuid().ToString("N"),
                Status = ContainerUnitStatus.WithCustomer, CurrentCustomerId = a.CustomerId });
            return Task.CompletedTask;
        });

        var hoja = await a.Send(new GetRouteSheetQuery(a.VehicleId, Lunes), admin: true);

        Assert.Equal("Camion", hoja.VehicleName);
        Assert.Equal(Lunes, hoja.Date);
        Assert.Equal(new[] { segundaZona.Id, a.ZoneId }, hoja.Zones.Select(z => z.ZoneId));
        Assert.Equal("Kiosco", Assert.Single(hoja.Zones[0].Customers).DisplayName);
        Assert.Equal(new[] { antes.Id, a.CustomerId }, hoja.Zones[1].Customers.Select(c => c.CustomerId));
        var conCuenta = hoja.Zones[1].Customers[1];
        Assert.Equal(200m, conCuenta.Balance);
        Assert.Equal(new[] { new RouteSheetContainerDto(a.ProductId, "Bidon", 2), new RouteSheetContainerDto(a.UnitId, "Dispenser", 1) },
            conCuenta.Containers);
        Assert.Equal(-50m, hoja.Zones[1].Customers[0].Balance);
        Assert.Empty(hoja.Zones[1].Customers[0].Containers);

        // Los del martes, los inactivos y los que tienen baja pedida no van; tampoco los de otro camión.
        var todos = hoja.Zones.SelectMany(z => z.Customers).Select(c => c.CustomerId).ToList();
        Assert.Equal(3, todos.Count);
        Assert.DoesNotContain(otro.CustomerId, todos);
    }

    [Fact]
    public async Task Zones_outside_the_agenda_go_last_and_an_unknown_vehicle_is_not_found()
    {
        var a = await new Scenario(fixture).Seed(open: false);
        var hoja = await a.Send(new GetRouteSheetQuery(a.VehicleId, Lunes), admin: true);
        Assert.Equal(a.CustomerId, Assert.Single(Assert.Single(hoja.Zones).Customers).CustomerId);

        var martes = await a.Send(new GetRouteSheetQuery(a.VehicleId, Lunes.AddDays(1)), admin: true);
        Assert.Empty(martes.Zones);

        await Assert.ThrowsAsync<NotFoundException>(() => a.Send(new GetRouteSheetQuery(Guid.NewGuid(), Lunes), admin: true));
        await Assert.ThrowsAsync<ForbiddenException>(() => a.Send(new GetRouteSheetQuery(a.VehicleId, Lunes)));
    }

    [Fact]
    public void Endpoint_is_get_on_reports()
        => Assert.Equal("route-sheet", typeof(ReportsController).GetMethod("RouteSheet")!
            .GetCustomAttribute<HttpGetAttribute>()!.Template);
}
