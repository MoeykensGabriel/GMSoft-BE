# Operación de GM-Soft

Guía para quien publica y mantiene Villa Esperanza. [Índice](README.md).

**Producción corre en Railway y cada repositorio publica automáticamente su rama `main`**, según la configuración informada por el dueño. Backend y web son servicios separados. Lo que todavía no está listo se trabaja en otra rama.

Esta guía no ejecutó despliegues ni consultó datos o secretos de producción. Para crear servicios o revisar variables, usá [Publicar en Railway](deploy-railway.md). El detalle de Docker, datos y seguridad está en [Arquitectura técnica](arquitectura-tecnica/arquitectura-tecnica.md).

## Antes de publicar

1. Identificá el cambio y los commits de API y web que deben acompañarse. Registrá también las versiones actualmente sanas.
2. Revisá las verificaciones de ambos repositorios: backend según [testing.md](testing.md); frontend según [su arquitectura](../../GM-SoftFE/docs/arquitectura.md#cómo-se-verifica).
3. Revisá las migraciones nuevas y la compatibilidad del contrato. Las ramas son independientes: `main` de un repo no espera al otro.
4. Coordiná con oficina si habrá interrupción. Evitá cortar una recepción o dejar visitas sin enviar desde celulares.
5. Hacé un respaldo verificado de la base antes de incorporar el cambio que activa el despliegue.

No uses los recorridos de prueba que escriben por HTTP contra producción. El estado saludable del proceso tampoco reemplaza una prueba de acceso a datos.

## Orden de publicación

1. Incorporá primero el backend listo a `main` de `GM-SoftBE`.
2. Esperá que Railway construya, arranque y termine las migraciones. Revisá logs y `/api/health`.
3. Comprobá login y una lectura autenticada. La web anterior debe seguir funcionando mientras se actualiza la nueva.
4. Después incorporá la web a `main` de `GM-SoftFE`.
5. Esperá el despliegue y comprobá login, navegación y recarga de rutas internas.

Si la nueva API rompe el contrato de la web anterior, publicar primero backend no basta: prepará compatibilidad transitoria o coordiná una ventana. No dependas de que los dos despliegues terminen al mismo tiempo.

Cambiar `VITE_API_URL` requiere reconstruir la web. Reiniciar el mismo contenedor no actualiza el bundle ni su CSP. Al cambiar dominios, revisá además `CORS_ORIGINS` en la API. No copies secretos a variables `VITE_*`.

## Migraciones al arrancar

El código de [`Program.cs`](../GMSoft.API/Program.cs) aplica migraciones en Development o con `Database:MigrateOnStartup` habilitado. En producción se configura con **`Database__MigrateOnStartup=true`**. Con esa opción, el arranque aplica las pendientes y después intenta el seed de ADMIN.

Si una migración falla, el proceso no continúa normalmente. Revisá el error del despliegue. El seed no cambia la contraseña de una cuenta ya existente; un health correcto no acredita que el administrador inicial se haya creado.

Antes de publicar, revisá el `Up` de cada migración pendiente y el SQL que ejecutará:

- Eliminaciones de columnas o tablas, `DELETE`, `TRUNCATE` y cambios que reescriban datos.
- Cambios de tipo, longitud, nulabilidad o valores por defecto que afecten registros existentes.
- Nuevos índices o restricciones que puedan fallar con los datos actuales.
- Operaciones largas y compatibilidad con la aplicación anterior.

Una migración generada correctamente puede seguir siendo destructiva. No borres migraciones ya aplicadas ni ejecutes una reversión de esquema como respuesta automática a un fallo de la app.

La configuración de arranque no define un procedimiento propio para varias réplicas migrando a la vez. Revisá esa decisión antes de escalar.

## Respaldo antes de subir

Prepará una copia con fecha, entorno y versión de aplicación. Guardala en un lugar privado fuera de los repositorios. Conservá sus controles de acceso: una copia contiene información de clientes aunque no tenga una contraseña visible.

En Railway, revisá el servicio PostgreSQL y su sección **Backups**. Si tiene respaldos disponibles, generá o identificá uno anterior al cambio y esperá que termine correctamente. La disponibilidad y retención efectivas de Villa Esperanza están **«A confirmar»**; no se deducen de los Dockerfiles. [Guía oficial de respaldo y restauración](https://docs.railway.com/guides/postgres-backups-restores).

Como copia lógica adicional, el responsable de base puede usar `pg_dump`. Ejemplo de procedimiento en PowerShell, sin valores de acceso en el documento. Requiere las herramientas de PostgreSQL y `PGHOST`, `PGPORT`, `PGDATABASE` y `PGUSER` ya configuradas de forma privada para el entorno correcto. La contraseña se pide interactivamente.

```powershell
foreach ($nombre in 'PGHOST', 'PGPORT', 'PGDATABASE', 'PGUSER') {
    if (-not [Environment]::GetEnvironmentVariable($nombre)) {
        throw "Falta configurar $nombre para el respaldo."
    }
}
$carpetaCopia = Read-Host 'Carpeta privada existente para guardar la copia'
$archivoCopia = Join-Path $carpetaCopia ("gmsoft-{0}.dump" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
pg_dump --format=custom --no-owner --password --file="$archivoCopia"
if ($LASTEXITCODE -ne 0) { throw 'Falló el respaldo; no publicar.' }
pg_restore --list "$archivoCopia" > $null
if ($LASTEXITCODE -ne 0) { throw 'No se pudo leer la copia; no publicar.' }
```

Comprobá previamente que esas variables apunten a la base deseada. Usá un cliente `pg_dump` compatible con la versión del servidor. Si el host solo resuelve en la red privada de Railway, ejecutá la copia desde un acceso autorizado que llegue a esa red; no alcanza con copiar la configuración a tu PC.

Leer el índice del dump solo comprueba que se puede interpretar el archivo. Para validar recuperación, restauralo en **una base aislada de prueba**, con una API de prueba, y comprobá migraciones aplicadas, login, lecturas y totales operativos. No apuntes ese ensayo a producción. Registrá fecha, resultado y responsable de la prueba.

## Volver atrás un despliegue

Si el cambio falla, frená nuevas publicaciones y anotá servicio, commit, hora y error. Coordiná una pausa de escrituras si hay riesgo para los datos.

En Railway, abrí **servicio → Deployments**, buscá el despliegue sano anterior y usá **Rollback** en su menú. Revisá la confirmación y esperá que quede activo. Railway restaura la imagen y las variables propias de ese despliegue; la opción depende de la retención del plan. Verificá las variables resultantes sin copiarlas a tickets o documentación. [Acciones oficiales de despliegue](https://docs.railway.com/deployments/deployment-actions).

Si no está disponible, recuperá una versión conocida mediante el procedimiento del proyecto. No confundas reiniciar con volver a otra versión. Comprobá compatibilidad si también retrocedés la web. Corregí luego la fuente en `main`: un rollback manual no corrige el código que iniciará la próxima publicación.

**Volver la aplicación atrás no devuelve la base al estado anterior.** Una migración pudo haber cambiado el esquema o los datos. Antes de arrancar código viejo, comprobá que pueda usarlos. Puede convenir una corrección hacia adelante.

Si hace falta recuperar datos, hacelo como un incidente separado: detené escrituras, preservá el estado actual, identificá la copia exacta y ensayá en otra base. Restaurar producción reemplaza su estado y puede perder operaciones posteriores a la copia. El responsable debe decidir cómo recuperarlas y coordinarlo con oficina. No elimines el servicio PostgreSQL para “rehacer” un despliegue.

## Verificar que salió bien

Después de publicar o recuperar:

1. Revisá que el despliegue esperado esté activo y que no se repitan reinicios o errores de migración.
2. Consultá **`GET /api/health`** de la API. Debe devolver `status: "ok"` y hora UTC. [`HealthController`](../GMSoft.API/Controllers/HealthController.cs) solo comprueba el proceso: no consulta PostgreSQL.
3. Abrí la web. Entrá con una cuenta autorizada y comprobá una lectura que use la base, por ejemplo el listado de vehículos.
4. Abrí una ruta interna y recargala. nginx debe devolver la SPA, no un 404.
5. Revisá en el navegador que las llamadas vayan a la API correcta y no fallen por CORS o CSP.
6. Si hay un reparto abierto, comprobá con oficina que el chofer siga viendo su salida y sus pendientes. No crees ventas ficticias en producción para probar.
7. Registrá versiones, hora y resultado. Si falla, guardá el mensaje y `traceId` cuando exista, sin información personal ni credenciales.

La raíz de la API o Swagger no son el control de salud de producción. Swagger está condicionado a Development en el código.

## Consultas lentas

La API anota en el log cada consulta a PostgreSQL que tarde más de **500 ms**, con
el aviso `Consulta lenta: <ms> ms.` y el SQL. Los valores van como parámetros
(`@p0`, `@p1`), así que el log no muestra datos de clientes. El umbral se cambia
con la variable `Database__SlowQueryMs` en Railway, sin publicar código.

Para revisarlo, buscá «Consulta lenta» en los logs de la API en Railway. Algún
aviso suelto en la recepción o en un informe es normal. Si se repite la misma
consulta durante el reparto, hay que mirarla: también aparecen las esperas por el
bloqueo de una salida, que avisan de visitas que chocan entre sí.

Para ver qué consultas consumen más en total, se puede activar la extensión
`pg_stat_statements` en la base de Railway. La activa el dueño: es configuración de
producción.

## Scripts de la carpeta común

Los cinco scripts siguientes están **fuera** de los repositorios, en la carpeta que contiene `GM-SoftBE` y `GM-SoftFE`. Una descarga de un solo repo no los incluye.

Ejecutalos desde esa carpeta en PowerShell. No los ejecutes solo para comprobar si arrancan: cuatro escriben operaciones reales. Usá siempre `-Api` explícito y, donde corresponde, `-Archivo` y `-Patente`; hay valores predeterminados particulares que no sirven para cualquier operación.

En una sesión de PowerShell, pedí el destino sin incluirlo en el comando compartido:

```powershell
$apiPublica = Read-Host 'Origen de la API destino, sin /api ni barra final'
```

Los scripts piden usuario ADMIN y clave. Dejá que la clave se solicite de forma interactiva; no pases `-Clave` en la línea de comandos ni guardes la salida con datos personales en Git.

### cargar-zonas.ps1

[`cargar-zonas.ps1`](../../cargar-zonas.ps1) crea una lista fija de zonas incluida en el propio script. Consulta las existentes y saltea los nombres encontrados. No es un importador de zonas desde una planilla y no ofrece un modo de simulación.

```powershell
powershell -NoProfile -File .\cargar-zonas.ps1 -Api $apiPublica
```

Después de autenticar comienza las altas. Revisá la lista fija antes de correrlo y mirá el resumen de creadas, salteadas y fallas. No lo uses para cambiar zonas existentes.

### cargar-clientes.ps1

[`cargar-clientes.ps1`](../../cargar-clientes.ps1) lee un CSV UTF-8 separado por `;`. Crea clientes para un camión ya existente y ajusta sus envases iniciales por producto usando la API. Zonas y productos deben estar creados.

Columnas usadas: `orden`, `codigo_viejo`, `nombre`, `direccion`, `telefono`, `zona`, `dias`, `deuda`, `nota`, `envases_bidon12`, `envases_bidon20` y `envases_soda`. Los días aceptan abreviaturas de lunes a domingo separadas por comas o espacios.

Ejemplo con archivo y patente **ficticios**; el archivo se prepara aparte:

```powershell
powershell -NoProfile -File .\cargar-clientes.ps1 -Api $apiPublica -Archivo .\ejemplos\clientes-ficticios.csv -Patente ZZ000ZZ
```

Primero muestra una revisión y pide continuar. `-Si` omite esa confirmación; no se recomienda para una carga manual revisada.

Límites que hay que revisar:

- Crea en el orden físico de las filas; `orden` se usa en el informe, no para ordenar la importación.
- Reconoce existentes por nombre y dirección normalizados dentro del camión. No es una identidad universal del cliente.
- Si una corrida quedó a medias, intenta completar envases que todavía están en cero. No lo uses como sincronización después de empezar el reparto: un saldo cero también puede ser resultado de devoluciones reales.
- Busca productos por texto que contenga `12`, `20` o `soda/sifon`, tomando el primero. Revisá el mapeo que muestra antes de confirmar.
- La deuda anterior se copia a **notas**. No crea deuda monetaria en la cuenta corriente.
- Las altas y ajustes son pedidos separados. Puede quedar una carga parcial si falla; no hay una transacción de toda la planilla.
- Escribe un archivo `.resultado.csv` junto al archivo de entrada. Contiene información de clientes y debe mantenerse privado.

### corregir-carga.ps1

[`corregir-carga.ps1`](../../corregir-carga.ps1) es una herramienta excepcional para una **salida abierta** con carga mal anotada. No es el formulario de corrección de una recepción ya cerrada.

```powershell
powershell -NoProfile -File .\corregir-carga.ps1 -Api $apiPublica -Patente ZZ000ZZ
```

Pregunta por cantidades reales y kilometraje. Muestra un plan y exige escribir **SI** antes de cambiar nada.

- Si salió más de lo anotado, agrega una recarga por la diferencia.
- Si salió menos, incluye lo que nunca salió entre los llenos declarados devueltos al recibir.
- Después hace la recepción. La rendición de efectivo queda pendiente; se completa desde el detalle de la salida, sección Liquidación.

Este procedimiento compensa registros; no reescribe la carga inicial y sus devoluciones declaradas pueden diferir de la descarga física. Revisalo con oficina para interpretar después los reportes. Una recarga y el cierre son pedidos separados: si se corta entre ambos, consultá la salida antes de volver a correrlo. La búsqueda de camión consulta los primeros 100 vehículos.

### corregir-zonas.ps1

[`corregir-zonas.ps1`](../../corregir-zonas.ps1) corrige la zona de clientes de un camión
ya cargados. Exige `-Api`, `-Archivo` y `-Patente`; pide usuario y clave sin mostrarla.
`-Usuario` y `-Clave` permiten pruebas locales; `-Clave` solo se admite con API local.

El CSV es UTF-8 separado por `;`, con `nombre;direccion;zona_real` y, opcionalmente,
`codigo_viejo`. El código viejo se muestra como referencia: la búsqueda usa nombre y
dirección del cliente dentro del camión, sin distinguir mayúsculas, acentos ni espacios
repetidos, igual que cargar-clientes. Los nombres ambiguos o duplicados y las zonas
inexistentes o inactivas se informan y deben corregirse antes de guardar.

```powershell
powershell -NoProfile -File .\corregir-zonas.ps1 -Api $apiPublica -Archivo .\ejemplos\zonas-ficticias.csv -Patente ZZ000ZZ
```

Muestra **cliente: zona actual → zona real**, saltea los que ya están bien e informa
los no encontrados. Exige escribir **SI**. Antes de cada PUT vuelve a leer el cliente
por ID para conservar todos los demás campos; si cambió de camión o zona, informa
el conflicto. El backend lo ubica al final del orden de la nueva zona. No toca envases
ni ventas. Revisá después la agenda del camión para incluir sus zonas reales.

Cada cliente se guarda por separado: una falla puede dejar parte del plan aplicada.
El resumen informa cambios y fallas; una nueva corrida saltea los ya corregidos.

### verificar-produccion.ps1

[`verificar-produccion.ps1`](../../verificar-produccion.ps1) compara el CSV con la API. Después del login hace lecturas de negocio: salud, vehículo, chofer, catálogo, clientes, orden, envases, carga y horario. No crea ventas ni modifica esas entidades.

```powershell
powershell -NoProfile -File .\verificar-produccion.ps1 -Api $apiPublica -Archivo .\ejemplos\clientes-ficticios.csv -Patente ZZ000ZZ
```

Es un verificador puntual de importación, **no una prueba general de producción**. Espera tres familias de productos por saldo y comprueba específicamente que los clientes visiten los **jueves**, aunque el CSV tenga otros días. Puede señalar diferencias de envases legítimas si ya hubo visitas.

Leé cada **MAL**, **OJO** y el resumen: no todos los fallos terminan con código de salida distinto de cero. Su salida puede mostrar datos personales; no la publiques. `-Archivo` permite elegir el archivo autorizado sin usar su valor predeterminado.

## Puntos «A confirmar»

Son comprobaciones del entorno, no funciones pendientes del producto:

- **«A confirmar»:** backups de PostgreSQL realmente habilitados, frecuencia, retención, ubicación privada de una copia externa y responsable de recuperación.
- **«A confirmar»:** última restauración ensayada y tiempo necesario para recuperar el servicio. No hay evidencia de ese ensayo en esta revisión.
- **«A confirmar»:** valor efectivo de `Database__MigrateOnStartup`, commits activos de ambos servicios y última ejecución de CI. El repositorio muestra el mecanismo, no las variables ni el estado vivo de Railway.
- **«A confirmar»:** retención y disponibilidad de rollback en el plan y servicios actuales. La guía oficial describe el mecanismo; no se inspeccionó la cuenta.

## Evidencia y mantenimiento

Fuentes del proyecto: [`Program.cs`](../GMSoft.API/Program.cs), [`railway.json` backend](../railway.json), [`railway.json` web](../../GM-SoftFE/railway.json), [`Dockerfile` web](../../GM-SoftFE/Dockerfile), [CSP de Vite](../../GM-SoftFE/vite.config.ts), [nginx](../../GM-SoftFE/nginx.conf.template) y los cinco scripts enlazados. Los procedimientos de Railway se contrastaron con sus guías oficiales; su disponibilidad efectiva se verifica en el entorno.

Al cambiar el arranque, los scripts o el proceso de publicación, actualizá esta guía en el mismo trabajo. No pegues valores de variables privadas ni registros de clientes para documentar una incidencia.

## Corregir el kilometraje

ADMIN puede corregir la base desde Vehículos → Editar. Para bajarla debe escribir un motivo (hasta 300 caracteres) y confirmar; el camión no puede tener una salida abierta. Quedan registrados el valor anterior, el nuevo, el motivo, la fecha UTC y el usuario. Las salidas anteriores no cambian. La próxima salida admite kilómetros iguales o mayores a la nueva base.

`PUT /api/vehicles/{id}` admite `kilometersReason` opcional; al bajar es obligatorio (400 sin motivo, 409 con salida abierta). Subir o mantener el valor no genera una corrección. `GET /api/vehicles/{id}/kilometers-corrections` es solo ADMIN y devuelve `correctedAt`, `previousKilometers`, `newKilometers`, `reason` y `correctedByName`, de más reciente a más antigua. El historial aparece debajo del formulario cuando tiene registros.
