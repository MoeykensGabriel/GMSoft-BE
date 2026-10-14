# Horario laboral de la empresa

Contrato backend de la tarea 11. JSON camelCase, roles del token `Admin` y `Driver`.
Un solo horario para toda la empresa. La configuración de promociones conserva
sus endpoints actuales: GET/PUT `/api/promotions/settings` (Admin); la futura
pantalla Configuración puede reunir ambas secciones.

## Configuración

| Verbo | Ruta | Acceso | Respuesta |
| --- | --- | --- | --- |
| GET | /api/working-hours | Cualquier usuario autenticado | 200 WorkingHoursDto |
| PUT | /api/working-hours | Admin | 200 WorkingHoursDto |

Sin configuración, GET devuelve exactamente:

```json
{ "isConfigured": false, "days": [] }
```

Leer no crea una configuración. Sin ella no se restringe ninguna operación y
las nuevas marcas quedan en false. No se siembra un horario por defecto.

PUT reemplaza la semana completa, atómicamente. Cuerpo de ejemplo:

```json
{
  "days": [
    { "dayOfWeek": 1, "isWorkingDay": true, "from": "08:00:00", "until": "18:00:00", "openingCutoff": "16:00:00" },
    { "dayOfWeek": 2, "isWorkingDay": true, "from": "08:00:00", "until": "18:00:00", "openingCutoff": null },
    { "dayOfWeek": 3, "isWorkingDay": true, "from": "08:00:00", "until": "18:00:00", "openingCutoff": null },
    { "dayOfWeek": 4, "isWorkingDay": true, "from": "08:00:00", "until": "18:00:00", "openingCutoff": null },
    { "dayOfWeek": 5, "isWorkingDay": true, "from": "08:00:00", "until": "18:00:00", "openingCutoff": "16:00:00" },
    { "dayOfWeek": 6, "isWorkingDay": true, "from": "08:00:00", "until": "13:00:00", "openingCutoff": null },
    { "dayOfWeek": 7, "isWorkingDay": false, "from": null, "until": null, "openingCutoff": null }
  ]
}
```

La respuesta del PUT y de posteriores GET tiene `isConfigured: true` y el mismo
array `days`, ordenado de lunes a domingo. Se conserva `openingCutoff: null` si
no se indicó: su valor efectivo es `until`.

Validaciones (400 con `errors` por campo):

- Exactamente siete días ISO, lunes=1 a domingo=7, sin duplicados ni entradas nulas.
- Día laborable: `from` y `until` obligatorios, `from < until`.
- `openingCutoff` opcional/null; si se indica, `from <= openingCutoff <= until`.
- Día no laborable: las tres horas deben ser null u omitidas.
- Horas locales sin fecha ni offset, formato `HH:mm:ss`, precisión de minutos
  (segundos y fracciones deben ser cero). El selector del front puede trabajar
  con `HH:mm` y enviar `:00`. No se admiten turnos que crucen medianoche ni 24:00.
- No existe DELETE/reset: una semana con siete días no laborables bloquea las
  operaciones restringidas todos los días; no significa “sin configuración”.

Es una única fila (Id=1, garantizado en PostgreSQL). Dos PUT concurrentes no
mezclan días ni crean filas adicionales: queda la última semana guardada.

## Hora y regla

La hora la toma el servidor mediante `TimeProvider`, convertida por `BusinessTime`
a Argentina (UTC-3 fijo). Nunca se toma del dispositivo ni del cuerpo del pedido.
Timestamps persistidos y respuestas de operaciones: UTC con precisión de
microsegundos; mostrar en `America/Argentina/Buenos_Aires`.

Intervalo laboral: **desde inclusive, hasta exclusive**. Ejemplo 08:00–18:00:
08:00 entra, 18:00 ya está fuera. Para abrir una salida se exige además no haber
pasado `openingCutoff`: el instante exacto del límite entra si sigue dentro del
horario general. Con límite 16:00, 16:00:00 entra y 16:00:01 no. Cuando el límite
es igual a `until`, prevalece el fin exclusivo del horario general.

El día de semana también se calcula en Argentina: lunes 02:59Z todavía es domingo.
La marca de apertura considera tanto el horario general como el límite de apertura.
Las demás operaciones consideran únicamente el horario general.

| Operación | Fuera de horario |
| --- | --- |
| Carga inicial | Bloqueada; Admin puede confirmar excepción |
| Apertura de salida | Bloqueada; Admin puede confirmar excepción |
| Recarga | Bloqueada; Admin puede confirmar excepción |
| Venta, No compró y retiro de envases | Se registra y se marca |
| Registro y cierre de promoción | Se registra y se marca por separado |
| Recepción/cierre del camión | Se registra y se marca |
| Rendición de efectivo (liquidación) | Se registra y se marca |

“Se registra” conserva todas las otras validaciones existentes: stock, salida
abierta, permisos, cantidades, etc. El horario nunca bloquea estos últimos cinco
circuitos. Las consultas de liquidación no registran operaciones nuevas.

