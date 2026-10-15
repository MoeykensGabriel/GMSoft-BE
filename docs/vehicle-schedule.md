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
    {"day": 1, "zoneId": "22222222-2222-4222-8222-222222222222", "zoneName": "Centro"},
    {"day": 2, "zoneId": null, "zoneName": null},
    {"day": 3, "zoneId": null, "zoneName": null},
    {"day": 4, "zoneId": "22222222-2222-4222-8222-222222222222", "zoneName": "Centro"},
    {"day": 5, "zoneId": null, "zoneName": null},
    {"day": 6, "zoneId": null, "zoneName": null},
    {"day": 7, "zoneId": null, "zoneName": null}
  ]
}
```

Siempre devuelve siete días ordenados. `null` en una agenda guardada significa
que ese día no sale. Sin filas guardadas: `isConfigured:false`, `isSuggested:true`
y la misma estructura con una sugerencia del historial, sin escribir datos.
Sin historial elegible, los siete días tienen zona null; no significa una semana
confirmada sin salidas. Guardar explícitamente siete null sí configura esa semana.

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
    {"day": 1, "zoneId": "22222222-2222-4222-8222-222222222222"},
    {"day": 2, "zoneId": null},
    {"day": 3, "zoneId": null},
    {"day": 4, "zoneId": "22222222-2222-4222-8222-222222222222"},
    {"day": 5, "zoneId": null},
    {"day": 6, "zoneId": null},
    {"day": 7, "zoneId": null}
  ]
}
```

Requiere exactamente siete días diferentes 1..7; zonas existentes, no eliminadas
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
  "isConfigured": true,
  "isSuggested": false,
  "routeDays": [1, 4],
  "days": [
    {"day": 1, "zoneId": "22222222-2222-4222-8222-222222222222", "zoneName": "Centro"},
    {"day": 4, "zoneId": "33333333-3333-4333-8333-333333333333", "zoneName": "Norte"}
  ]
}
```

Combina los días de todas las cargas pendientes, sin repetir y en orden ISO.
Si hay carga antigua sin días, usa hoy en Argentina, igual que OpenSession.
Sin carga pendiente devuelve `routeDays:[]`, `days:[]`, zona y nombre null.
La zona principal es la del primer día ISO, incluso si es null; `days` contiene
todos los días y zonas, incluidas repeticiones o null. Si no hay agenda, usa la
sugerencia histórica y marca `isSuggested:true`, `isConfigured:false`.

El frontend prellena la zona, muestra las alternativas/días y permite cambiarla.
`OpenSessionCommand.ZoneId` sigue siendo obligatorio y la apertura mantiene sus
reglas existentes. No se exige coincidir con esta sugerencia ni confirmación nueva.
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

`covered:true` solo si todos los días coinciden; `false` si alguno no coincide,
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

Persistencia aditiva: tabla VehicleScheduleDays, clave compuesta VehicleId+Day,
check ISO 1..7, FK a vehículo y zona. No transforma datos existentes. La migración
se genera en Release; solo se aplica automáticamente en las bases efímeras de tests
para validarla. No se aplicó a la base operativa ni se levantó la API.
El refresco cada 30 segundos y los avisos visuales corresponden a la tanda frontend.
