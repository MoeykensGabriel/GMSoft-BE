# GMSoft — arquitectura técnica

**Fecha:** 19 de octubre de 2026. **Fuentes:** backend `619bacc` y frontend `efed7ed`, sin modificaciones locales al comenzar esta revisión. El commit de backend incluye documentación; la lógica analizada corresponde a sus archivos de aplicación vigentes.

Este documento se limita a tecnologías, dependencias, datos, transacciones, seguridad, contenedores y operación. Describe el código y los archivos de despliegue, no un entorno productivo inspeccionado. No se ejecutaron contenedores, pruebas de carga ni un pentest; tampoco se accedió a la base o a secretos de ejecución.

## 1. Resumen técnico

La solución contiene una **SPA React**, una **API ASP.NET Core .NET 10 organizada por capas** y una **base relacional PostgreSQL**. El backend se despliega como una aplicación: es un monolito modular, no un conjunto de microservicios.

El navegador descarga HTML, JavaScript y CSS desde Nginx. Ese JavaScript realiza peticiones HTTP/JSON directamente al origen configurado de la API, con un JWT Bearer. No hay acceso directo del navegador a PostgreSQL ni un proxy `/api` definido en la plantilla Nginx actual.

En producción están preparados dos Dockerfiles con etapas separadas de construcción y ejecución. La topología documentada añade PostgreSQL como servicio externo y usa el proxy de la plataforma para terminar HTTPS. Los archivos no acreditan que esa topología esté desplegada hoy.

## 2. Tecnologías y función de cada una

Las versiones siguientes son las declaradas en los manifiestos revisados. No son una comprobación de vulnerabilidades ni una afirmación de que sean las últimas disponibles.

| Área | Tecnología | Papel en la solución |
| --- | --- | --- |
| Interfaz | React `^19.2.8`, TypeScript `~6.0.2` | SPA con componentes tipados. |
| Construcción web | Vite `^8.3.4`, Node 22 en Docker/CI | Transformación y empaquetado; Node no integra la imagen final de la web. |
| Presentación | Tailwind CSS `^4.3.3` | Tokens y utilidades de estilo. |
| Navegación | React Router `^7.18.3` | Rutas protegidas y carga diferida del panel. |
| Estado remoto | TanStack Query `^5.102.8` | Caché en memoria, consultas e invalidación. |
| Gráficos | Recharts `^3.10.1` | Visualizaciones; carga diferida de la pantalla que lo usa. |
| Servidor | C# / ASP.NET Core sobre `net10.0` | API HTTP, inyección de dependencias y middleware. |
| Casos de uso | MediatR `14.2.0` | Comandos, consultas y pipeline interno. No es un broker de mensajes. |
| Validación / mapeos | FluentValidation `12.1.1`, Mapster `10.0.11` | Validación de requests y transformación entre modelos. |
| Persistencia | EF Core `10.0.11`, Npgsql EF `10.0.3` | Acceso a PostgreSQL, modelo, migraciones y transacciones. |
| Identidad | ASP.NET Core Identity EF `10.0.11` | Usuarios, hashes de contraseña, roles, bloqueo y sello de seguridad. |
| Tokens | JwtBearer `10.0.11`, JWT `8.22.0` | Emisión HS256 y validación de tokens. |
| Diagnóstico | Serilog ASP.NET `10.0.0`, sink de archivos `7.0.0` | Logs HTTP, errores y archivos de ejecución. |
| Contrato HTTP | Swagger / Swashbuckle `10.2.3` | Exploración de la API habilitada en Development. |
| Servidor web | `nginx:alpine` | Archivos estáticos, rutas SPA, cabeceras y caché. |
| Automatización | GitHub Actions, xUnit, Node test runner y scripts Python | Verificaciones de compilación, arquitectura e integración. |

PostgreSQL 16 aparece como servicio de CI. El repositorio no fija con ello la versión de la base productiva.

## 3. Organización y dependencias

### Frontend

