# Baja de clientes pedida por el chofer

Tarea 18 de `TAREAS-PARA-CONSTRUCTOR.md`. El chofer pide la baja desde la ficha del
cliente; el cliente sale de su hoja en el momento y ADMIN la confirma o la rechaza.
Nunca se borra un cliente: confirmada, queda inactivo con todo su historial. JSON
camelCase; los enums viajan como texto.

| Verbo | Ruta | Rol |
| --- | --- | --- |
| POST | /api/customers/{id}/removal-requests | Driver |
| GET | /api/customers/removal-requests?status=Pending&page=1&pageSize=20 | Admin |
| POST | /api/customers/removal-requests/{requestId}/confirm | Admin |
| POST | /api/customers/removal-requests/{requestId}/reject | Admin |

## Pedir la baja (chofer)

```json
{
  "clientRequestId": "6f7c2c1a-6a0e-4b8e-9d1c-0c1b2a3d4e5f",
  "reason": "Moved",
  "notes": "Se fue a Salta",
  "containersNotReturned": false
}
```

- `reason`: `Moved` (se mudó), `ClosedBusiness` (cerró el negocio), `NoLongerWants`
  (no quiere más el servicio), `Deceased` (falleció), `Other` (nota obligatoria).
- Solo para clientes de la salida abierta del chofer (camión, zona y días). Cliente
  ajeno: 403. Sin salida abierta: 403.
- **Envases**: si el cliente tiene envases (saldo por producto o unidades
  identificadas) y `containersNotReturned` es `false`, responde **409** con el detalle
  en `containers`, para ofrecer retirarlos primero:

```json
{
  "status": 409,
  "title": "Conflict",
  "detail": "El cliente tiene envases. Retiralos primero o marcá que no los devolvió.",
  "containers": [{ "productId": "…", "productDetail": "Bidon 20 lts", "quantity": 3 }]
}
```

  Con `containersNotReturned: true` la baja se registra igual y guarda esos envases.
- **Deuda**: no bloquea. Se guarda el saldo del momento en `debt`.
- Un cliente con una baja pendiente no puede tener otra (409).
- Idempotente por `clientRequestId`: el mismo envío devuelve la misma solicitud; la
  misma clave con otros datos responde 409.

Respuesta 200 (`RemovalDto`):

```json
{
  "id": "…", "customerId": "…", "customerName": "Rosa Aráoz",
  "vehicleId": "…", "vehicleName": "Renault Kangoo", "zoneId": "…", "zoneName": "Banda del Río Salí",
  "driverId": "…", "driverName": "Marcos Díaz", "requestedByUserId": "…",
  "requestedAt": "2026-10-15T19:10:00Z", "status": "Pending", "reason": "Moved",
  "notes": "Se fue a Salta", "containersNotReturned": false, "containers": [], "debt": 0,
  "resolvedByUserId": null, "resolvedAt": null, "clientRequestId": "…"
}
```

## Efecto inmediato

Mientras la baja está `Pending`, el cliente no aparece en la hoja del chofer, en
"sin visitar", en Recorridos ni en la cobertura de la agenda, y una venta o visita
responde 409 «El cliente fue dado de baja; lo confirma la oficina». Su lugar en el
recorrido (`routeOrder`) no cambia. En el listado y la ficha de ADMIN el cliente
sigue visible con `removalPending: true`.

## Resolver (ADMIN)

- `GET …/removal-requests`: `{ items: RemovalDto[], totalCount, pendingCount, page, pageSize }`.
  `pendingCount` no depende del filtro, sirve para el aviso del Home. `status` es
  opcional (`Pending`, `Confirmed`, `Rejected`; sin valor, todas).
- `confirm`: el cliente queda `isActive: false`. Nunca se elimina.
- `reject`: vuelve a la hoja en su lugar de siempre.
- Repetir la misma resolución devuelve la solicitud; resolver una ya resuelta de
  otra forma responde 409.
- Reactivar un cliente dado de baja es solo de ADMIN, desde la edición del cliente.

## Cuenta corriente

`statement` de `GET /api/customers/{id}/account` suma renglones `RemovalRequested`,
`RemovalConfirmed` y `RemovalRejected` con `removalReason` y `notes`, sin mover dinero.
