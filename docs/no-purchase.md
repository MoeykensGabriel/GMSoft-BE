# Visita sin compra

## Registro: POST /api/deliveries (Chofer)

Se usa el mismo endpoint de visitas. La salida se obtiene del chofer autenticado;
no se acepta una salida elegida por el navegador. Ejemplo de cuerpo:

```json
{
  "customerId": "11111111-1111-1111-1111-111111111111",
  "type": "NoPurchase",
  "noPurchaseReason": "Other",
  "notes": "Pidió que pasemos el jueves",
  "clientRequestId": "22222222-2222-2222-2222-222222222222",
  "items": [], "containersOut": [], "containersIn": [], "payment": null
}
```

Respuesta 200 (también al actualizar o reintentar):

```json
{"deliveryId":"33333333-3333-3333-3333-333333333333","customerId":"11111111-1111-1111-1111-111111111111","total":0,"saldoCuentaCliente":1500}
```

`saldoCuentaCliente` es el saldo actual; no se modifica por esta visita ni queda
congelado en un reintento. `deliveryId` permanece al cambiar el motivo.

| Valor | Etiqueta |
| --- | --- |
| NotHome | No estaba |
| StillHasProduct | Todavía tiene producto |
| DidNotWant | No quiso |
| NoMoney | Sin dinero |
| BuysElsewhere | Compra a otro proveedor |
| Other | Otro |

Elección única obligatoria. `notes` admite hasta 300 caracteres, se recorta en los
extremos y es obligatoria y no blanca para `Other`; opcional para los demás.
`clientRequestId` es UUID obligatorio y distinto de cero. `customerId` debe ser
un cliente existente activo, del camión, zona y alguno de los días de la salida.
No permite `newCustomer`, productos, envases ni `payment` (ni siquiera importe cero).
Las tres colecciones se envían vacías, no null. Motivo solo se acepta en NoPurchase.

Se bloquea la salida abierta dentro de la transacción y se reconsulta la clave
bajo ese bloqueo. Un nuevo identificador para el mismo cliente/salida actualiza
motivo, nota y fecha de la única visita NoPurchase. Se conservan todas las claves
aceptadas en `NoPurchaseRequestIds`: reintentar una corrección vieja devuelve la
visita existente y no revierte la más reciente. No se altera `ClientRequestId`
original. Estas claves son propias de la visita y no deben reutilizarse en ventas.
Una clave que pertenece a otra salida se rechaza con 409.

Si ya hay Sale en esa salida, un nuevo NoPurchase recibe 409: «Ya compró en esta
salida.» Una venta posterior sí se permite y conserva el NoPurchase histórico.
Un reintento ya aceptado no crea ni corrige nada, incluso si hubo una venta después.
Se requiere salida abierta también para reintentar (igual que las ventas).

NoPurchase tiene total cero y no crea items, stock, envases, pagos ni movimientos
de cuenta. Actualiza LastVisitAt y elimina únicamente la marca de pospuesto del
cliente. No reinicia actividad: última compra sigue usando exclusivamente Sale.

Errores: 400 validación (motivo, nota, clave, contenido o cliente fuera del recorrido),
404 cliente inexistente, 409 sin salida abierta/salida recibida/cliente desactivado/
venta previa/clave de otra salida. 401 sin autenticación y 403 rol incorrecto.
Los errores de aplicación usan ProblemDetails; los de validación incluyen `errors`.

## Consultas

- GET `/api/sessions/{id}/deliveries`: ADMIN o chofer dueño, abierta o cerrada.
  Mantiene orden cronológico; cada NoPurchase incluye `type: "NoPurchase"`,
  `noPurchaseReason`, `notes`, `deliveredAt`, `total: 0`, `items: []`, `containers: []`.
- GET `/api/sessions/{id}/detailed-settlement`: ADMIN. Incluye a todo cliente visitado.
  Agrega `noPurchases: [{date, reason, notes}]`; conserva `lines: []` cuando solo
  tuvo NoPurchase. No inventa renglones de venta. Balance conserva deuda anterior.
