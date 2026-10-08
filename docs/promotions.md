# Promociones de prueba

Circuito propio: el prospecto no es un Customer. El tipo historico `Promotion`
de POST /api/deliveries sigue disponible sin cambios. Para nuevas pruebas usar
estos endpoints. Los roles del token son `Admin` (ADMIN) y `Driver` (chofer).

| Verbo | Ruta | Rol |
| --- | --- | --- |
| POST | /api/promotions | Driver |
| GET | /api/promotions/pending | Driver |
| POST | /api/promotions/{id}/close | Driver |
| GET | /api/promotions | Admin |
| GET | /api/promotions/{id} | Admin |
| GET | /api/promotions/settings | Admin |
| PUT | /api/promotions/settings | Admin |

Todos responden 200 con JSON. Fechas/hora en UTC (ISO 8601 con Z); `pickupDate`
es fecha local del negocio (YYYY-MM-DD, Argentina). Los nombres JSON son camelCase.

## Registrar

POST /api/promotions:

```json
{
  "clientRequestId": "11111111-1111-4111-8111-111111111111",
  "prospect": {
    "businessName": null,
    "contactName": "Ana",
    "phone": "1122334455",
    "address": "Calle 123",
    "notes": null,
    "visitDays": [2, 5]
  },
  "items": [
    { "productId": "22222222-2222-4222-8222-222222222222", "quantity": 3 }
  ]
}
```

Prospect tiene las mismas validaciones que `newCustomer` en visitas:
contactName obligatorio (150), phone obligatorio (30), address obligatorio (300),
businessName opcional (200), notes opcional (1000); visitDays no vacio, sin
repetidos, ISO lunes=1 a domingo=7. Items no vacio, cantidades enteras > 0,
sin productos repetidos. No se envian precios, cobro ni cantidades de envases:
ByBalance presta uno por unidad; None no presta; ByUnit se rechaza.

Salida, camion, zona y chofer se toman de la salida abierta autenticada.
Descuenta llenos usando la misma validacion de stock de ventas. Guarda el
prospecto, sin cliente, venta, deuda ni cobro. La fecha de retiro se congela como
dia argentino del registro + pickupDays.

Respuesta de registro y cierre (misma forma):

```json
{
  "promotionId": "33333333-3333-4333-8333-333333333333",
  "customerId": null,
  "status": "Pending",
  "registeredAt": "2026-10-07T15:00:00Z",
  "pickupDate": "2026-10-14",
  "closedAt": null
}
```

En esta respuesta de escritura, status es el estado persistido:
Pending / Converted / NotConverted. En consultas, Pending vencida se presenta
como Overdue.

## Cerrar

POST /api/promotions/33333333-3333-4333-8333-333333333333/close.
Convertir con los datos guardados:

```json
{
  "clientRequestId": "44444444-4444-4444-8444-444444444444",
  "convertToCustomer": true,
  "customer": null,
  "containersReturned": []
}
```

Si hay que corregir datos, customer acepta un objeto completo con exactamente
los mismos campos y validaciones que prospect; reemplaza los datos para el alta,
no es un parche parcial. El prospecto original queda conservado.
El cliente toma camion y zona de la promocion original y queda al final del
recorrido de esa zona. Sus envases se traspasan al saldo con movimientos
enlazados, sin retirarlos ni volver a descontar llenos. No crea Delivery ni
Payment, no genera deuda ni compra; LastVisitAt queda sin visita hasta que se
registre una. Responde customerId del nuevo cliente, status Converted y closedAt.

Cerrar sin conversion, recuperando dos de los tres prestados:

```json
{
  "clientRequestId": "55555555-5555-4555-8555-555555555555",
  "convertToCustomer": false,
  "customer": null,
  "containersReturned": [
    { "productId": "22222222-2222-4222-8222-222222222222", "quantity": 2 }
  ]
}
```

ContainersReturned es obligatorio (puede ser []). Solo admite productos con
envases prestados, sin repetidos y cantidades enteras >= 0 que no superen lo
prestado. Omitir un producto equivale a recuperar cero. El resto se registra
como Lost; ADMIN ve containersReturned y containersLost por linea.
Los retirados suben como vacios a la salida actual. Responde status NotConverted
y closedAt, con customerId null. Convertir exige containersReturned vacio;
sin conversion exige customer null.

Ambos cierres requieren salida abierta del mismo camion, aunque sea otro chofer,
otra fecha o zona. Nunca se reabre ni se vuelve a cerrar una promocion cerrada.

## Consultar

GET /api/promotions/pending devuelve un array de PromotionDto. Usa el camion
de la salida abierta; sin salida, el asignado actualmente al chofer. Ordena
pickupDate, registeredAt, id ascendentes: vencidas, hoy y futuras.
Incluye todas las pendientes del camion, sin filtro por zona o dias de clientes.

GET /api/promotions?page=1&pageSize=20&status=Overdue&vehicleId=...&from=2026-10-01&to=2026-10-31

