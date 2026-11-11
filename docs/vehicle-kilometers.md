# Kilometraje por camión

`GET /api/reports/vehicle-kilometers?from=2026-11-01&to=2026-11-30`

Requiere Bearer JWT con rol `Admin`. Responde 200 con el objeto siguiente,
401 sin autenticación, 403 sin permiso y 400 ante parámetros inválidos.
Mismo período que [Rendimiento por camión](vehicle-performance.md): fechas
`YYYY-MM-DD`, `from <= to`, máximo 366 días inclusivos, por `OpenedAt` en
hora de Argentina.

Es el primer paso de la sección de mantenimiento: solo lógica, sin pantalla.

## Qué salidas cuentan

- Solo **cerradas** (`Status == Closed`) con kilometraje de vuelta. Las abiertas,
  anuladas y borradas no cuentan.
- Km de una salida = `kilometersAtClose - kilometersAtOpen`. Si la recepción se
  corrigió, ya vale el km corregido.
- Un camión sin salidas cerradas en el período no aparece. Un camión dado de
  baja con salidas en el período sí.

## Indicadores por camión

| Campo | Qué es |
|---|---|
| `sessions` | Salidas que cuentan. |
| `totalKilometers` | Suma de los km de esas salidas. |
| `averagePerSession` | Promedio por salida. |
| `medianPerSession` | **La referencia.** Un km inventado mueve el promedio pero no la mediana. Con cantidad par, promedio de los dos del medio. |
| `minPerSession`, `maxPerSession` | La salida más corta y la más larga. |
| `daysWorked`, `averagePerDay` | Días con salida y km por día. |
| `zeroKilometerSessions` | Salidas con 0 km: dato no cargado. |
| `kilometersBetweenSessions` | Lo que subió el tablero entre una vuelta y la salida siguiente del mismo camión: km fuera de reparto o mal cargados. |
| `routes` | Lo mismo por recorrido: las zonas de la salida en su orden (`"SMT → Tafí Viejo"`), de la más frecuente a la menos. |

Promedios y medianas con 1 decimal. Orden de `vehicles`: por nombre.

## JSON de ejemplo

```json
{
  "from": "2026-11-01",
  "to": "2026-11-30",
  "vehicles": [
    {
      "vehicleId": "01a15ca5-d3de-750a-a7cc-73d8166e90d6",
      "vehicleName": "Dfsk - DE",
      "licensePlate": "AG989DE",
      "sessions": 4,
      "totalKilometers": 300160,
      "averagePerSession": 75040.0,
      "medianPerSession": 40.0,
      "minPerSession": 40,
      "maxPerSession": 300000,
      "daysWorked": 4,
      "averagePerDay": 75040.0,
      "zeroKilometerSessions": 0,
      "kilometersBetweenSessions": 20,
      "routes": [
        {
          "route": "San Miguel de Tucumán → Tafí Viejo",
          "sessions": 4,
          "averagePerSession": 75040.0,
          "medianPerSession": 40.0,
          "minPerSession": 40,
          "maxPerSession": 300000
        }
      ]
    }
  ]
}
```

## Para después

Rangos normales por camión, avisos en Alertas, mantenimiento por km o por fecha.

## Revisión del km (calibración)

El km que tenía el sistema no era confiable. Cada vehículo tiene
`KilometersCheckPending` (la migración lo pone en `true` a todos los existentes, y un
vehículo nuevo nace así).

1. Con la marca puesta, al abrir la salida el chofer ve "Km del tablero" y un aviso
   ámbar; **esa sola vez** puede poner menos que el km del sistema. Se guarda una
   `VehicleKilometersReading` en `Pending` y la marca se apaga: las salidas siguientes ya
   no pueden bajar, aunque la oficina no haya revisado. La salida no espera a la oficina.
2. La oficina la ve en **Alertas → Kilometraje para revisar**:
   - `GET /api/vehicles/kilometers-readings/pending`
   - `POST /api/vehicles/kilometers-readings/{id}/accept`: `KilometersCheckedAt` = hora
     de la lectura.
   - `POST /api/vehicles/kilometers-readings/{id}/reject`: vuelve la marca; la próxima
     salida lee el tablero de nuevo (y esa vez puede bajar).
3. Los promedios de este reporte cuentan solo salidas abiertas desde
   `KilometersCheckedAt`. Un vehículo en revisión que todavía no se aceptó no aparece.

Fuera de esa salida que lee el tablero, todo es como antes: el km de salida no puede ser
menor al del vehículo.
