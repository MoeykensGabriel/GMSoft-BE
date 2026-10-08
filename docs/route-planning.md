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
      "vehicleId": "44444444-4444-4444-8444-444444444444"
    }
  ]
}
```

customerIds contiene exactamente todos los IDs leidos, incluso los que salen
de la lista al guardar, en el orden deseado. changes es obligatorio (puede ser
[]); cada cliente aparece como maximo una vez. visitDays y vehicleId omitidos
o null conservan su valor. No permite desasignar el camion.
El ejemplo quita a Ana del lunes y cambia su camion: si era la unica cliente,
responde items: [] y la nueva version del recorrido vacio.

## Reglas y concurrencia

- Un solo RouteOrder por cliente, propio de su zona. Se distribuye el mismo
  conjunto de posiciones actuales, de menor a mayor, entre los IDs enviados.
  No se renumera la zona ni se toca a clientes fuera de la lista.
- Cambiar dias o camion no asigna una posicion al final: conserva la posicion
  resultante del reordenamiento del mismo lote (la anterior si no se reordeno).
  La zona nunca cambia. La edicion individual de clientes conserva su conducta.
- Dias: al menos uno, sin repetidos, enteros 1..7, validacion compartida con
  edicion de clientes. Se guardan ordenados. El camion debe existir.
- Al quitar el dia elegido o cambiar de camion, el cliente sale de la respuesta.
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
