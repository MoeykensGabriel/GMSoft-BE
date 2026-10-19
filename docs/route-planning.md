# Organizar recorridos

ADMIN elige camion, zona y dia ISO (lunes=1, domingo=7). Solo incluye clientes
activos, no eliminados, asignados a ese camion y zona y con ese dia de visita.
Lista completa, sin paginar, ordenada por RouteOrder e Id. JSON camelCase.

| Verbo | Ruta | Rol |
| --- | --- | --- |
| GET | /api/route-planning?vehicleId={uuid}&zoneId={uuid}&day={1..7} | Admin |
| PUT | /api/route-planning | Admin |

## Consultar y guardar

GET /api/route-planning?vehicleId=11111111-1111-4111-8111-111111111111&zoneId=22222222-2222-4222-8222-222222222222&day=1

Respuesta 200 de GET y PUT (misma forma; version es un SHA-256 opaco, copiar
siempre el valor recibido, no calcularlo en el cliente):

```json
{
  "items": [
    {
      "id": "33333333-3333-4333-8333-333333333333",
      "displayName": "Ana",
      "address": "Calle 123",
      "phone": "3815551234",
      "zoneId": "22222222-2222-4222-8222-222222222222",
      "zoneName": "Centro",
      "routeOrder": 6,
      "visitDays": [1, 4],
      "vehicleId": "11111111-1111-4111-8111-111111111111",
      "vehicleName": "Camion 1",
      "vehicleLicensePlate": "AA123BB"
    }
  ],
  "version": "B8A13CC1BF9360C385CCB3C91CAB11382F55CB28F3527B061975241180990C15"
}
```

PUT /api/route-planning, usando la version recibida:

```json
{
  "vehicleId": "11111111-1111-4111-8111-111111111111",
  "zoneId": "22222222-2222-4222-8222-222222222222",
  "day": 1,
  "version": "B8A13CC1BF9360C385CCB3C91CAB11382F55CB28F3527B061975241180990C15",
  "customerIds": ["33333333-3333-4333-8333-333333333333"],
  "changes": [
    {
      "id": "33333333-3333-4333-8333-333333333333",
      "visitDays": [4],
      "vehicleId": "44444444-4444-4444-8444-444444444444",
      "zoneId": "55555555-5555-4555-8555-555555555555"
    }
  ]
}
```

customerIds contiene exactamente todos los IDs leidos, incluso los que salen
de la lista al guardar, en el orden deseado. changes es obligatorio (puede ser
[]); cada cliente aparece como maximo una vez. visitDays, vehicleId y zoneId omitidos
o null conservan su valor. No permite desasignar el camion.
El ejemplo quita a Ana del lunes y cambia su camion y zona: si era la unica cliente,
responde items: [] y la nueva version del recorrido vacio.

## Reglas y concurrencia

- Un solo RouteOrder por cliente, propio de su zona. Se distribuye el mismo
  conjunto de posiciones actuales, de menor a mayor, entre los IDs enviados.
  No se renumera la zona ni se toca a clientes fuera de la lista.
- Cambiar dias o camion no asigna una posicion al final: conserva la posicion
  resultante del reordenamiento del mismo lote (la anterior si no se reordeno).
  changes[].zoneId opcional permite cambiar de zona (null u omitido conserva la actual).
  Si cambia, pasa al final de la zona nueva usando GetNextRouteOrderAsync; varios
  traslados a esa zona reciben posiciones consecutivas, en el orden de changes.
  La zona destino debe existir; UUID vacio es invalido. La edicion individual conserva su conducta.
- Dias: al menos uno, sin repetidos, enteros 1..7, validacion compartida con
  edicion de clientes. Se guardan ordenados. El camion debe existir.
- Al quitar el dia elegido o cambiar de camion o zona, el cliente sale de la respuesta.
- Version incluye filtros, datos visibles y UpdatedAt de todos los integrantes.
  Un alta desde la calle, baja, cambio de pertenencia o edicion invalida la lista.
  Incluso una visita que actualice UpdatedAt pide recargar: decision conservadora.
- Verificacion y guardado se ejecutan en una transaccion. Un bloqueo PostgreSQL
  SHARE ROW EXCLUSIVE sobre Customers impide altas y escrituras concurrentes
  durante ese tramo, incluidos flujos existentes que no toman locks explicitos.
  Las lecturas siguen disponibles; las escrituras de otras zonas tambien esperan
  hasta commit/rollback. Es deliberadamente conservador y no requiere migracion.
