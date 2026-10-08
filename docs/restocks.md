# Recargas durante el reparto

ADMIN registra una tanda de llenos sobre la salida abierta. Sigue siendo la misma
salida, aunque el camion vuelva al deposito. Roles del token: `Admin` y `Driver`.

| Verbo | Ruta | Rol |
| --- | --- | --- |
| POST | /api/sessions/{id}/restocks | Admin |
| GET | /api/sessions/{id} | Admin; Driver solo su propia salida |
| GET | /api/sessions/current | Driver, solo su salida abierta |

Respuestas 200, JSON camelCase. El POST reemplaza a POST /api/sessions/{id}/stock:
la ruta vieja se retira, no queda otro camino de registro. No hay endpoints para
editar ni anular recargas. Un error se corrige en la recepcion final.

## Registrar

POST /api/sessions/11111111-1111-4111-8111-111111111111/restocks:

```json
{
  "clientRequestId": "22222222-2222-4222-8222-222222222222",
  "items": [
    { "productId": "33333333-3333-4333-8333-333333333333", "quantity": 3 },
    { "productId": "44444444-4444-4444-8444-444444444444", "quantity": 2 }
  ],
  "notes": "En ruta"
}
```

La salida se toma de la ruta; no hace falta mandar `id` en el cuerpo. UUID obligatorio
y distinto de cero para `clientRequestId`. Items obligatorio, al menos uno, sin
productos repetidos; cantidades enteras > 0. Notes opcional (null o hasta 500
caracteres), se guarda sin espacios en los extremos.

Respuesta de registro y reintento:

```json
{
  "id": "55555555-5555-4555-8555-555555555555",
  "sessionId": "11111111-1111-4111-8111-111111111111",
  "clientRequestId": "22222222-2222-4222-8222-222222222222",
  "occurredAt": "2026-10-08T15:30:00Z",
  "registeredByUserId": "66666666-6666-4666-8666-666666666666",
  "notes": "En ruta",
  "items": [
    { "productId": "33333333-3333-4333-8333-333333333333", "productDetail": "Bidon 20 litros", "quantity": 3 },
    { "productId": "44444444-4444-4444-8444-444444444444", "productDetail": "Agua 2 litros", "quantity": 2 }
  ]
}
```

`registeredByUserId` identifica al ADMIN autenticado. La hora la pone el servidor
una sola vez para toda la tanda, UTC con precision de microsegundos. El frontend
la muestra en America/Argentina/Buenos_Aires (BusinessTime, UTC-3): 15:30Z es 12:30.
El detalle del producto se congela al registrar; los items se ordenan por productId.

## Consultar y avisar al chofer

GET /api/sessions/{id} y GET /api/sessions/current agregan `restocks` al DTO de
salida existente. Es la lista completa de sus recargas, de la mas antigua a la mas
reciente (`occurredAt`, luego `id` para desempatar). ADMIN la consulta desde el
detalle, incluso despues del cierre. No necesita otro endpoint de listado.

