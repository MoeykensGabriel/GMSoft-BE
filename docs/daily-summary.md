# Resumen diario

ADMIN consulta el cuadre de un camion por dia de reparto. Solo lectura: no registra
movimientos, no modifica rendiciones y no requiere migraciones.

| Verbo | Ruta | Rol |
| --- | --- | --- |
| GET | /api/sessions/daily-summary?vehicleId=...&date=2026-10-08 | Admin |

VehicleId obligatorio, UUID no vacio. Date obligatoria, YYYY-MM-DD, distinta de
0001-01-01 y anterior a 9999-12-31 para poder calcular el intervalo completo.
Se filtra por OpenedAt con BusinessTime: desde las 00:00 de Argentina inclusive
hasta las 00:00 siguientes exclusive (03:00Z a 03:00Z). Es el mismo dia que usa
Liquidacion, aunque la recepcion ocurra otro dia. Todas las salidas, sin paginar,
ordenadas por apertura y luego ID. Productos agrupados por ID, ordenados por detalle
y luego ID. Fecha/hora en UTC; la pantalla muestra America/Argentina/Buenos_Aires.

Respuesta 200, JSON camelCase. Ejemplo completo de una salida recibida y rendida:

```json
{
  "vehicleId": "11111111-1111-4111-8111-111111111111",
  "date": "2026-10-08",
  "sessions": [{
    "sessionId": "22222222-2222-4222-8222-222222222222",
    "vehicleId": "11111111-1111-4111-8111-111111111111",
    "vehicleName": "Camion 1",
    "vehicleLicensePlate": "AA123BB",
    "driverName": "Juan Perez",
    "zoneName": "Centro",
    "openedAt": "2026-10-08T11:00:00Z",
    "closedAt": "2026-10-08T20:00:00Z",
    "status": "Closed",
    "isClosed": true,
    "kilometersAtOpen": 100,
    "kilometersAtClose": 150,
    "receivedAt": "2026-10-08T20:15:00Z",
    "notes": "Contado en oficina",
    "money": { "cashExpected": 10000, "transfer": 3000, "card": 2000, "cashDeclared": 9500, "cashDifference": 500 },
    "products": [{
      "productId": "33333333-3333-4333-8333-333333333333",
      "productDetail": "Bidon 20 litros",
      "stock": { "fullLoaded": 15, "fullSold": 6, "fullReturned": 8, "fullDifference": 1, "emptyCollected": 5, "emptyReturned": 4, "emptyDifference": 1 }
    }],
    "totals": { "fullLoaded": 15, "fullSold": 6, "fullReturned": 8, "fullDifference": 1, "emptyCollected": 5, "emptyReturned": 4, "emptyDifference": 1 }
  }],
  "dayTotals": {
    "isClosed": true,
    "pendingSettlements": 0,
    "money": { "cashExpected": 10000, "transfer": 3000, "card": 2000, "cashDeclared": 9500, "cashDifference": 500 },
    "products": [{
      "productId": "33333333-3333-4333-8333-333333333333",
      "productDetail": "Bidon 20 litros",
      "stock": { "fullLoaded": 15, "fullSold": 6, "fullReturned": 8, "fullDifference": 1, "emptyCollected": 5, "emptyReturned": 4, "emptyDifference": 1 }
    }],
    "totals": { "fullLoaded": 15, "fullSold": 6, "fullReturned": 8, "fullDifference": 1, "emptyCollected": 5, "emptyReturned": 4, "emptyDifference": 1 }
  }
}
```

## Columnas

- FullLoaded (cargados): entradas netas de llenos, InitialLoad + Restock + TransferIn
  + Adjustment - TransferOut. Se suman las cantidades con el signo del libro mayor:
  tambien los ajustes negativos reducen cargados. No se cuentan como ventas.
- FullSold (vendidos): opuesto de la suma de Delivered en Full. Cantidad entregada
  en ventas, promociones de visita y promociones de prueba, incluidas las que no
  tienen DeliveryId. No se vuelve a sumar DeliveryItem ni SessionRestock: duplicaria
  movimientos. Las promociones no generan dinero.
- FullReturned (rendidos): opuesto de ReturnedAtClose en Full, descarga contada.
- FullDifference: fullLoaded - fullSold - fullReturned.
- EmptyCollected (contabilizados): CollectedEmpty, incluidos retiros de pruebas
  sin conversion. Si existen otros movimientos de vacios, se suman con su signo
  aqui (carga, ajustes, transferencias); es la entrada neta contabilizada, no solo
  retiros de clientes. ReturnedAtClose queda separado. Esto conserva la identidad
  tambien para movimientos excepcionales, sin inventar retiros ni ocultar ajustes.
- EmptyReturned (rendidos): opuesto de ReturnedAtClose en Empty.
- EmptyDifference: emptyCollected - emptyReturned.
- CashExpected: suma de Payment.Amount con Method Cash de esa salida, incluidos
  cobros de deuda anterior. Transfer y Card: sumas de sus medios, solo informativas.
- CashDeclared: AmountReceived de SessionCashSettlement; null sin rendicion.
- CashDifference: cashExpected - cashDeclared; null sin rendicion. El resumen usa
  solo efectivo, aunque el DTO historico de Settlement compara todos los cobros.

Delivered y ReturnedAtClose salen negativos del camion; vendidos y rendidos se
muestran positivos. No se usa valor absoluto para esconder correcciones. Las
diferencias son exactamente el saldo del libro mayor: con salida abierta son
stock a bordo y rendidos es cero; cerrada coinciden con el faltante de Close.
Positivo es faltante, negativo sobrante. IsClosed/status distinguen ambos casos.

Notes y ReceivedAt provienen de la rendicion de efectivo. Close no guarda notas
propias de recepcion: no se inventa un campo ni se usan notas de ventas/recargas.
KilometersAtOpen y KilometersAtClose son lecturas del odometro, no una distancia.

Totals suma cada columna de la salida; dayTotals.products agrupa y suma por producto
entre todas las salidas, y dayTotals.totals suma esas filas. Los importes se suman
por medio. Si alguna salida no rindio, cashDeclared y cashDifference del dia son
null y pendingSettlements indica cuantas faltan: un total parcial no es un cuadre.
DayTotals.isClosed es true solo si todas cerraron. Con alguna abierta el total de
diferencias es provisional (a bordo mas diferencias de las cerradas), siempre neutro;
cada bloque cerrado conserva su propio control. La pantalla muestra el total del
dia cuando hay mas de una salida.

Sin salidas (tambien para un UUID de vehiculo sin registros): sessions y products
vacios, totales cero, pendingSettlements 0 e isClosed true. 400 para filtros invalidos,
401 sin autenticar y 403 para otro rol, usando ProblemDetails habitual.