- GET `/api/customers`, `/api/customers/{id}`, `/api/customers/{id}/account`:
  mantienen sus permisos y agregan `lastNoPurchase: {date, reason, notes}` o null.
  Se muestra la más reciente únicamente si es estrictamente posterior a la última
  Sale (o si nunca compró). Una fecha igual no cuenta como posterior.
- GET `/api/sessions/{id}/unvisited-customers`: ADMIN o chofer dueño, abierta o
  cerrada. 404 si no existe; 403 salida ajena. Respuesta 200:

```json
{
  "items": [{
    "id": "11111111-1111-1111-1111-111111111111",
    "displayName": "Almacén Ana", "contactName": "Ana", "businessName": "Almacén Ana",
    "address": "San Martín 123", "phone": "3811234567", "routeOrder": 4,
    "weeksWithoutPurchase": 2, "activityStatus": "Red"
  }],
  "total": 1
}
```

`items` usa CustomerDto completo; el ejemplo muestra sus campos relevantes.
Orden RouteOrder y desempate por id, sin paginación. Incluye solo activos del
camión/zona/días actuales de la salida, sin NINGUNA Delivery en ella: Sale,
ContainerOnly, Promotion histórico y NoPurchase excluyen. Una marca de pospuesto
sola no excluye. Promociones de prospectos no son visitas a clientes.
**Limitación:** en salidas cerradas se usa la asignación actual del cliente, no una
foto histórica. Cambiar camión/zona/días/estado después puede cambiar esta lista.

## Persistencia y revisión de consultas

Migración aditiva NoPurchaseVisits: motivo integer nullable y claves de corrección
uuid[] nullable en Deliveries; no modifica datos existentes. Notes reutiliza la
columna histórica. No se aplica a desarrollo como parte de esta tarea.

Liquidación y recorrido identifican explícitamente las visitas; los totales de
sesión y deudores suman Total (cero). Inactivos y últimas compras filtran Sale.
Resumen diario usa movimientos de stock y pagos, ambos ausentes. Envases afuera
usa saldos/unidades/promociones; NoPurchase no escribe ninguno. Cuenta excluye
NoPurchase de movimientos, sin ocultar su dato lastNoPurchase. Promociones y su
conversión conservan el circuito existente. No se agregan conteos de ventas.

## Archivos de esta implementación

C = creado; M = modificado.

### GMSoft.API

- M: [GMSoft.API/Controllers/SessionsController.cs](../GMSoft.API/Controllers/SessionsController.cs)

### GMSoft.Application

- M: [GMSoft.Application/Common/Interfaces/Repositories/ICustomerRepository.cs](../GMSoft.Application/Common/Interfaces/Repositories/ICustomerRepository.cs)
- M: [GMSoft.Application/Common/Interfaces/Repositories/ISessionRepository.cs](../GMSoft.Application/Common/Interfaces/Repositories/ISessionRepository.cs)
- M: [GMSoft.Application/Features/Customers/Account/CustomerAccountDto.cs](../GMSoft.Application/Features/Customers/Account/CustomerAccountDto.cs)
- M: [GMSoft.Application/Features/Customers/Account/GetCustomerAccountQueryHandler.cs](../GMSoft.Application/Features/Customers/Account/GetCustomerAccountQueryHandler.cs)
- M: [GMSoft.Application/Features/Customers/Common/CustomerDto.cs](../GMSoft.Application/Features/Customers/Common/CustomerDto.cs)
- M: [GMSoft.Application/Features/Customers/Common/CustomerMapping.cs](../GMSoft.Application/Features/Customers/Common/CustomerMapping.cs)
- M: [GMSoft.Application/Features/Customers/GetById/GetCustomerByIdQueryHandler.cs](../GMSoft.Application/Features/Customers/GetById/GetCustomerByIdQueryHandler.cs)
- M: [GMSoft.Application/Features/Customers/GetList/GetCustomersQueryHandler.cs](../GMSoft.Application/Features/Customers/GetList/GetCustomersQueryHandler.cs)
- M: [GMSoft.Application/Features/Deliveries/Register/RegisterDeliveryCommand.cs](../GMSoft.Application/Features/Deliveries/Register/RegisterDeliveryCommand.cs)
- M: [GMSoft.Application/Features/Deliveries/Register/RegisterDeliveryCommandHandler.cs](../GMSoft.Application/Features/Deliveries/Register/RegisterDeliveryCommandHandler.cs)
- M: [GMSoft.Application/Features/Deliveries/Register/RegisterDeliveryCommandValidator.cs](../GMSoft.Application/Features/Deliveries/Register/RegisterDeliveryCommandValidator.cs)
- M: [GMSoft.Application/Features/Sessions/Common/SessionDeliveryDto.cs](../GMSoft.Application/Features/Sessions/Common/SessionDeliveryDto.cs)
- M: [GMSoft.Application/Features/Sessions/DetailedSettlement/CustomerSettlementDto.cs](../GMSoft.Application/Features/Sessions/DetailedSettlement/CustomerSettlementDto.cs)
- M: [GMSoft.Application/Features/Sessions/DetailedSettlement/GetSessionDetailedSettlementQueryHandler.cs](../GMSoft.Application/Features/Sessions/DetailedSettlement/GetSessionDetailedSettlementQueryHandler.cs)
- C: [GMSoft.Application/Features/Customers/Common/LastNoPurchaseDto.cs](../GMSoft.Application/Features/Customers/Common/LastNoPurchaseDto.cs)
- C: [GMSoft.Application/Features/Sessions/Unvisited/GetUnvisitedCustomersQuery.cs](../GMSoft.Application/Features/Sessions/Unvisited/GetUnvisitedCustomersQuery.cs)

