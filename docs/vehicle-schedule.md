# Agenda por camión — contrato para frontend

La agenda es una guía semanal, nunca una restricción para guardar clientes,
organizar recorridos o abrir salidas. JSON camelCase; UUID para identificadores;
autenticación Bearer. Días ISO: lunes 1, martes 2, miércoles 3, jueves 4,
viernes 5, sábado 6, domingo 7. Una zona puede repetirse varios días.

## Consultar agenda

`GET /api/vehicles/{id}/schedule` — Admin o chofer activo asignado al camión.
Respuesta 200:

```json
{
  "vehicleId": "11111111-1111-4111-8111-111111111111",
  "isConfigured": true,
  "isSuggested": false,
  "days": [
    {"day": 1, "zoneId": "22222222-2222-4222-8222-222222222222", "zoneName": "Centro", "zones": [{"zoneId": "22222222-2222-4222-8222-222222222222", "zoneName": "Centro"}]},
    {"day": 2, "zoneId": null, "zoneName": null, "zones": []},
    {"day": 3, "zoneId": null, "zoneName": null, "zones": []},
    {"day": 4, "zoneId": "22222222-2222-4222-8222-222222222222", "zoneName": "Centro", "zones": [{"zoneId": "22222222-2222-4222-8222-222222222222", "zoneName": "Centro"}]},
    {"day": 5, "zoneId": null, "zoneName": null, "zones": []},
    {"day": 6, "zoneId": null, "zoneName": null, "zones": []},
    {"day": 7, "zoneId": null, "zoneName": null, "zones": []}
  ]
}
```

Siempre devuelve siete días ordenados. Cada día incluye `zones: [{zoneId, zoneName}]`
en el orden de visita. `zoneId` y `zoneName` conservan la primera zona para compatibilidad.
Una lista `zones: []` en una agenda guardada significa
que ese día no sale. Sin filas guardadas: `isConfigured:false`, `isSuggested:true`
y la misma estructura con una sugerencia del historial, sin escribir datos.
Sin historial elegible, los siete días tienen zona null; no significa una semana
confirmada sin salidas. Guardar explícitamente siete listas vacías sí configura esa semana.

Historial: salidas no eliminadas abiertas entre ahora menos 56 días y ahora (UTC),
con zona vigente y activa, de ese camión. Cada salida aporta un voto por cada
`routeDays`; las antiguas sin días usan el día de apertura en Argentina (UTC−3).
Gana la zona más frecuente por día; empate: apertura más reciente y luego UUID
ascendente. No usa clientes como sustituto ni guarda la inferencia automáticamente.

## Reemplazar semana

`PUT /api/vehicles/{id}/schedule` — solo Admin. Cuerpo:

```json
{
  "days": [
    {"day": 1, "zoneId": null, "zoneIds": ["22222222-2222-4222-8222-222222222222"]},
    {"day": 2, "zoneId": null, "zoneIds": []},
    {"day": 3, "zoneId": null, "zoneIds": []},
    {"day": 4, "zoneId": null, "zoneIds": ["22222222-2222-4222-8222-222222222222"]},
    {"day": 5, "zoneId": null, "zoneIds": []},
    {"day": 6, "zoneId": null, "zoneIds": []},
    {"day": 7, "zoneId": null, "zoneIds": []}
  ]
}
```

Cada día acepta `zoneIds` ordenadas, sin repetir, hasta 5; `[]` significa que no sale.
El frontend manda `zoneId: null` junto a `zoneIds`. El campo singular sigue aceptándose
para clientes viejos. Requiere exactamente siete días diferentes 1..7; zonas existentes, no eliminadas
y activas; no admite UUID vacío. Respuesta 200: la misma forma que GET, configurada
y no sugerida. Reemplazo transaccional bajo bloqueo del camión; dos escrituras
simultáneas se serializan y la última reemplaza la semana completa. No hay PATCH
ni endpoint para volver al estado sin configuración. No altera clientes ni salidas.

## Zona sugerida al abrir salida

`GET /api/sessions/suggested-zone` — chofer: su camión.
`GET /api/sessions/suggested-zone?driverId={uuid}` — Admin: driverId obligatorio.
El chofer no puede enviar driverId, ni siquiera el propio. Respuesta 200:

```json
{
  "vehicleId": "11111111-1111-4111-8111-111111111111",
  "zoneId": "22222222-2222-4222-8222-222222222222",
  "zoneName": "Centro",
  "zones": [{"zoneId": "22222222-2222-4222-8222-222222222222", "zoneName": "Centro"}],
  "isConfigured": true,
  "isSuggested": false,
  "routeDays": [1, 4],
  "days": [
    {"day": 1, "zoneId": "22222222-2222-4222-8222-222222222222", "zoneName": "Centro", "zones": [{"zoneId": "22222222-2222-4222-8222-222222222222", "zoneName": "Centro"}]},
    {"day": 4, "zoneId": "33333333-3333-4333-8333-333333333333", "zoneName": "Norte", "zones": [{"zoneId": "33333333-3333-4333-8333-333333333333", "zoneName": "Norte"}]}
  ]
}
```

Combina los días de todas las cargas pendientes, sin repetir y en orden ISO.
Si hay carga antigua sin días, usa hoy en Argentina, igual que OpenSession.
Sin carga pendiente devuelve `routeDays:[]`, `days:[]`, zona y nombre null.
La respuesta agrega `zones: [{zoneId, zoneName}]` en el orden sugerido; los campos
singulares representan la primera zona. Cada elemento de `days` también trae `zones`.
Si no hay agenda, usa la sugerencia histórica y marca `isSuggested:true`, `isConfigured:false`.

