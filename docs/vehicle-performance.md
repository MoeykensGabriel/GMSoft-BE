# Rendimiento por camión

`GET /api/reports/vehicle-performance?from=2031-05-10&to=2031-05-11`

Requiere Bearer JWT con rol `Admin`. Responde 200 con el objeto siguiente,
401 sin autenticación, 403 sin permiso y 400 ante parámetros inválidos
(ProblemDetails y `errors` por campo, como los demás endpoints).
No tiene paginación.

## Período e inclusión

Ambas fechas son obligatorias, con formato `YYYY-MM-DD`. `from <= to` y
como máximo **366 días inclusivos**. Se rechazan `0001-01-01` y `9999-12-31`
para evitar valores por defecto y desbordamientos al convertir a UTC.

Se seleccionan salidas por `DeliverySession.OpenedAt`, usando `BusinessTime`
(Argentina, UTC−3). Desde las 00:00 de `from` inclusive hasta las 00:00 del
día posterior a `to` exclusive. El ejemplo selecciona `[2031-05-10T03:00:00Z,
2031-05-12T03:00:00Z)`. No se filtra por fecha de cierre, visita, pago ni rendición:
toda la actividad vinculada a esas salidas entra, incluso si ocurrió después.
Incluye salidas todavía abiertas.

Incluye todos los camiones activos (en este modelo: `IsDeleted == false`),
aunque no tengan salidas, y también los dados de baja que tuvieron salidas
en el período. Excluye salidas dadas de baja. Orden: `sold` descendente,
desempate por `vehicleName` y luego `vehicleId`. Puede devolver `vehicles: []`
si no hay camiones elegibles.

## JSON de ejemplo

Los importes y cocientes son números JSON, no texto. Los IDs son UUID.
Las listas y objetos de conteos nunca son null.

```json
{
  "from": "2031-05-10",
  "to": "2031-05-11",
  "vehicles": [
    {
      "vehicleId": "11111111-1111-4111-8111-111111111111",
      "vehicleName": "Camión 1",
      "licensePlate": "AA123BB",
      "driverNames": ["Juan Prueba"],
      "sessions": 2,
      "closedSessions": 1,
      "sold": 600,
      "collected": 575,
      "collectedByMethod": { "cash": 250, "transfer": 300, "card": 25 },
      "onAccount": 25,
      "collectedPercent": 0.9583333333333333333333333333,
      "routeCustomers": 3,
      "visitedCustomers": 3,
      "buyingCustomers": 2,
      "buyingPercent": 0.6666666666666666666666666667,
      "visits": 6,
      "sales": 3,
      "averageTicket": 200,
      "noPurchase": { "total": 3, "byReason": { "NotHome": 2, "NoMoney": 1 } },
      "unitsByProduct": [
        { "productId": "22222222-2222-4222-8222-222222222222", "productDetail": "Bidón", "units": 6, "amount": 600 }
      ],
      "shortages": { "full": 2, "empty": 3 },
      "cashDifference": 10,
      "kilometers": 25,
      "soldPerKilometer": 24
    },
    {
      "vehicleId": "33333333-3333-4333-8333-333333333333",
      "vehicleName": "Camión sin salidas",
      "licensePlate": "AC456DE",
      "driverNames": [],
      "sessions": 0,
      "closedSessions": 0,
      "sold": 0,
      "collected": 0,
      "collectedByMethod": { "cash": 0, "transfer": 0, "card": 0 },
      "onAccount": 0,
      "collectedPercent": null,
      "routeCustomers": 1,
      "visitedCustomers": 0,
      "buyingCustomers": 0,
      "buyingPercent": null,
      "visits": 0,
      "sales": 0,
      "averageTicket": null,
      "noPurchase": { "total": 0, "byReason": {} },
      "unitsByProduct": [],
      "shortages": { "full": 0, "empty": 0 },
      "cashDifference": 0,
      "kilometers": 0,
      "soldPerKilometer": null
    }
  ]
}
```

## Definiciones para el frontend