`src/app` compone providers y rutas. `src/modules` agrupa capacidades; `core` contiene componentes y mecanismos técnicos compartidos. Los módulos exponen puntos de entrada y dependency-cruiser comprueba los límites definidos en `.dependency-cruiser.cjs`.

El cliente HTTP centraliza `fetch`, el encabezado `Authorization`, la interpretación de ProblemDetails y la reacción ante `401`. `403` mantiene la identidad y muestra falta de permiso. La caché de TanStack Query no sustituye las validaciones del servidor.

### Backend

| Proyecto | Responsabilidad | Referencias propias |
| --- | --- | --- |
| `GMSoft.API` | Controllers, middleware, autenticación y composición de servicios. | Application y Data. |
| `GMSoft.Application` | Handlers, contratos, validación y DTOs. | Domain. |
| `GMSoft.Domain` | Entidades y enumeraciones independientes de infraestructura. | Ninguna otra capa. |
| `GMSoft.Data` | EF Core, Identity, repositorios y servicios concretos. | Application y Domain. |

**En ejecución**, un handler invoca una interfaz de repositorio y la implementación de Data accede a PostgreSQL. **En compilación**, Application no referencia Data: Data implementa las interfaces de Application y API conecta ambas mediante DI.

El `AppDbContext` tiene ciclo de vida scoped y se expone también como `IUnitOfWork`; ambas resoluciones usan la misma instancia dentro del scope. MediatR y su `ValidationBehaviour` invocan los validadores registrados antes de ejecutar cada handler. No existe una transacción global aplicada automáticamente a todos los handlers: los casos que la necesitan la abren explícitamente.

No se identificaron Redis, RabbitMQ, Kafka, un motor NoSQL, Kubernetes ni un servidor de renderizado React como parte de esta arquitectura. La separación comandos/consultas usa una misma base: no constituye por sí sola CQRS distribuido ni event sourcing.

## 4. Datos y persistencia

### Modelo físico observado

`AppDbContext` hereda de `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`. Comparte contexto y base para las tablas de Identity y las entidades de la aplicación. La configuración de cada entidad está separada en clases `IEntityTypeConfiguration`.

| Aspecto | Implementación |
| --- | --- |
| Identificadores | `Guid` en C#, UUID en PostgreSQL. Se completan al guardar si estaban vacíos. |
| Importes | `decimal` en C#; columnas con precisión `numeric(18,2)` en precios y pagos, entre otras. |
| Fechas | Registros UTC; filtros de día de negocio se convierten con `BusinessTime`. |
| Huso de negocio | Offset fijo UTC−3 en backend. No es una base completa de zonas horarias configurable. |
| Colecciones de días | `integer[]` de PostgreSQL en configuraciones correspondientes. |
| Enumeraciones | Varias columnas convierten enums a enteros; el serializador HTTP emite nombres textuales. |
| Integridad referencial | FK y `DeleteBehavior.Restrict` en relaciones que deben conservar historial. |
| Límites | Longitudes de texto, valores requeridos, índices y algunos `CHECK` de cantidades/configuración. |

El modelo combina entidades de estado, saldos actuales y registros de movimientos. Esos movimientos permiten reconstruir operaciones concretas; no se encontró una infraestructura de eventos que vuelva todas las tablas proyecciones de un único log inmutable.

### Índices y restricciones relevantes

- Índices únicos parciales sobre `DeliverySessions.VehicleId` y `DriverId` cuando `Status = 0`.
- `Deliveries.ClientRequestId` único, permitiendo valores nulos heredados.
- `SessionRestocks.ClientRequestId` y claves de registro/cierre de promociones únicas.
- Par `(CustomerId, ProductId)` único para saldos y precios particulares.
- Número de serie único en `ContainerUnits`.
- Índices compuestos por identidad de recurso, estado o fecha para consultas de saldos e historial.
- `CHECK` en líneas de promociones y configuraciones; no debe inferirse que toda regla de aplicación está duplicada como constraint SQL.

Estas restricciones siguen actuando si llegan dos requests simultáneos. Una validación previa en C# no reemplaza una unicidad garantizada por la base.

### Auditoría y bajas lógicas