## Rechazo y confirmación explícita

Los tres comandos restringidos agregan `confirmOutsideWorkingHours`, booleano
opcional, false por defecto. Nunca enviar true automáticamente. Ante el rechazo,
ADMIN puede revisar el aviso y reenviar el mismo pedido confirmándolo. El chofer
no puede saltear el bloqueo, aunque envíe true.

Ejemplo de error:

```json
{
  "type": "outside-working-hours",
  "title": "Conflict",
  "status": 409,
  "detail": "Fuera del horario laboral. Lunes: 08:00 a 18:00; límite para abrir salidas: 16:00 (Argentina, UTC-3).",
  "instance": "/api/sessions/open",
  "traceId": "identificador-de-la-solicitud"
}
```

En día no laborable, `detail` dice: “Fuera del horario laboral. Domingo: no
laborable (Argentina, UTC-3).” El front debe comparar `type`, no analizar el texto.
Otros 409 siguen siendo conflictos de negocio sin este type. No ofrecer override
para ellos. El rechazo no guarda la operación ni consume el clientRequestId.

401 sin autenticar; 403 rol no permitido/chofer ajeno; 400 cuerpo o configuración
inválidos; 404 recurso inexistente. Se conserva ProblemDetails y `errors` de
validación del backend. Los 401/403 de autorización pueden no tener cuerpo.

### Carga inicial

POST `/api/vehicles/{vehicleId}/load` (Admin), mismo cuerpo y respuesta anteriores,
agregando el flag:

```json
{
  "clientRequestId": "11111111-1111-4111-8111-111111111111",
  "routeDays": [1],
  "items": [{ "productId": "22222222-2222-4222-8222-222222222222", "quantity": 5 }],
  "confirmOutsideWorkingHours": true
}
```

Respuesta 200 `{ "loadedAt": "2026-10-12T22:00:00Z" }`. Cada renglón de carga
persiste su marca y `registeredByUserId`. El flag no abre una salida ni autoriza
una apertura futura.

### Apertura

POST `/api/sessions/open` acepta ahora Driver y Admin. El chofer sigue enviando
zona y kilometraje y usa su asignación de vehículo. **Solo Admin** puede enviar
`driverId` para abrir la salida de ese chofer, usando el vehículo asignado a él:

```json
{
  "zoneId": "33333333-3333-4333-8333-333333333333",
  "kilometersAtOpen": 100,
  "driverId": "44444444-4444-4444-8444-444444444444",
  "confirmOutsideWorkingHours": true
}
```

Para Admin sin perfil de chofer, `driverId` es necesario; sin él recibe 403.
Driver debe omitirlo (o null): enviarlo recibe 403 incluso si es su propio ID.
Se mantienen chofer activo, carga previa, vehículo asignado, zona, kilometraje y
una sola salida abierta. Respuesta 201, UUID de la salida y Location del detalle.
`openedByUserId` guarda al usuario autenticado; `driverId` de la salida identifica
al chofer, que puede ser distinto del usuario que la abrió. Este endpoint no
incorpora clientRequestId: ante respuesta incierta consultar la salida existente.

### Recarga

POST `/api/sessions/{id}/restocks` (Admin), contrato de [restocks.md](restocks.md):

```json
{
  "clientRequestId": "55555555-5555-4555-8555-555555555555",
  "items": [{ "productId": "22222222-2222-4222-8222-222222222222", "quantity": 3 }],
  "notes": "En ruta",
  "confirmOutsideWorkingHours": true
}
```

Respuesta 200 SessionRestockDto anterior más `isOutsideWorkingHours: true`.
Conserva `registeredByUserId`, el ID, hora y productos congelados de la tanda.

Confirmar dentro de horario no produce una marca: el booleano persistido expresa
el resultado de la evaluación, no el valor enviado por el cliente.

## Reintentos

Un clientRequestId ya registrado devuelve la operación original sin leer de nuevo
el horario, sin volver a bloquear y sin cambiar su marca, fecha o autor. Conservar
el UUID ante respuesta incierta o al confirmar la excepción tras 409.

Aplica a carga, recarga, visitas y registro/cierre de promociones. Se conserva el
alcance de cada clave y las reglas de propiedad existentes; los reintentos de
visitas requieren el chofer original y también funcionan tras recibir la salida.
Como antes, `saldoCuentaCliente` es el saldo actual y las promociones devuelven su
estado actual. Una corrección de No compró con clave nueva actualiza la misma
visita, su fecha y su marca; reintentar una clave anterior no revierte la corrección.
Una clave nueva siempre representa una operación/corrección nueva.

## Campos nuevos en respuestas existentes

Todos son aditivos. Las marcas son booleanos persistidos, no cálculos contra la
configuración vigente. No se borra ni reinterpreta el historial al editar horarios.
Los registros anteriores a la migración quedan con false (sin marca histórica).