| Campo | Cálculo y presentación |
| --- | --- |
| `vehicleId`, `vehicleName`, `licensePlate` | Identidad y datos actuales del camión. |
| `driverNames` | Nombres actuales de los choferes de las salidas seleccionadas, sin nombres duplicados y ordenados alfabéticamente. No son los choferes asignados hoy. |
| `sessions`, `closedSessions` | Cantidad de salidas seleccionadas; subconjunto con estado `Closed`. |
| `sold` | Suma de `Delivery.Total` exclusivamente de visitas `Sale`. |
| `collected` | Todos los `Payment.Amount` asociados a las salidas, incluidos cobros de deuda. Excluye pagos de oficina sin salida. |
| `collectedByMethod` | Los mismos pagos, separados en efectivo (`cash`), transferencia (`transfer`) y tarjeta (`card`). |
| `onAccount` | `sold - collected`, mismo criterio neto de `NewDebt` de la rendición. **Puede ser negativo** por cobros de deuda. El modelo no vincula pagos a ventas individuales: no representa una imputación exacta de deuda a cada venta ni se limita a cero. |
| `collectedPercent` | `collected / sold`; null si `sold == 0`. Es una proporción: multiplicar por 100 para mostrar porcentaje; puede superar 1. |
| `routeCustomers` | Clientes activos, no dados de baja, asignados **hoy** al camión. No se filtra por día de visita ni se reconstruye la asignación histórica. Puede ser mayor que cero sin salidas. |
| `visitedCustomers` | Clientes distintos con al menos una `Delivery` de cualquier tipo en las salidas. Un pago solo no constituye visita. |
| `buyingCustomers` | Clientes distintos con al menos una visita `Sale`. Se deduplican entre todas las salidas del camión. |
| `buyingPercent` | `buyingCustomers / visitedCustomers`; null sin visitados. Multiplicar por 100 para mostrar porcentaje. |
| `visits`, `sales` | Número total de visitas de cualquier tipo y número de visitas `Sale`, respectivamente. |
| `averageTicket` | `sold / sales`; null sin ventas. |
| `noPurchase.total` | Cantidad de visitas `NoPurchase`, no de clientes. |
| `noPurchase.byReason` | Objeto con motivos presentes y conteos. Claves: `NotHome` (no estaba), `StillHasProduct` (todavía tiene), `DidNotWant` (no quiso), `NoMoney` (sin dinero), `BuysElsewhere` (compra en otro lugar), `Other` (otro). Se omiten motivos con cero; `Unknown` cubre datos históricos sin motivo. |
| `unitsByProduct` | Solo ítems de ventas: suma de cantidades y suma de `Quantity * UnitPrice` (precio de la venta), por producto. Descripción actual; orden por descripción y luego ID. |
| `shortages.full`, `shortages.empty` | Diferencias firmadas de llenos/vacíos de salidas **cerradas**: saldo del libro de stock después de recepción. Equivale a cargado − entregado − devuelto, incluyendo recargas, promociones, ajustes y traspasos, como Resumen diario. Positivo = faltante, negativo = sobrante. Se suman con signo, por lo que se pueden compensar. El stock a bordo de salidas abiertas no es un faltante. |
| `cashDifference` | Suma de `CashSettlementCalculation.Difference(efectivo cobrado, AmountReceived)` solo de rendiciones registradas. Positivo = falta efectivo; negativo = sobra. Transferencias y tarjetas no generan faltante. Cero si no hay rendiciones; **no indica que todas estén rendidas**. |
| `kilometers` | Suma de `KilometersAtClose - KilometersAtOpen` en salidas cerradas con kilometraje de cierre. |
| `soldPerKilometer` | `sold / kilometers`; null si kilómetros es cero. El numerador incluye ventas de salidas abiertas, por consistencia con `sold`. |

Sin redondeo de negocio adicional: el frontend puede formatear importes a dos
decimales y porcentajes según su presentación. Los cocientes nulos deben
mostrarse como “—”, no como 0 %. Los datos de actividad respetan las bajas
lógicas existentes; esto es un reporte operativo, no una instantánea contable.

## Implementación y verificación

Ocho consultas en lote, independientes de la cantidad de camiones: salidas
proyectadas, flota, clientes asignados, visitas/ventas, motivos, productos,
pagos por salida/medio y stock por camión/estado. Los conteos y sumas de
actividad se agregan en SQL; no se carga un grafo de entidades ni hay N+1.

Tests: `GMSoft.Application.Tests/Features/Reports/VehiclePerformanceTests.cs`
y `GMSoft.IntegrationTests/VehiclePerformanceTests.cs`. La integración usa
PostgreSQL efímero mediante la fixture existente, sin iniciar la API.

```powershell
dotnet build GMSoft.slnx -c Release --no-restore
dotnet test GMSoft.slnx -c Release --no-restore
```
