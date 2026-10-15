# Cuenta corriente del cliente

`GET /api/customers/{id}/account?statementLimit=60&movementsLimit=50`

Requiere autenticacion con rol `Admin` o `Driver`. Admin puede consultar cualquier cliente. El chofer conserva el alcance actual: salida abierta, cliente activo del camion, zona y dias de esa salida. Cliente inexistente: 404; fuera del recorrido: 400; chofer sin salida: 409. Limite invalido: 400 de validacion.

## Parametros

| Parametro | Default | Valores | Uso |
| --- | --- | --- | --- |
| statementLimit | 60 | 1 a 200 | Cantidad maxima de renglones, no de pagos ni productos |
| movementsLimit | 50 | 1 a 200 | Limite independiente del campo legado; por compatibilidad, valores <= 0 usan 50 |

No hay paginacion por offset. `statementTotalCount` cuenta todos los renglones antes del recorte. Si supera `statement.length`, existe historial anterior no incluido.

## Respuesta completa de ejemplo

```json
{
  "customerId": "11111111-1111-4111-8111-111111111111",
  "displayName": "Almacen Maria",
  "address": "San Martin 123",
  "phone": "1122334455",
  "zoneName": "Centro",
  "balance": 5000,
  "lastPurchaseAt": "2026-10-14T13:00:00Z",
  "daysWithoutPurchase": 0,
  "containers": [],
  "units": [],
  "movements": [
    { "date": "2026-10-14T13:00:00Z", "type": "Delivery", "amount": 10000, "referenceId": "22222222-2222-4222-8222-222222222222", "notes": null },
    { "date": "2026-10-14T13:00:00Z", "type": "Payment", "amount": 5000, "referenceId": "33333333-3333-4333-8333-333333333333", "notes": null }
  ],
  "lastNoPurchase": null,
  "weeksWithoutPurchase": 0,
  "statementTotalCount": 1,
  "statement": [
    {
      "date": "2026-10-14T13:00:00Z",
      "kind": "Sale",
      "referenceId": "22222222-2222-4222-8222-222222222222",
      "lines": [
        { "productId": "44444444-4444-4444-8444-444444444444", "productDetail": "Bidon 20 lts", "quantity": 2, "unitPrice": 5000, "amount": 10000 }
      ],
      "containersDelivered": [
        { "productId": "44444444-4444-4444-8444-444444444444", "productDetail": "Bidon 20 lts", "quantity": 2 }
      ],
      "containersReturned": [
        { "productId": "44444444-4444-4444-8444-444444444444", "productDetail": "Bidon 20 lts", "quantity": 2 }
      ],
      "containerChanges": [],
      "charge": 10000,
      "payments": [{ "amount": 5000, "method": "Transfer" }],
      "paid": 5000,
      "noPurchaseReason": null,
      "notes": null,
      "balanceAfter": 5000
    }
  ]
}
```

`movements` esta **obsoleto**: se conserva sin cambios para el frontend actual. No combinarlo con `statement` para sumar dinero: representan las mismas operaciones. Los demas campos existentes se conservan. `containers` es el saldo actual por producto (`productId`, `productDetail`, `quantity`); `units` contiene unidades actuales (`containerUnitId`, `productId`, `productDetail`, `serialNumber`). `lastNoPurchase`, si existe, tiene `date`, `reason`, `notes`. Los campos opcionales pueden ser null; las listas siempre son arrays, incluso vacias.

## Contrato de cada renglon

| Campo | Tipo y significado |
| --- | --- |
| date | Fecha/hora ISO 8601 UTC; convertir a hora local para mostrar |
| kind | `Sale`, `ContainerOnly`, `Promotion`, `NoPurchase`, `Payment`, `ContainerAdjustment`, `ContainerLoss` |
| referenceId | UUID de Delivery para visitas, Payment para cobros sueltos, ContainerMovement para ajustes/perdidas, Promotion para promociones convertidas; usar `(kind, referenceId)` como clave |
| lines | Productos: `productId`, `productDetail`, `quantity` entero, `unitPrice` decimal, `amount` decimal = cantidad por precio |
| containersDelivered / containersReturned | Cantidades positivas agrupadas por producto: `productId`, `productDetail`, `quantity`; no son dinero |
| containerChanges | Cambios administrativos: `productId`, `productDetail`, `quantity` con signo, `reason` texto o null para datos historicos incompletos |
| charge | Decimal: total de la visita; cero en NoPurchase, cobros sueltos y ajustes/perdidas |
| payments | Lista de `{ amount, method }`; metodo textual `Cash`, `Transfer` o `Card` |
| paid | Decimal: suma de payments; cero si no hubo cobro |
| noPurchaseReason | null o `NotHome`, `StillHasProduct`, `DidNotWant`, `NoMoney`, `BuysElsewhere`, `Other` |
| notes | Texto o null |
| balanceAfter | Decimal: deuda acumulada despues de ese renglon; negativo indica saldo a favor |