`SaveChangesAsync` completa `CreatedAt` y `UpdatedAt` en entidades que heredan de `BaseEntity`; transforma sus estados EF `Deleted` en `IsDeleted = true` y registra `DeletedAt`. Un filtro global excluye esas filas de consultas habituales.

Las tablas de Identity y otras clases que no heredan de esa base quedan fuera de ese mecanismo. Por tanto, no corresponde afirmar que todas las tablas del sistema tienen la misma política de borrado. Las fechas de auditoría tampoco conforman por sí solas una bitácora inviolable de cada valor anterior.

### Consultas y cambios directos en SQL

Las consultas combinan LINQ/EF Core con SQL específico de PostgreSQL. En ejemplos críticos se usa `FromSqlInterpolated` / `ExecuteSqlInterpolatedAsync`, parametrizando los valores. También aparecen sentencias raw constantes para bloqueos. Debe mantenerse esa distinción frente a concatenar entradas del request en una cadena SQL.

`ContainerBalanceRepository.AdjustAsync` realiza un `INSERT ... ON CONFLICT ... DO UPDATE` que suma el delta en la base. Evita el patrón de leer un saldo, incrementarlo en memoria y sobrescribir el cambio de otro request. Luego recarga cualquier copia seguida por el ChangeTracker para no seguir operando con un valor anterior.

## 5. Transacciones, concurrencia e idempotencia

### Límite transaccional

`IUnitOfWork.ExecuteInTransactionAsync` implementa esta estructura:

```text
Database.CreateExecutionStrategy()
  └─ BeginTransactionAsync
       ├─ Ejecutar el caso de uso
       ├─ Uno o varios SaveChangesAsync
       └─ CommitAsync
```

Si la acción lanza una excepción antes del commit, la transacción se descarta al salir del `await using`, sin confirmar sus cambios. Varios `SaveChangesAsync` dentro de ella no son varios commits independientes.

No se especifica un `IsolationLevel` en ese método; usa el predeterminado del proveedor/base. No debe describirse como una transacción `Serializable` explícita ni como una transacción distribuida entre navegador, API y base.

Existe una llamada a `CreateExecutionStrategy`, pero no se identificó `EnableRetryOnFailure` en la configuración revisada. El uso de esa estrategia no basta para afirmar que se habilitaron reintentos automáticos ante toda caída de PostgreSQL. Tampoco garantiza que cualquier handler sea seguro de repetir con las mismas entidades seguidas en memoria.

### Mecanismos de concurrencia

| Mecanismo | Uso observado | Límite |
| --- | --- | --- |
| `SELECT ... FOR UPDATE` | Filas de salida, vehículo, chofer y unidades; lectura bloqueada de saldos en operaciones específicas. | Coordina escritores del recurso durante la transacción; no serializa toda la aplicación. |
| UPSERT atómico | Creación o ajuste incremental de saldos. | Protege la suma; las validaciones de límites requieren su coordinación adicional. |
| Índice único / parcial | Identificadores de reintento y exclusión de registros abiertos duplicados. | Un conflicto debe traducirse o resolverse según el caso, no siempre es un reintento válido. |
| Advisory locks transaccionales | Promociones y solicitudes de baja. | El código que requiere esa coordinación debe usar las mismas claves/convención. |
| `LOCK TABLE ... SHARE ROW EXCLUSIVE` | Guardado de reorganizaciones de clientes. | Es deliberadamente amplio: puede hacer esperar escrituras de otras zonas. |
| Concurrencia optimista | `Promotion.Status` marcado con `IsConcurrencyToken`. | Es selectiva; no se observó una versión optimista uniforme en todas las entidades. |
| Versión opaca del listado | Edición de recorridos. | Una versión vencida exige recargar; no convierte ese PUT en idempotente. |

Las excepciones de concurrencia EF se traducen a `409`. Algunas violaciones de índices únicos tienen traducción específica en `AppDbContext`; no se debe generalizar que cualquier error de integridad queda tratado de la misma manera.

### Secuencia técnica representativa

