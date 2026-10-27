# Carga esperada por vehículo

El administrador configura un mínimo y/o máximo esperado por vehículo y producto.
El frontend pinta en rojo las cantidades fuera de rango, pero **no bloquea la carga**.
Estos rangos no cambian las reglas del backend para registrar cargas.

## Endpoints (solo ADMIN)

- `GET /api/vehicles/{id}/load-ranges`: **200** con `[{ "productId": "…", "min": 1, "max": 4 }]`.
  Devuelve solo filas guardadas, ordenadas por detalle del producto, con los dispenser al final.
  Sin configuración devuelve `[]`; un extremo no configurado se devuelve como `null`.
- `PUT /api/vehicles/{id}/load-ranges`: **204**, sin cuerpo de respuesta. Reemplaza todos los rangos
  del vehículo en una transacción. Los productos omitidos se borran; `{"ranges": []}` borra todos.
  Se usa siempre el `id` de la ruta, aunque el cuerpo incluya otro `vehicleId`.

Cuerpo del PUT (los extremos también pueden omitirse):

```json
{
  "ranges": [
    { "productId": "11111111-1111-1111-1111-111111111111", "min": 1, "max": 4 },
    { "productId": "22222222-2222-2222-2222-222222222222", "min": 20, "max": null },
    { "productId": "33333333-3333-3333-3333-333333333333", "min": null, "max": 10 }
  ]
}
```

## Validaciones y errores

- El vehículo debe existir: **404** en GET y PUT. El identificador vacío en PUT es **400**.
- `ranges` es obligatorio y no puede ser `null`; no admite filas nulas ni productos repetidos.
- Cada fila debe tener al menos `min` o `max`, enteros entre **0 y 100000**, inclusive.
- Si vienen ambos, `max >= min`.
- Todos los productos deben existir. Un producto inexistente o una validación fallida devuelve **400**;
  se conserva la configuración previa.
- Un chofer recibe **403** tanto al consultar como al guardar; sin autenticación, **401**.

La tabla tiene un índice único por vehículo y producto, y borrado en cascada ante el borrado físico
del vehículo o producto. Las bajas lógicas existentes no son borrados físicos.