| Consulta/respuesta | Campos nuevos |
| --- | --- |
| GET `/api/vehicles/{vehicleId}/load` (cada renglón pendiente) | `isOutsideWorkingHours` |
| POST restocks y `restocks[]` del detalle/current | `isOutsideWorkingHours` |
| GET `/api/sessions/{id}`, `/api/sessions/current` | `openedOutsideWorkingHours`, `closedOutsideWorkingHours`, `settlementOutsideWorkingHours`, `openedByUserId`, `closedByUserId`, `outsideWorkingHoursEvents` |
| GET `/api/sessions` (`items[]`) | Los mismos campos; la lista de eventos solo incluye apertura/cierre porque el listado no carga el historial |
| GET `/api/sessions/{id}/deliveries` (cada visita) | `isOutsideWorkingHours` |
| GET `/api/sessions/daily-summary` (`sessions[]`) | Los mismos seis campos del detalle, con historial completo |
| GET/POST `/api/sessions/{id}/settlement` | `isOutsideWorkingHours` de la rendición |
| GET promociones: listado `items[]`, detalle y pending; POST registro/cierre | `isOutsideWorkingHours` del registro y `closedOutsideWorkingHours` del cierre |

`closedOutsideWorkingHours` es false mientras no se recibió/cerró; comprobar
`closedAt` para distinguir ausencia de cierre de cierre dentro de horario.
`settlementOutsideWorkingHours` es false sin rendición; comprobar `receivedAt`
en Resumen diario/rendición para distinguirla. En el listado paginado de salidas
no se carga la rendición: usar detalle o resumen para su marca definitiva.
Los IDs de autores nuevos pueden ser null en registros históricos.

El detalle y cada bloque del Resumen diario agregan el mismo array
`outsideWorkingHoursEvents`, únicamente con operaciones marcadas. Ejemplo parcial
de los campos agregados, para una apertura normal y recarga/recepción tardías:

```json
{
  "openedOutsideWorkingHours": false,
  "closedOutsideWorkingHours": true,
  "settlementOutsideWorkingHours": false,
  "openedByUserId": "66666666-6666-4666-8666-666666666666",
  "closedByUserId": "77777777-7777-4777-8777-777777777777",
  "outsideWorkingHoursEvents": [
    {
      "operation": "restock",
      "id": "88888888-8888-4888-8888-888888888888",
      "occurredAt": "2026-10-12T22:00:00Z",
      "userId": "77777777-7777-4777-8777-777777777777",
      "driverId": null
    },
    {
      "operation": "session-close",
      "id": "99999999-9999-4999-8999-999999999999",
      "occurredAt": "2026-10-12T23:00:00Z",
      "userId": "77777777-7777-4777-8777-777777777777",
      "driverId": null
    }
  ]
}
```

Valores estables de `operation`:

| Valor | ID referido | Autor |
| --- | --- | --- |
| initial-load | Renglón VehicleLoad (una tanda puede tener varios) | userId |
| session-open | Salida | userId y driverId |
| restock | Tanda SessionRestock | userId |
| visit | Visita (venta, No compró o retiro) | driverId |
| promotion | Promoción registrada en la salida | driverId |
| promotion-close | Promoción cerrada en esta salida, aunque se haya registrado en otra | driverId |
| session-close | Salida recibida | userId |
| settlement | Rendición SessionCashSettlement | userId |

Orden: occurredAt, operation e id ascendentes. Array vacío sin marcas. Usar
`operation` + `id` como clave (una promoción o salida puede aparecer dos veces).
El Resumen diario sigue agrupando por día argentino de apertura, aunque cierre
al día siguiente. `dayTotals` y los cálculos de dinero/stock no cambian. El front
puede mostrar un distintivo general si el array no está vacío y desglosarlo por
operación; no debe inferir que todo el recorrido fue fuera de horario porque su
apertura lo fue, ni ocultar marcas de visitas/cargas si la apertura fue normal.

## Persistencia y verificación

Migración aditiva `20261014010539_WorkingHours`: tabla WorkingHoursSettings con
semana JSONB, booleanos false por defecto y autores nullable de apertura/cierre.
No tiene seed ni modificaciones destructivas en Up. Generada sin aplicarla a la
base de desarrollo. Los tests de integración migran exclusivamente su base
efímera `gmsoft_it_*` y la eliminan al terminar; no levantan API.

Tests de aplicación: límites, UTC-3 y cambio de día, no laborables, límite opcional,
autorización y confirmación explícita, validación semanal, ausencia de configuración
y reintento de recarga sin consultar horario. Integración: configuración concurrente,
carga, apertura, recarga, visitas, promociones, recepción y rendición con marcas
persistidas; detalle y Resumen diario; reintentos después del cierre y del cambio
de configuración. El contrato ProblemDetails y metadatos de autorización se prueban
con el middleware/controladores reales sin servidor HTTP.

```powershell
dotnet build -c Release
dotnet test -c Release
```

No se agrega horario a `tools/e2e`. Esa suite HTTP requiere API y no forma parte
de la verificación de esta tanda de backend sin servidor.
