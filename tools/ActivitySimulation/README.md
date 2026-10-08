# Simulación local de actividad

Consola **solo de desarrollo**, independiente de la solución y de la publicación
de la API. Usa las entidades EF y la política/lector reales, sin migraciones ni
cambios de umbrales. No crea clientes, choferes, vehículos, zonas ni productos.

Desde `GM-SoftBE`, en **PowerShell**:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
# Si DOTNET_ENVIRONMENT está definido, también debe ser Development.
dotnet run --project tools/ActivitySimulation -c Release -- --dry-run
dotnet run --project tools/ActivitySimulation -c Release -- --apply
dotnet run --project tools/ActivitySimulation -c Release -- --undo
```

Sin argumentos equivale a `--dry-run`. Este modo usa una transacción PostgreSQL
READ ONLY: no escribe en la base ni crea manifiesto/lock (la compilación de
`dotnet run` sí puede generar bin/obj). Solo `--apply` escribe el escenario.

Opcionalmente elegir clientes por ID o nombre **exacto**, sin distinguir mayúsculas:

```powershell
dotnet run --project tools/ActivitySimulation -c Release -- --dry-run --a 'Nombre A' --b 'Nombre B' --c 'Nombre C'
dotnet run --project tools/ActivitySimulation -c Release -- --apply --a 'Nombre A' --b 'Nombre B' --c 'Nombre C'
```

Los nombres ambiguos se rechazan: usar GUID. Los puestos no indicados se completan
por `RouteOrder`, luego ID, excluyendo los ya elegidos. Deben ser tres clientes
distintos, activos, con zona activa y días de visita, del vehículo de `reparto1`.
Se requiere una cuenta Admin activa para identificar carga, recepción y rendición.

Se leen `GMSoft.API/appsettings.json`, `appsettings.Development.json` y variables
de entorno. La conexión respeta la precedencia de la API: `CONNECTION_STRING`,
`DATABASE_URL`, `DATABASE_PUBLIC_URL`, `ConnectionStrings__DefaultConnection` y
JSON. Acepta el formato PostgreSQL URI de la API. No copia ni imprime secretos.
Rechaza entorno ausente/distinto de Development, entornos contradictorios y todo
host distinto de `localhost`/`127.0.0.1` (incluidos hosts múltiples). No migra la base.

## Fechas y limitaciones del escenario

- A compra en una visita de la semana actual ya terminada; si todavía no se puede
  recibir una visita de esa semana, se usa la semana anterior.
- B compra en la semana -4 y pierde las semanas -3/-2/-1: Red(3).
- C compra en la semana -6 y pierde las semanas -5/-4/-3/-2/-1: Black(5).
- Las semanas son lunes-domingo argentinos. Se guardan instantes UTC. Las salidas
  compartidas de igual zona/día se unifican. Distintas zonas tienen franjas sin
  solapamiento 08–10, 11–13, 14–16; carga media hora antes y venta una hora después
  de abrir. Por prudencia, el día actual solo se usa desde las 16:00 AR.

**No toda combinación puede dar 0/3/5.** Si A debe comprar esta semana y comparte
zona/día con B o C, su salida también les suma un turno. Se busca otro día de A;
si no existe, se aborta y se piden otros clientes. Las salidas también cuentan
para los demás clientes del vehículo/zona/día, sin modificar sus filas.

Antes de escribir se listan ventas posteriores incompatibles, salidas reales en
los días del plan (incluidas bajas lógicas), salidas abiertas y actividad adicional
que altere 0/3/5. No se borra ni adapta historial ajeno. Los kilómetros se insertan
creciendo un kilómetro por salida entre las lecturas históricas y el odómetro
actual; si no hay espacio se aborta. No se modifica `Vehicle.CurrentKilometers`.

## Datos y reversión

`--apply` inserta únicamente en `DeliverySessions`, `VehicleLoads`,
`SessionStockMovements`, `Deliveries`, `DeliveryItems`, `Payments`,
`SessionCashSettlements` y, si corresponde, `ContainerMovements`.
Vende una unidad de un producto publicado None/ByBalance con precio vigente del
cliente (o catálogo), positivo, íntegramente cobrado en efectivo. Lleva una unidad
extra que vuelve llena; devuelve todos los vacíos y rinde todo lo cobrado.
En ByBalance entrega y recupera igual cantidad: no toca `CustomerContainerBalances`
ni genera deuda. El único campo existente que puede modificar es
`Customers.LastVisitAt`, solo si la venta sembrada es más reciente. Conserva todos
los demás campos, incluidos los timestamps de auditoría de filas existentes.
Las filas nuevas llevan timestamps históricos; la herramienta usa `SaveChanges()`
de EF para evitar el override async que reemplaza esas fechas por la hora actual.

Todo ocurre en **una transacción**. Durante apply/undo se bloquean escrituras en
las tablas del modelo para impedir carreras con la API; las lecturas pueden seguir.
El timeout de lock es cinco segundos. Conviene hacerlo con la app sin actividad.
Antes del commit se vuelve a leer con `CustomerActivityReader` y se exige 0/3/5;
cualquier diferencia revierte la transacción. Los umbrales deben seguir en 2/4.

Cada ejecución usa un UUID y Notes con prefijo `[GMSoft.ActivitySimulation/v1]`.
`activity-manifest.json`, ignorado por git, guarda la identidad de la base sin
contraseña, IDs, snapshots íntegros de las filas sembradas y LastVisitAt anterior.
**Conservar ese archivo hasta terminar `--undo`**; contiene nombres indirectamente
por IDs, importes y datos de la simulación, pero ninguna credencial.

Un segundo apply verifica el manifiesto y muestra la actividad actual sin duplicar.
Para cambiar selección o actualizar fechas: undo y luego apply. Si falta el
manifiesto pero hay marcas en la base, se rechaza la operación: recuperarlo, nunca
borrar por prefijo. Undo verifica que ninguna fila sembrada haya cambiado y que
ninguna fila ajena la referencie, incluidas relaciones con cascade y bajas lógicas.
También verifica LastVisitAt antes de restaurarlo. Si hubo cambios posteriores,
aborta íntegramente para revisión; no los sobrescribe.

El manifiesto se escribe y sincroniza a disco antes del commit. Si el proceso cae,
`--undo` puede retirar un manifiesto pendiente cuando confirma que no hay filas y
que los campos originales siguen intactos. Si el commit sí ocurrió, deshace las
filas exactas. Borrado físico de hijos primero, siempre por IDs del manifiesto.
No se pierde la reversión por un corte entre commit y escritura del archivo.

## Verificación sin base

```powershell
dotnet build GMSoft.slnx -c Release
dotnet build tools/ActivitySimulation/ActivitySimulation.csproj -c Release
dotnet test GMSoft.Application.Tests/GMSoft.Application.Tests.csproj -c Release
dotnet test tools/ActivitySimulation.Tests/ActivitySimulation.Tests.csproj -c Release
```

Los tests no se conectan a PostgreSQL. Cubren días ISO, semanas/años/meses,
fecha UTC distinta del día argentino, visitas todavía pendientes, días múltiples,
interferencia de salidas compartidas, resultados de la política real, kilómetros
y conciliación de stock, envases, venta/cobro/rendición.