1. La API autentica, autoriza y valida el request.
2. Se comprueba la clave de reintento cuando corresponde.
3. Se inicia la transacción y se bloquea el recurso compartido.
4. Se vuelve a leer el estado y, en los flujos que lo implementan, la clave bajo el bloqueo.
5. Se realizan las validaciones dependientes de los datos actuales.
6. Se escriben entidad, detalles, movimientos, saldos y otros registros asociados.
7. Se confirma la transacción y se construye la respuesta.

Una respuesta HTTP puede perderse después del commit. Eso no significa que la base haya deshecho la operación. Por ese motivo el cliente reenvía la misma clave y el servidor debe devolver el registro previo en lugar de volver a escribirlo.

### Idempotencia y outbox del navegador

La outbox reside en `localStorage`, separada por usuario y por identificador. Guarda cuerpo, destino y consultas a invalidar antes de enviar. `OutboxSender` reintenta desde una pantalla activa al entrar, cada 30 segundos y al recibir el evento `online`.

- `clientRequestId` distingue un envío lógico y se conserva al repetirlo.
- `expectedSessionId` evita imputar un pendiente a un recurso de contexto nuevo.
- La API combina búsqueda de la operación previa, autorización de su propietario e integridad SQL.
- Rechazos definitivos quedan para revisar; fallos de red o respuesta incierta pueden reintentarse.
- Si falla el almacenamiento local, existe un respaldo en memoria con menor durabilidad.

Esta es una **bandeja de reenvío del navegador**, no el patrón transactional outbox del servidor: no se guarda en la transacción de PostgreSQL ni tiene un worker persistente que siga procesando con la aplicación cerrada. No hay cacheo integral offline en el service worker. El diseño busca efectos idempotentes; no ofrece una garantía universal de entrega “exactly once”.

## 6. Seguridad

### Autenticación e identidad

- Identity maneja usuarios, roles y hashes de contraseña. No se usa un sistema de contraseñas reversibles diseñado a medida.
- El emisor crea JWT firmados con **HS256**. La clave proviene de `JWT_SECRET_KEY` o configuración; nunca debe incorporarse al bundle Vite.
- La API valida emisor, audiencia, vencimiento y firma, con `ClockSkew = 0`.
- Los claims incluyen identidad, roles, `jti`, sello de seguridad y referencias adicionales cuando corresponden.
- `OnTokenValidated` consulta que la cuenta siga activa y valida el sello. Por ello, la autenticación JWT revisada tiene también una dependencia de lectura de la base; no elimina todo acceso al servidor de identidad en cada request.
- El fallback de duración en el emisor es 720 minutos, reemplazable por configuración. El keep-alive requiere un JWT todavía válido; no es un refresh token independiente que rescate un acceso ya vencido.

Hay una compatibilidad explícita con tokens antiguos sin `securityStamp`: se admite la ausencia del claim si la cuenta sigue activa. No corresponde prometer que un cambio de contraseña revoca también esos tokens heredados. Si ya no se necesitan, retirar esa compatibilidad es un endurecimiento a evaluar.

### Autorización

La política fallback exige autenticación salvo `[AllowAnonymous]`, como login y `/api/health`. Los controllers agregan requisitos de rol y los casos de uso comprueban la pertenencia/acceso al recurso. La protección de rutas React mejora la navegación, pero no es el control de seguridad definitivo.

El JWT vive en `localStorage`. Esto facilita persistir la cuenta, pero un script que se ejecute en el origen podría leerlo. La CSP reduce superficie de inyección; no transforma ese almacenamiento en una cookie HttpOnly. El frontend limpia token y caché ante un `401` válido para la cuenta actual.

### Protección del login

El login tiene una ventana de **10 intentos por IP por minuto**, sin cola, que rechaza con `429`. Identity tiene **5 intentos fallidos y bloqueo de 15 minutos**. Son controles complementarios: uno se vincula a la IP y otro a la cuenta.

La política de contraseña fija expresamente mínimo de 8 caracteres, dígito y mayúscula; desactiva la exigencia de símbolos. Otros valores no sobrescritos conservan el comportamiento de Identity. Los errores comunes de credenciales no distinguen usuario inexistente de contraseña incorrecta; las cuentas bloqueadas o desactivadas tienen respuestas específicas.