Filtros opcionales: status (Pending, Overdue, Converted, NotConverted),
vehicleId, from y to (YYYY-MM-DD). Pending incluye vencidas; Overdue es solo
Pending con pickupDate < hoy. From y to incluyen ambos dias y filtran la fecha
de registro, en Argentina, antes de paginar. Page >= 1 (maximo 21474836),
pageSize 1–100, por defecto 1 y 20; orden registeredAt descendente, id ascendente.
Respuesta: `{ "items": [PromotionDto], "totalCount": 1, "page": 1, "pageSize": 20,
"totalPages": 1, "hasPreviousPage": false, "hasNextPage": false }`.
GET /api/promotions/{id} devuelve un solo PromotionDto:

```json
{
  "id": "33333333-3333-4333-8333-333333333333",
  "vehicleId": "66666666-6666-4666-8666-666666666666",
  "vehicleName": "Camion 1",
  "vehicleLicensePlate": "AB123CD",
  "driverId": "77777777-7777-4777-8777-777777777777",
  "driverName": "Juan Perez",
  "deliverySessionId": "88888888-8888-4888-8888-888888888888",
  "zoneId": "99999999-9999-4999-8999-999999999999",
  "registeredAt": "2026-10-07T15:00:00Z",
  "pickupDate": "2026-10-14",
  "status": "Pending",
  "isOverdue": false,
  "isDueToday": false,
  "prospect": {
    "businessName": null,
    "contactName": "Ana",
    "phone": "1122334455",
    "address": "Calle 123",
    "notes": null,
    "visitDays": [2, 5]
  },
  "lines": [{
    "productId": "22222222-2222-4222-8222-222222222222",
    "productDetail": "Bidon 20 litros",
    "quantity": 3,
    "containersLoaned": 3,
    "containersReturned": 0,
    "containersLost": 0
  }],
  "customerId": null,
  "closedAt": null,
  "closingSessionId": null,
  "closedByDriverId": null,
  "clientRequestId": "11111111-1111-4111-8111-111111111111",
  "closeClientRequestId": null
}
```

IsOverdue e isDueToday se calculan solo para pendientes. Overdue no se guarda.
Al cerrar se completan closingSessionId, closedByDriverId y closeClientRequestId.

## Plazo y reintentos

GET /api/promotions/settings devuelve `{ "pickupDays": 7 }`.
PUT /api/promotions/settings recibe y devuelve `{ "pickupDays": 10 }`.
Entero positivo, una sola fila (Id=1), inicializada atomicamente en el primer
acceso con 7. Cambiarlo nunca mueve fechas ya guardadas. Si el plazo no cabe en
el rango de fechas del sistema, el registro devuelve 409 sin movimientos.

ClientRequestId es UUID obligatorio y no vacio en registro y cierre, con indices
unicos separados. Reusar el mismo envio devuelve el resultado actual de la
promocion sin repetir efectos, incluso si la salida ya cerro; debe hacerlo el
mismo chofer que registro ese envio. Conservar clave y cuerpo al reintentar.
Una clave de cierre no se puede reutilizar para otra promocion u otro resultado.
Otra clave contra una promocion cerrada devuelve 409.

Errores en ProblemDetails: 400 validaciones/ByUnit/retiros invalidos,
403 otro camion/chofer, 404 recurso inexistente, 409 sin salida/stock insuficiente/
doble cierre. Las escrituras son transaccionales. Locks de PostgreSQL serializan
reintentos y cierres de promociones; status tambien tiene control de concurrencia.
La validacion compartida de stock mantiene el alcance de ventas: no incorpora
un bloqueo global de ventas/cargas concurrentes.

## Envases afuera y migracion

GET /api/reports/containers-out (Admin) conserva una fila por producto y agrega
promotionQuantityOut y promotions. QuantityOut ahora incluye clientes y prospectos;
customersHolding sigue contando solo clientes. Ejemplo con tres envases de prueba:

```json
[{
  "productId": "22222222-2222-4222-8222-222222222222",
  "productDetail": "Bidon 20 litros",
  "tracking": "ByBalance",
  "quantityOut": 3,
  "customersHolding": 0,
  "promotionQuantityOut": 3,
  "promotions": [{
    "promotionId": "33333333-3333-4333-8333-333333333333",
    "prospectName": "Ana",
    "quantity": 3,
    "pickupDate": "2026-10-14"
  }]
}]
```

Sin prospectos: promotionQuantityOut = 0 y promotions = []. Al convertir,
salen de promotions y pasan a los saldos de clientes sin duplicarse. Al cerrar
sin conversion salen del reporte, incluidos los perdidos. Los prospectos nunca
entran a consultas ni reportes de clientes.

TrialPromotions agrega cuatro tablas e indices; no altera tablas ni datos
existentes. PromotionContainerMovements enlaza prestamos, traspasos, retiros
y perdidas al libro mayor existente. No se aplico la migracion durante esta tarea.

