# Suite HTTP: once recorridos completos

Desde la raíz de GM-SoftBE, con Python estándar, .NET 10 y PostgreSQL local
en localhost:5432 (sin Docker):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/e2e/run-local.ps1
# Verificar repetibilidad: dos suites sobre LA MISMA base y API, luego limpiar.
powershell -NoProfile -ExecutionPolicy Bypass -File tools/e2e/run-local.ps1 -Runs 2
```

El script necesita `psql`, disponible en PATH o en
`C:/Program Files/PostgreSQL/*/bin/psql.exe`. Compila mediante
`dotnet build GMSoft.slnx -c Release`. Resuelve credenciales de desarrollo con
prioridad `GMSOFT_TEST_CONNECTION`, `CONNECTION_STRING`, `DATABASE_URL`,
`DATABASE_PUBLIC_URL`, `ConnectionStrings__DefaultConnection`, user-secrets y
appsettings de la API. Admite cadenas key=value y URI postgres/postgresql;
nunca imprime la contraseña. El servidor debe ser local, puerto 5432, y el
usuario debe poder crear y eliminar su base. No conecta a la base de desarrollo:
psql usa exclusivamente `postgres` para CREATE, DROP y lectura del catálogo.

Crea `gmsoft_e2e_<uuid>` y reemplaza por completo la base de la conexión de la
API. Ejecuta directamente la DLL Release en un proceso oculto propio, sin
perfiles de lanzamiento (`--no-launch-profile`), por defecto en 5199. El puerto
5142 está prohibido; `-Port` permite otro puerto libre. Comprueba que el listener
pertenece al PID iniciado antes de enviar la suite. Usa entorno Production,
`CONNECTION_STRING`, `JWT_SECRET_KEY`, `Database__MigrateOnStartup=true`,
`Seed__AdminUserName`, `SEED_ADMIN_PASSWORD` y `ASPNETCORE_URLS` propios. La base
se crea vacía: la API aplica sus migraciones y siembra el admin al arrancar.
Los umbrales de actividad del entorno de prueba son 2 y 4 semanas.

El `finally` detiene únicamente ese PID, elimina únicamente la base generada
con `DROP DATABASE ... WITH (FORCE)` y verifica PID terminado, cero listeners
en el puerto de prueba y el catálogo de `gmsoft_e2e_*`. También se ejecuta al
fallar la suite o el arranque; no elimina bases de otras corridas. Restaura las
variables del proceso al terminar. Una terminación forzada de PowerShell o del
sistema puede impedir el `finally`; el nombre creado queda en la consola.

Los logs locales quedan ignorados por Git en `artifacts/e2e/api.stdout.log`,
`api.stderr.log` y `suite-<n>.log`. No incluyen tokens ni contraseñas en los
diagnósticos de la suite. El runner devuelve 1 si falla el build, arranque,
alguna prueba no conocida o la limpieza. Una falla conocida de negocio no
oculta fallas posteriores del mismo recorrido ni de los demás.

Para una API de prueba ya levantada (CI conserva su servicio PostgreSQL):

```powershell
$env:GMSOFT_BASE_URL = 'http://localhost:5199'
$env:GMSOFT_ADMIN_USER = 'e2eadmin'
$env:GMSOFT_ADMIN_PASSWORD = 'E2eAdmin1234'
python tools/e2e-smoke.py
```

Ese punto de entrada **escribe datos por HTTP** y no administra procesos ni bases;
usar el runner para garantizar aislamiento local. Sin variable BASE_URL apunta
a 5199, nunca a la API de desarrollo. CI configura explícitamente su URL y
credenciales en el paso de humo existente. No se agregaron paquetes Python.

Cada recorrido crea zona, camión, chofer/usuario, dos productos y clientes con
identificadores aleatorios, usa tokens reales y termina con recepción, rendición
y consultas de estado final. Autorización crea un segundo conjunto independiente.
Los reportes globales se filtran por productos/zonas propios. Se conserva y
restaura la configuración global de promociones en `finally`; las suites sobre
una misma API deben ejecutarse secuencialmente.

| Recorrido | Cobertura |
| --- | --- |
| 01 Día normal | Efectivo calculado por servidor, deuda, envases, liquidación detallada, resumen y cuentas consistentes. |
| 02 Reintentos | Carga, venta, recarga, promoción y cierre; misma respuesta, un solo efecto, también después de recibir la salida. |
| 03 Recarga | Varios productos, stock y aviso al chofer, home conserva carga inicial, resumen suma recarga, nueva recarga cerrada 409 sin cambios. |
| 04 Promociones | Prospectos sin cliente, listados, envases afuera, conversión al final del recorrido, pérdida sin conversión, doble cierre 409, plazo configurable y congelado. |
| 05 Recorridos | Consulta por camión/zona/día, orden, cambios de días y camión, listado y hoja, versión vieja 409 sin cambios. |
| 06 Diferencias | Faltan exactamente 2 llenos, 1 vacío y 500 de efectivo; cuenta, recepción, resumen y rendición. |
| 07 Autorización | 401, rutas ADMIN 403, ADMIN sin ventas, aislamiento de salidas/clientes/promociones y ausencia de modificaciones. |
| 08 Validaciones | Cantidades cero/negativas, duplicados, días, UUID, límites de paginación; ProblemDetails y errors por campo; 404, sobreventa, salida sin carga y carga en calle 409. |
| 09 Visitas | Varios días, orden, posponer idempotente, rechazo conserva marca, venta limpia solo su marca, alta en calle al final, siguiente salida sin marcas. |
| 10 Original + actividad | Circuito original de dos vueltas (35000/42500 de deuda, ajuste 7→6, pérdida 2, reportes), semana perdida al recibir, dos vueltas no duplican semana, venta reinicia y coherencia listado/ficha/cuenta. |
| 11 Visita sin compra | Motivos, nota y clave; rechazos, corrección y reintento viejo; stock y cuenta sin cambios; motivo en recorrido, liquidación y cuenta; venta posterior; 409 tras venta; sin visitar antes y después de recibir. |

Cada código HTTP esperado y cada aserción de contenido cuentan como una
verificación; la consola informa el número por recorrido y el tiempo total.
El login inicial de admin agrega una verificación al total. El rate limiter
real permanece activo: se reutilizan tokens y se espacian los logins con una
ventana conservadora de 61 segundos (máximo nueve por ventana). Entre suites
del runner hay 61 segundos de espera. Una corrida tarda aproximadamente 67 s,
mayormente por esa protección. No se reintentan escrituras automáticamente.

La actividad histórica Red/Black y el vencimiento real de promociones no se
fuerzan cambiando fechas por SQL: no existe un contrato HTTP para retrocederlas.
Los umbrales de semanas históricas siguen cubiertos por integración. El smoke
comprueba las transiciones que sí se pueden producir hoy por HTTP.

El rechazo de clientes fuera de camión/zona/días es 400 con ProblemDetails,
según `CustomerRouteAccess`; no es una filtración ni una falla conocida. El
contrato de días exige rechazo sin fijar 403. Las restricciones de rol y de
salidas/promociones ajenas sí se verifican con 403. Los 401/403 producidos por
middleware de autorización pueden tener cuerpo vacío; los errores de aplicación
400/403/404/409 se verifican como ProblemDetails.

# Tests de integración con PostgreSQL real

Requieren .NET 10 y PostgreSQL alcanzable, sin Docker. En Windows (PowerShell),
desde la raíz de GM-SoftBE:

```powershell
# Opcional si la configuración de desarrollo de la API ya funciona.
# Reemplazar los valores por los del servidor local; el nombre de base se ignora.
$env:GMSOFT_TEST_CONNECTION = 'Host=localhost;Port=5432;Username=postgres;Password=TU_CLAVE'
dotnet build GMSoft.slnx -c Release
dotnet test GMSoft.Application.Tests/GMSoft.Application.Tests.csproj -c Release
dotnet test GMSoft.IntegrationTests/GMSoft.IntegrationTests.csproj -c Release --logger 'console;verbosity=normal'
```

La prioridad es `GMSOFT_TEST_CONNECTION`, `CONNECTION_STRING` y luego la resolución
de desarrollo de la API: `DATABASE_URL` / `DATABASE_PUBLIC_URL`, o
`ConnectionStrings:DefaultConnection` en appsettings.json,
appsettings.Development.json, user-secrets del proyecto API (si declara
UserSecretsId) y variables de entorno. Acepta también URI postgres/postgresql.
La contraseña no se imprime. El usuario necesita permiso CREATEDB y ser dueño de
la base creada para eliminarla. Sin conexión o permisos la suite **falla**.

La fixture conecta a la base administrativa `postgres` únicamente para CREATE,
DROP y lectura del catálogo. Crea `gmsoft_it_<16 caracteres aleatorios>` y siempre
reemplaza la base de la cadena original. Todas las migraciones y escrituras de
negocio van exclusivamente a esa base efímera; nunca a desarrollo ni `gmsoft_ci`.
Una guarda valida el prefijo `gmsoft_it_` y el identificador antes de crear,
conectar el contexto o eliminar. No se inicia HTTP ni se ejecuta la API.

Se elimina con `DROP DATABASE ... WITH (FORCE)` al terminar, incluso ante tests
fallidos o errores al migrar. El log normal muestra el nombre creado, su eliminación,
el tiempo y la lista final de bases con ese prefijo. Una terminación forzada del
proceso o caída del servidor puede impedir el teardown: no hay borrado automático
de otras bases, ni siquiera de otras ejecuciones de integración. Revisar esos
nombres antes de cualquier limpieza manual; nunca borrar bases sin el prefijo.

Una sola colección evita paralelismo entre tests. Cada escenario tiene zona,
camión, usuarios Identity, chofer, clientes y productos propios; cada envío
MediatR y cada lectura de verificación usa un contexto nuevo. Se registran
AddDataLayer, AddApplicationLayer y CustomerActivityPolicy como en la API; solo
se sustituyen el usuario actual y la fábrica del contexto para fijar la base
efímera. Carga, apertura y recepción usan los handlers reales. Los datos históricos
de actividad se fechan en SQL después de ejecutar esos handlers, con reloj real.

Las carreras ejecutan Task.WhenAll con scopes separados. La barrera SQL de ventas
espera a que ambas consultas reales de stock hayan terminado; no reemplaza datos
ni repositorios. Así reproduce la ventana de concurrencia sin sleeps. El índice
único, las transacciones y los bloqueos son los de PostgreSQL.

CI utiliza el servicio postgres:16 existente y su CONNECTION_STRING, con un paso
de integración separado de los tests de unidad y del smoke HTTP.

La suite tiene 47 casos, agrupados por el Trait "Grupo" de cada test.
En PostgreSQL local tarda aproximadamente 1,5–2 minutos, más restauración y
compilación. El log informa el tiempo efectivo de cada ejecución.

## Bugs que encontraron las suites (corregidos)

- Precisión de fechas en reintentos de promociones: repetir el registro o el cierre
  devolvía la hora con un dígito menos que la respuesta original. Ahora se recorta
  a microsegundos antes de guardar, como en las recargas. La encontró la suite HTTP.
- Reintento simultáneo de una venta: dos pedidos con el mismo clientRequestId
  guardaban una sola venta, pero el segundo recibía el error del índice único en
  vez de la venta original. `Reintentos_simultaneos_de_venta_devuelven_ambos_la_venta_original`.
- Sobreventa: con 20 llenos a bordo, dos ventas simultáneas de 15 se confirmaban
  las dos y el stock quedaba en -10. `Ventas_simultaneas_no_superan_el_stock_disponible`.

Los dos tenían la misma causa: la venta decidía sobre un stock leído antes de que
otra visita terminara de guardarse. Ahora la venta y la promoción de prueba
bloquean la fila de la salida (el mismo bloqueo de la recarga y la recepción) y,
ya con el bloqueo, vuelven a buscar el clientRequestId antes de escribir.

Las recargas con una misma clave sí devuelven la misma tanda, aun forzando ambas
lecturas iniciales antes de escribir. En la carrera recarga/recepción se acepta
recarga previa al cierre (queda incluida en el faltante), o conflicto si gana
el cierre; el stock nunca cambia por una recarga posterior a la recepción.