### Navegador, cabeceras y orígenes

| Control | Implementación observada |
| --- | --- |
| CORS | Orígenes concretos desde `CORS_ORIGINS` / configuración. No sustituye autenticación ni restringe clientes HTTP ajenos al navegador. |
| CSP del documento | Vite inyecta una política en el build: scripts propios, origen de API en `connect-src`, sin objetos embebidos. Se permiten estilos inline. |
| Protección contra framing | Nginx agrega `X-Frame-Options: DENY` y `frame-ancestors 'none'` para el documento según sus bloques de configuración. |
| Tipos de contenido | `nosniff` en la API y en Nginx. |
| Otras cabeceras web | Referrer-Policy, Permissions-Policy y HSTS en la plantilla, con configuración específica por location. |
| Swagger | Disponible solo en Development según `Program.cs`. |
| Errores | ProblemDetails; los `500` públicos tienen mensaje genérico y un `traceId`. El detalle técnico queda en logs. |

La política completa del documento está en una meta CSP; `frame-ancestors` se entrega por cabecera. No se debe confundir la cabecera CSP parcial de Nginx con la política completa construida por Vite.

### Proxy y TLS

La API habilita forwarded headers al detectar Railway o mediante configuración. Lee un salto de `X-Real-IP` y el protocolo reenviado, vaciando las listas de proxies/redes conocidos. Ese modo depende de que el servicio reciba tráfico a través de un proxy de confianza. Exponerlo directamente con ese modo habilitado permitiría confiar en cabeceras que no se deberían aceptar de cualquier cliente.

En producción los archivos esperan TLS terminado por la plataforma, mientras API y Nginx escuchan HTTP en su puerto interno. No se incluyen certificados ni un listener HTTPS productivo en estos Dockerfiles.

Para una URI de PostgreSQL, el código usa `SslMode.Prefer` si no viene otra opción y respeta `sslmode` explícito. Prefer no obliga a cifrar la conexión si el servidor no ofrece TLS. La exigencia de TLS y validación de certificado de la base debe definirse para la red real; no puede inferirse del hecho de que la web use HTTPS.

### Secretos y límites de la revisión

La clave JWT, conexión a PostgreSQL y contraseña inicial se proporcionan por entorno/configuración privada. Las variables `VITE_*` forman parte del material público de la web y no son un almacén de secretos. `.dockerignore` excluye `.env` y configuraciones de desarrollo en los contextos correspondientes.

El arranque comprueba que exista una clave JWT; no se identificó una validación explícita de entropía o longitud en esa comprobación inicial. No se verificaron rotación, copias de seguridad, controles de acceso de la plataforma ni cifrado en reposo. Son propiedades del entorno operativo, no garantías demostradas por el repositorio.

## 7. Docker y despliegue

### Imagen de API

```text
mcr.microsoft.com/dotnet/sdk:10.0
  restore del proyecto API → publish Release
                 ↓ copiar publicación
mcr.microsoft.com/dotnet/aspnet:10.0
  libgssapi-krb5-2 + directorio de logs
  USER app → dotnet GMSoft.API.dll
  HTTP 0.0.0.0:$PORT (fallback 8080)
```

La imagen final contiene la publicación, no el SDK ni las suites de pruebas. La librería de sistema agregada es una dependencia de ejecución; su presencia no significa que se haya implementado un proveedor de autenticación Kerberos para usuarios.

### Imagen web

```text
node:22-alpine
  npm ci → npm run build
  ARG VITE_API_URL → incorporado a dist/
                 ↓ copiar estáticos
nginx:alpine
  plantilla con PORT → servir dist/
```

`VITE_API_URL` es un valor de **construcción**. Cambiarlo requiere reconstruir la web; reiniciar el mismo contenedor no reescribe el bundle ni su CSP. En cambio, `PORT` lo resuelve Nginx al arrancar mediante la plantilla y el filtro de sustitución.