El precio de venta es el guardado al vender, no el precio actual del catalogo. La descripcion del producto proviene del catalogo actual (no existe snapshot historico de la descripcion). Las visitas `ContainerOnly` tienen productos vendidos vacios y muestran sus movimientos de envases. Las promociones historicas de tipo Delivery se muestran como `Promotion`. Las promociones nuevas aparecen solo cuando tienen `CustomerId` (convertidas), en su fecha original `RegisteredAt`, con productos gratuitos y envases prestados originales; no generan cargo ni duplican el traspaso interno de envases al cliente. Promociones de prospectos sin cliente no aparecen.

Los ajustes y perdidas son un renglon por movimiento del libro de envases, con fecha `OccurredAt`. `containerChanges.quantity > 0` aumenta los envases en poder del cliente, `< 0` los reduce; una perdida no es una devolucion. Su motivo aparece en `reason` y `notes`. No generan deuda monetaria.

## Agrupacion de cobros y saldo

Payment no tiene FK a Delivery. El registro de visita escribe el mismo `CustomerId`, `DeliverySessionId` y timestamp (`PaidAt == DeliveredAt`) en ambos registros. Se agrupan todos los pagos que coinciden exactamente con una unica visita no NoPurchase. No se agrupa por proximidad horaria ni solamente por sesion: un cobro de deuda posterior en la misma sesion queda como Payment independiente. Si varias visitas coinciden en ese instante, la asociacion es ambigua y el pago queda separado. Sin sesion o sin coincidencia, tambien queda separado. Cada pago se cuenta una sola vez. Un cobro agrupado puede superar el cargo de la visita si incluye deuda anterior.

El saldo se acumula desde el comienzo: `saldo anterior + charge - paid`. Se calcula **antes** de aplicar `statementLimit`. La lista se devuelve de mas reciente a mas antigua: **`statement[0].balanceAfter == balance`** cuando hay renglones; el ultimo elemento del array es el mas antiguo incluido. Empates de fecha se ordenan de forma determinista por `kind` ordinal y UUID (descendentes al devolver). No inferir una secuencia temporal real entre eventos con el mismo timestamp.

Cuenta vacia: `statement: []`, `statementTotalCount: 0`, `balance: 0`. NoPurchase y cambios de envases conservan el saldo anterior. No reiniciar el acumulado en cero al dibujar una lista limitada. No sumar solamente las ventas visibles para obtener `balance`.

## Variantes para dibujar la tabla

Ejemplos de renglones (los arrays vacios y null se incluyen en la respuesta real):

```json
[
  { "kind": "Sale", "charge": 20000, "payments": [{ "amount": 20000, "method": "Cash" }], "paid": 20000, "balanceAfter": 0 },
  { "kind": "Sale", "charge": 10000, "payments": [], "paid": 0, "balanceAfter": 10000 },
  { "kind": "Sale", "charge": 10000, "payments": [{ "amount": 5000, "method": "Transfer" }], "paid": 5000, "balanceAfter": 5000 },
  { "kind": "Payment", "charge": 0, "payments": [{ "amount": 3000, "method": "Cash" }], "paid": 3000, "notes": "Cobro de deuda", "balanceAfter": 0 },
  { "kind": "NoPurchase", "charge": 0, "payments": [], "paid": 0, "noPurchaseReason": "NotHome", "notes": null, "balanceAfter": 0 },
  { "kind": "ContainerAdjustment", "charge": 0, "paid": 0, "containerChanges": [{ "productId": "44444444-4444-4444-8444-444444444444", "productDetail": "Bidon 20 lts", "quantity": 2, "reason": "Recuento presencial" }], "balanceAfter": 0 },
  { "kind": "ContainerLoss", "charge": 0, "paid": 0, "containerChanges": [{ "productId": "44444444-4444-4444-8444-444444444444", "productDetail": "Bidon 20 lts", "quantity": -1, "reason": "Envase roto" }], "balanceAfter": 0 }
]
```

Estas variantes son independientes, no una secuencia contable. Para la tabla: Fecha desde `date`; Detalle desde `lines`, envases, motivo y notas; Debe desde `charge`; Pago desde cada entrada de `payments` (efectivo / transferencia / tarjeta); Saldo desde `balanceAfter`. Mostrar guion para cargo/pago cero es una decision de presentacion. Traducciones sugeridas: NotHome = No estaba; StillHasProduct = Todavia tiene producto; DidNotWant = No quiso; NoMoney = Sin dinero; BuysElsewhere = Compra en otro lado; Other = Otro (leer notes).

## Implementacion y verificacion

Sin cambios de esquema. Lecturas en lote, sin consultas por visita o producto: entregas con items/envases mediante split queries, pagos, movimientos administrativos y promociones vinculadas. El historial completo se lee para calcular saldo y conteo; el limite recorta la respuesta, no el trabajo de lectura del historial. `balance` se obtiene del mismo calculo que `balanceAfter`.

Pruebas de aplicacion e integracion cubren pagos, cuenta corriente limitada, envases, motivos, promociones y acceso por recorrido. La fixture de integracion usa una base efimera `gmsoft_it_*` y la elimina al terminar. No requiere levantar la API.