El frontend precarga la lista y permite agregar zonas activas, quitar y subir/bajar
con botones. Muestra un aviso si cambia alguna zona o el orden respecto de la sugerencia.
`POST /api/sessions/open` recibe `zoneIds: ["uuid", "uuid"]`, al menos una y hasta 5,
sin repetir y en orden. Ya no hace falta mandar `zoneId`; sigue aceptándose para clientes viejos.
La agenda orienta: no es obligatorio coincidir con la sugerencia.
Esta consulta no reserva la carga; otra operación puede modificarla después.

## Consultar cobertura antes de guardar

`POST /api/route-planning/coverage` — solo Admin. Cuerpo:

```json
{
  "vehicleId": "11111111-1111-4111-8111-111111111111",
  "zoneId": "22222222-2222-4222-8222-222222222222",
  "visitDays": [1, 4]
}
```

Respuesta 200 (ejemplo de coincidencia parcial):

```json
{
  "covered": false,
  "days": [
    {"day": 1, "scheduledZoneId": "22222222-2222-4222-8222-222222222222", "scheduledZoneName": "Centro", "matches": true},
    {"day": 4, "scheduledZoneId": null, "scheduledZoneName": null, "matches": false}
  ]
}
```

`covered:true` solo si la zona del cliente está entre las zonas de cada día; `false` si alguno no coincide,
incluido un día sin salida. Sin agenda guardada, `covered:null` y `matches:null`
en cada día (ambos datos de zona null), aunque exista una sugerencia histórica.
Días requeridos: 1..7, al menos uno y sin repetir. Comprueba existencia del camión;
compara el UUID de zona recibido, sin validar su existencia (el guardado sí lo hace).

No guarda nada y no bloquea ninguna otra operación. Con false, mostrar días y
zonas, ofreciendo corregir o «Guardar igual» con el endpoint de guardado habitual.
Con null, informar que falta configurar agenda; no presentarlo como coincidencia.

## Clientes con días fuera de recorrido

`GET /api/route-planning/uncovered?vehicleId={uuid}&page=1&pageSize=50` — solo Admin.
vehicleId opcional, page por defecto 1, pageSize por defecto 50 y máximo 200.
Respuesta 200:

```json
{
  "items": [
    {
      "id": "44444444-4444-4444-8444-444444444444",
      "displayName": "Ana",
      "address": "Calle 123",
      "vehicleId": "11111111-1111-4111-8111-111111111111",
      "zoneId": "22222222-2222-4222-8222-222222222222",
      "zoneName": "Centro",
      "visitDays": [1, 4],
      "uncoveredDays": [4]
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 50,
  "totalPages": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

Incluye activos no eliminados, con camión y agenda guardada, si al menos un día
no coincide; incluye también aquellos sin ningún día cubierto. Excluye camiones
sin agenda y clientes antiguos sin días de visita. Orden estable por ID.
Filtro y paginado en SQL; tres consultas por página, sin consultas por cliente.
La lista describe la agenda actual, no prueba si hubo ventas ni modifica actividad.

## Cambio de zona desde Recorridos

`PUT /api/route-planning` acepta `changes[].zoneId` opcional; omitido o null conserva
la zona. UUID vacío es inválido; zona inexistente/eliminada devuelve 404. Al cambiar,
el cliente pasa al final de la nueva zona mediante `GetNextRouteOrderAsync`; varios
traslados a la misma zona obtienen posiciones consecutivas según orden en changes.
El cliente sale de la lista original. Ver [route-planning.md](route-planning.md)
para cuerpo completo, versionado y errores. La cobertura no es requisito del PUT.

## Errores y operación

401 sin autenticar; 403 rol o camión no autorizado/chofer inactivo;
404 recurso inexistente o eliminado; 400 cuerpo inválido, zona inactiva al guardar,
Admin sin driverId, chofer sin camión o paginación inválida. Formato ProblemDetails
habitual (`status`, `title`, `detail`, `instance`, `traceId`; validación añade `errors`).
No usar 400 como aviso de cobertura: una falta de cobertura devuelve 200.

La migración de varias zonas conserva la zona anterior como primera de cada día.
Guardar la agenda no cambia las salidas ya abiertas ni las zonas de los clientes.
Los avisos de cobertura y clientes fuera de recorrido tienen en cuenta todas las zonas de cada día.

## Salida con varias zonas

`SessionDto` (actual, detalle y listados) incluye `zones: [{zoneId, zoneName}]` en orden.
`zoneId` y `zoneName` conservan la primera como principal. Para mostrar varias zonas,
usá «Zonas: San Miguel de Tucumán → Tafí Viejo». Camiones en la calle y resumen diario
ya traen los nombres unidos con ` → ` en `zoneName`.

Para la hoja del chofer se consulta `/api/customers?zoneId={uuid}` una vez por zona
(con su paginado, vehículo y días), respetando el orden de `zones`. El cliente pertenece
al recorrido si está activo, pertenece al camión, coincide en días y su zona está entre
las de la salida. Visitados, «Visitar más tarde» y búsqueda abarcan la lista completa.

En visitas con cliente nuevo, `newCustomer.zoneId` es opcional y debe pertenecer a la
salida; en promociones, lo mismo para `prospect.zoneId`. Si se omite, se usa la principal.
Con varias zonas el formulario permite elegir y propone la primera; con una no muestra
el selector. No se cambia la zona de la salida a mitad del reparto.