Ejemplo completo de cualquiera de estas dos respuestas con una salida abierta,
carga inicial de 5 bidones, la recarga anterior y ninguna venta:

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "driverId": "77777777-7777-4777-8777-777777777777",
  "driverName": "Juan Perez",
  "vehicleId": "88888888-8888-4888-8888-888888888888",
  "vehicleName": "Camion 1",
  "vehicleLicensePlate": "AA123BB",
  "zoneId": "99999999-9999-4999-8999-999999999999",
  "zoneName": "Centro",
  "openedAt": "2026-10-08T11:00:00Z",
  "closedAt": null,
  "kilometersAtOpen": 100,
  "kilometersAtClose": null,
  "status": "Open",
  "stock": [
    { "productId": "44444444-4444-4444-8444-444444444444", "productDetail": "Agua 2 litros", "fullOnBoard": 2, "emptyOnBoard": 0 },
    { "productId": "33333333-3333-4333-8333-333333333333", "productDetail": "Bidon 20 litros", "fullOnBoard": 8, "emptyOnBoard": 0 }
  ],
  "routeDays": [4],
  "deferredCustomerIds": [],
  "restocks": [
    {
      "id": "55555555-5555-4555-8555-555555555555",
      "sessionId": "11111111-1111-4111-8111-111111111111",
      "clientRequestId": "22222222-2222-4222-8222-222222222222",
      "occurredAt": "2026-10-08T15:30:00Z",
      "registeredByUserId": "66666666-6666-4666-8666-666666666666",
      "notes": "En ruta",
      "items": [
        { "productId": "33333333-3333-4333-8333-333333333333", "productDetail": "Bidon 20 litros", "quantity": 3 },
        { "productId": "44444444-4444-4444-8444-444444444444", "productDetail": "Agua 2 litros", "quantity": 2 }
      ]
    }
  ]
}
```

Sin recargas: `restocks: []`. Sin salida abierta: current produce null y el
formateador MVC predeterminado responde 204 sin cuerpo (comportamiento existente).
El chofer refresca current y compara los IDs con los ya vistos para mostrar
«Te recargaron 3 × Bidon 20 litros a las 12:30». No hay marca de leido, push ni
acuse en el servidor. Usar estas dos consultas para recargas: el listado paginado
GET /api/sessions no carga el historial (`restocks` queda vacio alli).

## Reglas y reintentos

- Solo salida abierta y productos existentes, no eliminados y publicados. La carga
  inicial no rechaza ByUnit; la recarga tampoco. No asigna numeros de serie.
- Solo suma llenos. No registra vacios, ventas, deuda ni cobros, ni valida deposito.
  Los vacios se cuentan juntos en la recepcion final, aunque se descarguen antes.
- Cabecera SessionRestock y movimientos Restock se guardan en una transaccion.
  Los movimientos son los renglones: no hay una segunda copia de las cantidades.
  Se bloquea la fila de la salida y se revisa su estado dentro de la transaccion
  para no registrar una recarga despues de un cierre concurrente.
- Repetir clientRequestId en esa salida devuelve la original, aun despues del
  cierre o si cambiaron las cantidades/notas enviadas. No modifica lo registrado.
  En otra salida da 409. Un indice unico global en SessionRestocks y la recuperacion
  de la operacion ganadora resuelven envios simultaneos sin duplicar stock.
- El frontend genera el UUID una vez al confirmar y lo conserva ante respuesta
  incierta. Un UUID nuevo significa otra recarga. Abrir/cancelar el modal no envia
  el POST; durante el envio se bloquea otra confirmacion.
- Stock y faltante siguen sumando el libro mayor: carga inicial + recargas -
  ventas - descarga final (y los demas movimientos existentes). La liquidacion
  monetaria y el detalle por cliente ya usan ventas/cobros de la misma salida;
  una recarga no es una venta y no agrega dinero.
- Migracion SessionRestocks: tabla, dos columnas nullable en movimientos, indices
  y relaciones. No modifica datos anteriores. Los Restock historicos sin cabecera
  siguen contando en stock/cierre; no se inventan tandas ni avisos para ellos.

## Errores

400: cuerpo invalido, UUID omitido/vacio/mal formado, lista vacia/nula, renglones
nulos, producto repetido, cantidad no entera o <= 0, notas largas o producto no
publicado. 401: sin autenticar. 403: rol no permitido o salida de otro chofer.
404: salida o producto inexistente/eliminado. 409: salida cerrada para una nueva
recarga, o clientRequestId usado en otra salida. Errores de negocio usan el
ProblemDetails habitual; errores de validacion agregan `errors` por campo.

Ejemplo de 409 (traceId e instance corresponden a cada solicitud):

```json
{
  "title": "Conflict",
  "status": 409,
  "detail": "La sesion ya esta cerrada. Una recarga posterior cambiaria el faltante ya informado.",
  "instance": "/api/sessions/11111111-1111-4111-8111-111111111111/restocks",
  "traceId": "00-11111111111111111111111111111111-2222222222222222-01"
}
```
