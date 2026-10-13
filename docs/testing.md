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
de integración separado de los 256 tests de unidad y del smoke HTTP.

La suite tiene 38 casos: A=2, B=7, C=4, D=4, E=3, F=3, G=5, H=6, I=3, J=1.
En PostgreSQL local tarda aproximadamente 1,5–2 minutos, más restauración y
compilación. El log informa el tiempo efectivo de cada ejecución.

## Bugs que encontró la suite (corregidos)

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