La web final no ejecuta Node ni Vite. El Dockerfile no declara un `USER` no root para Nginx; no debe atribuirse a ambas imágenes la reducción de privilegios que sí es explícita en la API.

### Caché y rutas

- Las rutas SPA hacen fallback a `index.html`.
- `index.html` usa `Cache-Control: no-store`.
- `/assets/` usa caché de un año e `immutable`.
- Service worker y manifiesto se revisan con `no-cache`.
- La estrategia no equivale a un frontend operativo completamente sin conexión.

### Orquestación y persistencia

Se encontraron `Dockerfile` y `railway.json` para cada servicio, pero **no Docker Compose**. `railway.json` selecciona Dockerfile y el backend define `/api/health` como healthcheck. No crea por sí mismo los servicios ni valida que ya estén publicados.

La guía propone web, API y PostgreSQL en un entorno Railway con conexión privada a la base. PostgreSQL no vive en las imágenes de las aplicaciones. No se define en esos Dockerfiles su volumen, versión productiva, backup o restauración. Recrear un contenedor de API o web no debería confundirse con recrear el servicio de datos.

Las etiquetas de las imágenes base son mutables y no están fijadas por digest. La API escribe logs a consola y archivo; no se identifica un volumen persistente para `/app/logs` en los archivos de despliegue revisados. El sistema de logs de la plataforma debe formar parte del diseño operativo.

## 8. Migraciones, observabilidad y CI

### Migraciones

EF Core mantiene migraciones en Data. Se aplican al iniciar en Development o cuando `Database:MigrateOnStartup` está habilitado. Un fallo en esa etapa impide completar el arranque; no se sigue sirviendo una API con ese error ignorado. El seed de administrador corre en esa misma secuencia, pero su éxito debe comprobarse por sus propios resultados.

No se identificó una fase separada de migración previa al despliegue de múltiples réplicas. Si se escala la API, conviene definir quién ejecuta las migraciones y qué compatibilidad existe entre versiones de código y esquema. El mecanismo actual no prueba una estrategia de despliegue sin interrupciones.

### Observabilidad

Serilog registra método, ruta, estado y duración. ProblemDetails agrega `traceId` para relacionar un error con su log. El manejador diferencia errores esperados, fallos `500` y cancelaciones de conexión.

`GET /api/health` devuelve estado y hora sin consultar dependencias: es un ping del proceso, **no una prueba de disponibilidad de PostgreSQL**. No se identificó configuración de métricas centralizadas, trazas distribuidas o alertas operativas en esta revisión.

### CI existente

| Frontend | Backend |
| --- | --- |
| Node 22 y `npm ci`. | SDK .NET 10 y PostgreSQL 16 efímero. |
| Lint y build con chequeo TypeScript. | Restore y build Release. |
| Tests con Node test runner. | Tests unitarios y de integración con xUnit. |
| Dependency-cruiser. | Verificación de modelo/migraciones y suite HTTP contra la API. |

Los workflows están definidos para push y pull request. No equivalen a resultados verdes comprobados en esta tarea. Tampoco se identificaron pasos que construyan y escaneen las dos imágenes Docker, analicen dependencias por CVE o prueben restaurar un backup.

## 9. Evaluación técnica y próximos puntos concretos

| Punto | Qué está resuelto | Qué falta demostrar o definir |
| --- | --- | --- |
| Arquitectura | Dependencias hacia Domain y contratos aislando infraestructura. | Sostener esos límites al aumentar los módulos. |
| Datos concurrentes | Transacciones, índices, UPSERT y bloqueos específicos. | Comportamiento bajo carga, tiempos de espera y política explícita ante errores transitorios. |
| Reintentos | Identificadores y recuperación de operaciones ya registradas. | No generalizar idempotencia a todos los endpoints; no existe entrega universal exactamente una vez. |
| Tokens | Firma, vencimiento, roles y revisión de cuenta/sello. | Exposición de localStorage, compatibilidad de tokens sin sello y política de rotación. |
| Perímetro | CORS, CSP y cabeceras; proxy configurable. | Confirmar TLS público, confianza del proxy y TLS de la base en el entorno real. |
| Contenedores | Builds multietapa; API con usuario no root. | Usuario de Nginx, fijación de imágenes, análisis de imágenes y límites de recursos. |
| Durabilidad | PostgreSQL separado de los artefactos de aplicación. | Backups, retención, restauración ensayada y permisos del usuario de BD. |
| Operación | Logs, traceId, healthcheck y migración configurable. | Readiness de BD, métricas, alertas y procedimiento de migración/escalado. |