### GMSoft.Application.Tests

- M: [GMSoft.Application.Tests/Features/Customers/CustomerRouteAccessTests.cs](../GMSoft.Application.Tests/Features/Customers/CustomerRouteAccessTests.cs)
- M: [GMSoft.Application.Tests/Features/Deliveries/RegisterDeliveryWorkflowTests.cs](../GMSoft.Application.Tests/Features/Deliveries/RegisterDeliveryWorkflowTests.cs)
- C: [GMSoft.Application.Tests/Features/Customers/NoPurchaseMappingTests.cs](../GMSoft.Application.Tests/Features/Customers/NoPurchaseMappingTests.cs)

### GMSoft.Data

- M: [GMSoft.Data/Migrations/AppDbContextModelSnapshot.cs](../GMSoft.Data/Migrations/AppDbContextModelSnapshot.cs)
- M: [GMSoft.Data/Repositories/CustomerRepository.cs](../GMSoft.Data/Repositories/CustomerRepository.cs)
- M: [GMSoft.Data/Repositories/SessionRepository.cs](../GMSoft.Data/Repositories/SessionRepository.cs)
- C: [GMSoft.Data/Migrations/20261013174018_NoPurchaseVisits.Designer.cs](../GMSoft.Data/Migrations/20261013174018_NoPurchaseVisits.Designer.cs)
- C: [GMSoft.Data/Migrations/20261013174018_NoPurchaseVisits.cs](../GMSoft.Data/Migrations/20261013174018_NoPurchaseVisits.cs)

### GMSoft.Domain

- M: [GMSoft.Domain/Entities/Delivery.cs](../GMSoft.Domain/Entities/Delivery.cs)
- M: [GMSoft.Domain/Enums/DeliveryType.cs](../GMSoft.Domain/Enums/DeliveryType.cs)
- C: [GMSoft.Domain/Enums/NoPurchaseReason.cs](../GMSoft.Domain/Enums/NoPurchaseReason.cs)

### GMSoft.IntegrationTests

- C: [GMSoft.IntegrationTests/NoPurchaseTests.cs](../GMSoft.IntegrationTests/NoPurchaseTests.cs)

### docs

- M: [docs/customer-activity.md](../docs/customer-activity.md)
- M: [docs/delivery-rules.md](../docs/delivery-rules.md)
- M: [docs/postponed-visits.md](../docs/postponed-visits.md)
- M: [docs/testing.md](../docs/testing.md)
- C: [docs/no-purchase.md](../docs/no-purchase.md)

### tools

- M: [tools/e2e/suite.py](../tools/e2e/suite.py)