- Todos los recursos y cambios se validan antes de modificar entidades. Ante un
  error no se aplica ninguna parte del lote. El PUT no es idempotente: si se pierde
  su respuesta y se reintenta con una version vieja, puede devolver 409; recargar.

## Errores

400: filtros invalidos, version vacia, arrays omitidos/nulos, cambios repetidos,
renglones nulos, dias invalidos, camion vacio o cambios de un cliente existente
fuera de la lista. 401: sin autenticar. 403: rol distinto de Admin.
404: camion, zona o cliente inexistente/eliminado (se comprueba antes del conjunto).
409: IDs repetidos, faltantes o sobrantes respecto del conjunto actual, o version
distinta. Una lista vacia admite customerIds: [] y changes: [].

ProblemDetails habitual; validaciones agregan errors por campo. Ejemplo de 409
(instance y traceId corresponden a la solicitud):

```json
{
  "title": "Conflict",
  "status": 409,
  "detail": "El recorrido cambió. Recargá el recorrido antes de guardar.",
  "instance": "/api/route-planning",
  "traceId": "00-11111111111111111111111111111111-2222222222222222-01"
}
```

La agenda es orientativa: consultar POST /api/route-planning/coverage antes de guardar
y GET /api/route-planning/uncovered para detectar dias sin cubrir. Nunca bloquea este PUT.
Contrato completo y ejemplos: [vehicle-schedule.md](vehicle-schedule.md).

## Orden desde la salida del chofer

`GET /api/sessions/{id}/route-order` y `PUT /api/sessions/{id}/route-order`
requieren rol Driver y que el chofer sea dueño de esa salida abierta. No hay
excepción para ADMIN: la oficina sigue usando `/api/route-planning`.

El PUT recibe `{ "customerIds": ["uuid", "uuid"], "version": "SHA-256" }`.
GET y PUT devuelven `items` y `version`, con el mismo formato de clientes de arriba.
El alcance sale exclusivamente de VehicleId, ZoneId y RouteDays de la salida;
para salidas antiguas sin días, se usa el día ISO de apertura en Argentina.
Se incluyen todos los clientes de la hoja, visitados o no: activos, no eliminados,
sin baja pendiente, de ese camión y zona, con algún día de la salida. Un cliente
de varios días aparece una sola vez. No se modifica «Visitar más tarde».

La lista completa se redistribuye entre sus posiciones actuales con la misma
lógica y bloqueo de Customers de la oficina. Además se bloquea la salida para
que no pueda cerrarse durante el guardado. El orden persiste para próximas
salidas y es único por cliente, también cuando tiene varios días de visita.
La versión usa el mismo SHA-256; con un único día coincide con la de la oficina.
Para varios días incluye el conjunto de días ordenado. Una visita o edición que
actualiza un cliente también invalida la versión, igual que en la oficina.

Errores: 400 para versión vacía o lista nula; 404 para salida inexistente;
403 para salida de otro chofer; 409 para salida cerrada o versión/conjunto de
clientes desactualizado (incluidos IDs ajenos, repetidos o faltantes).
El conflicto de versión/conjunto dice:
«El recorrido cambió mientras ordenabas. Volvé a cargarlo.»
El celular recarga ante 409. Sin señal conserva el borrador para reintentar:
este PUT no pasa por la bandeja de envíos.

## Registro visible para la oficina

La migración `DriverRouteOrder` agrega `RouteOrderChanges`: camión, zona,
salida opcional, usuario, fecha UTC y cantidad de clientes cuya posición ordinal
cambió. Se registra en la misma transacción que el orden. Un guardado del chofer
sin cambios no genera registro. El PUT de oficina también registra sus cambios,
incluidos cambios de asignación (que pueden tener `movedCount: 0`).

GET/PUT de oficina agregan `lastChange`, nulo si no hay antecedentes:
`{ "changedAt": "2026-10-19T18:30:00Z", "changedByUserName": "Matías", "movedCount": 3 }`.
Es el último cambio del camión y zona, independientemente del día, porque los
clientes tienen un solo orden. El nombre se resuelve con Identity; puede ser
nulo si el usuario ya no está disponible.
El detalle `GET /api/sessions/{id}` agrega `driverRouteOrderChange` con esa misma
forma: el último cambio hecho por el chofer en esa salida, o null si no reordenó.