Son conclusiones de inspección técnica y asuntos a verificar, no una declaración de que producción esté expuesta ni una certificación de seguridad. Esta tarea no cambia esos controles.

## 10. Diagramas y fuentes

- [Stack y capas — PNG](stack-capas.png) · [SVG](stack-capas.svg).
- [Datos y transacciones — PNG](datos-transacciones.png) · [SVG](datos-transacciones.svg).
- [Docker, red y seguridad — PNG](docker-seguridad.png) · [SVG](docker-seguridad.svg).
- [Las tres láminas editables en Excalidraw](gmsoft-tecnico.excalidraw).
- [Generador técnico](generar_diagramas.py), que reutiliza las primitivas de boceto de `../caso-de-estudio/generar_diagramas.py`.

En la lámina de capas, las líneas discontinuas indican dependencias entre proyectos y las sólidas comunicación. En la de Docker, los bloques de build son etapas de construcción: no contenedores que deban quedar corriendo. La topología de esa lámina es la prevista en los archivos, no una captura de infraestructura viva.

| Evidencia local | Uso |
| --- | --- |
| [Program.cs](../../GMSoft.API/Program.cs), [AuthController](../../GMSoft.API/Controllers/AuthController.cs), [HealthController](../../GMSoft.API/Controllers/HealthController.cs) | Pipeline, JWT, autorización, rate limit, compresión, proxy, Swagger y healthcheck. |
| [DataLayerExtensions](../../GMSoft.Data/Extensions/DataLayerExtensions.cs), [ApplicationLayerExtensions](../../GMSoft.Application/Extensions/ApplicationLayerExtensions.cs) | DI, proveedores, Identity y validaciones. |
| [AppDbContext](../../GMSoft.Data/Context/AppDbContext.cs), [IUnitOfWork](../../GMSoft.Application/Common/Interfaces/IUnitOfWork.cs) | Auditoría, transacciones y excepciones de restricciones. |
| [Configuraciones EF](../../GMSoft.Data/Configurations), [repositorios](../../GMSoft.Data/Repositories) | Tipos, índices, FK, SQL, bloqueos y UPSERT. |
| [JwtTokenService](../../GMSoft.Data/Services/JwtTokenService.cs), [IdentityService](../../GMSoft.Data/Services/IdentityService.cs) | Claims, firma, expiración, hashes y revisión del sello. |
| [Manejador de errores](../../GMSoft.API/Middleware/GlobalExceptionHandler.cs) | ProblemDetails y correlación. |
| [Cliente HTTP](../../../GM-SoftFE/src/modules/core/lib/api.ts), [queryClient](../../../GM-SoftFE/src/modules/core/lib/queryClient.ts), [outbox](../../../GM-SoftFE/src/modules/core/lib/outbox.ts) | Estado del navegador y reenvíos. |
| [Vite](../../../GM-SoftFE/vite.config.ts), [Nginx](../../../GM-SoftFE/nginx.conf.template), [service worker](../../../GM-SoftFE/public/sw.js) | CSP, puertos, caché, cabeceras y límites offline. |
| [Dockerfile API](../../Dockerfile), [Dockerfile web](../../../GM-SoftFE/Dockerfile), [guía Railway](../deploy-railway.md) | Construcción y despliegue preparados. |
| Workflows [API](../../.github/workflows/ci.yml) y [web](../../../GM-SoftFE/.github/workflows/ci.yml) | Automatización declarada. |

La revisión se basó en estas fuentes del proyecto. No se incluyeron credenciales, direcciones privadas de bases ni datos reales en las láminas.
