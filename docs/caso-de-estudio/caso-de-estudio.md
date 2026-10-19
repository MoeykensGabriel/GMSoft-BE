# GMSoft: caso de estudio de un sistema de reparto con envases retornables

Análisis del código disponible al **19 de octubre de 2026**.

**Referencia de lectura:** backend `b926aae`; frontend `efed7ed`. Ambos repositorios estaban sin modificaciones locales al iniciar la revisión. Este informe describe lo encontrado en el código y distingue sus límites de las capacidades deseadas. No acredita qué versión está desplegada ni sustituye una prueba funcional del sistema.

## 1. El problema que resuelve

Una distribuidora de agua en bidones y soda no necesita registrar solamente qué vendió. Cada visita puede cambiar, al mismo tiempo, la mercadería que lleva el camión, el dinero que debe el cliente y la cantidad de envases que tiene prestados.

Un cliente puede recibir cuatro bidones llenos, devolver tres vacíos y pagar más adelante. Registrar solo una venta deja sin explicar el cuarto envase; registrar solamente lo cobrado pierde la deuda; registrar la carga inicial sin controlar lo que vuelve impide detectar diferencias.

Cuando esas decisiones dependen de la memoria del chofer o de anotaciones separadas, aparecen problemas de coordinación: no saber qué camión salió, perder el orden habitual de las visitas, olvidar deudas, entregar envases que después no se recuperan o contar dos veces una operación cuando el celular pierde señal.

**GMSoft busca mantener una historia consistente de cada reparto, desde la preparación de la carga hasta la recepción del vehículo, vinculando ventas, cobros, stock y envases.**

Es un sistema de gestión operativa para una distribuidora con oficina y reparto. Su valor principal es la trazabilidad de la circulación de productos y envases, junto con el control de la cuenta del cliente.

## 2. Tres controles relacionados, con unidades diferentes

| Control | Pregunta que responde | Cómo se expresa |
| --- | --- | --- |
| Mercadería del camión | ¿Qué se cargó, entregó, recuperó y descargó? | Unidades por producto y estado lleno/vacío. |
| Envases y bienes prestados | ¿Qué tiene cada cliente y qué falta recuperar? | Cantidades por producto o unidades identificadas. |
| Cuenta del cliente | ¿Cuánto se vendió, cobró y quedó pendiente? | Importes monetarios. |

La deuda de dinero y la obligación de devolver envases son independientes. Pagar una venta no devuelve el envase; recuperar un envase no cancela por sí mismo una deuda de dinero.

Para productos con seguimiento por saldo:

`envases pendientes = saldo anterior + entregados − recuperados`

Para la cuenta monetaria:

`deuda = cargos por ventas − pagos registrados`

El inventario a bordo se calcula desde movimientos con signo y se separa por producto y estado. En una recepción, el saldo que queda después de descontar lo contado permite identificar diferencias. Estos registros de movimientos aportan trazabilidad; la aplicación no está implementada como una arquitectura de event sourcing.

## 3. Actores y responsabilidades

| Actor | Acceso al software | Responsabilidades |
| --- | --- | --- |
| ADMIN / oficina | Cuenta con rol `Admin`; panel orientado a escritorio. | Mantener catálogo, clientes, zonas, vehículos y choferes; organizar recorridos; preparar cargas; registrar recargas; recibir el camión; rendir/corregir caja y consultar reportes. |
| Chofer / repartidor | Cuenta con rol `Driver`; interfaz móvil. | Abrir su salida, consultar clientes habilitados, registrar ventas y cobros, recuperar envases, registrar visitas sin compra, dar altas desde una visita, gestionar pruebas y solicitar bajas. |
| Cliente | Sin cuenta ni portal en el alcance actual. | Recibir productos, pagar o quedar a cuenta y devolver envases. La oficina y el chofer registran esos hechos. |
| Prospecto | Sin cuenta; sus datos pertenecen a una promoción de prueba. | Probar el producto y decidir si continúa como cliente; devolver los envases si no continúa. |

ADMIN concentra la recepción y las correcciones. El chofer opera sobre el recorrido que le corresponde; los controles de alcance se aplican en el servidor, además de las pantallas protegidas del frontend.

No aparece un rol separado de mecánico. El usuario operativo del vehículo es el chofer. El personal que acerca una recarga tampoco tiene un rol propio: la registra ADMIN.

## 4. Circuito principal del negocio

1. **Preparar la empresa.** ADMIN crea productos, zonas, vehículos y cuentas de choferes. Asigna vehículo al chofer y configura clientes con camión, zona y días de visita.
2. **Preparar el vehículo.** ADMIN carga productos llenos y selecciona los días que cubrirá esa salida. El formulario presenta un resumen antes de registrar la carga. Varias tandas conservan su historial, mientras los saldos se presentan agrupados por producto.
3. **Abrir el reparto.** El chofer inicia sesión y abre una salida con zona y kilometraje. El vehículo proviene de su asignación y la carga pendiente pasa a esa salida. El código también permite que ADMIN abra una salida eligiendo un chofer.
4. **Visitar clientes.** Se consultan clientes del camión, zona y días de esa salida. Se respeta `RouteOrder`: nace del orden de alta y ADMIN puede reorganizarlo. Tocar una fila abre la ficha y permite registrar la operación.
5. **Registrar lo ocurrido.** Puede haber venta cobrada, venta a deuda, retiro de envases, préstamos habilitados o una visita sin compra con motivo. Las promociones de prospectos tienen un circuito separado. Los visitados se distinguen de los pendientes y de quienes pidieron pasar más tarde.
6. **Recargar si hace falta.** ADMIN incorpora otra tanda a la misma salida abierta. Una recarga no obliga a cerrar y abrir otro reparto.
7. **Recibir el camión.** ADMIN cuenta los llenos y vacíos que regresaron, registra kilómetros de cierre y puede registrar la rendición en la misma operación. El sistema compara lo esperado con lo recibido, conservando las diferencias.
8. **Cerrar el circuito.** La salida queda cerrada; el celular vuelve al login al detectar esa recepción. La detección es por consultas periódicas y reconexión, no una notificación instantánea garantizada sin conexión.

La sesión de reparto y la autenticación son conceptos distintos. Recargar la página no debería crear otra salida; que expire un token tampoco equivale a recibir físicamente el camión.

## 5. Requisitos funcionales observados

“Implementado” significa que se identificaron rutas, pantallas o casos de uso correspondientes. No significa que se hayan ejecutado nuevamente sus pruebas en esta revisión documental.

| ID | Requisito | Actor | Evidencia principal |
| --- | --- | --- | --- |
| RF-01 | Autenticar y autorizar por roles y por alcance del recorrido. | Ambos | Identity/JWT, controllers, `ProtectedRoute`, `CustomerRouteAccess`. |
| RF-02 | Crear y editar choferes, usuarios, vehículos, zonas y productos. | ADMIN | Módulos y controllers de catálogo, flota y personal. |
| RF-03 | Crear clientes con contacto, dirección, teléfono, camión, zona y días. | ADMIN | `CreateCustomerCommandValidator`, `CustomerForm`, rutas de clientes. |
| RF-04 | Dar de alta un cliente desde una visita, tomando camión y zona de la salida. | Chofer | `StreetCustomer`, registro de entregas. |
| RF-05 | Organizar el recorrido por camión, zona y día; guardar reordenamientos y cambios con control de versión. | ADMIN | `route-planning`, módulo `routePlanning`. |
| RF-06 | Configurar una agenda semanal del camión y detectar cobertura faltante. | ADMIN | `VehicleSchedule`, `schedule`; agenda orientativa, no bloqueo absoluto. |
| RF-07 | Preparar carga inicial de llenos, revisar un resumen y recuperar su historial. | ADMIN | `VehicleLoads`, `LoadConfirmModal`. |
| RF-08 | Abrir una sola salida por chofer y camión, con carga pendiente, zona y kilómetros válidos. | Chofer / ADMIN | `OpenSessionCommandHandler`. |
| RF-09 | Listar clientes en orden, buscar, abrir la ficha y separar pendientes/visitados/para más tarde. | Chofer | `CustomerRouteList`, `CustomerVisitView`. |
| RF-10 | Vender con cantidades no negativas, subtotales y total calculados, validando stock y precios en servidor. | Chofer | `RegisterDeliveryCommandHandler`, editor y formulario de venta. |
| RF-11 | Elegir “cobré / queda a deuda” y medio de pago sin copiar el importe. | Chofer | Modal de venta y contrato `payment` de entregas. |
| RF-12 | Registrar y consultar saldos y movimientos de envases, distinguiendo conteo de unidades seriadas. | Ambos, según operación | `CustomerContainerBalance`, `ContainerMovement`, `ContainerUnit`. |
| RF-13 | Prestar productos habilitados sin cargo y retirarlos, sin quitar la posibilidad de venderlos. | Chofer | `Product.CanBeLent`, `loansOut` / `loansIn`. |
| RF-14 | Registrar una visita sin compra con motivo y observaciones. | Chofer | `NoPurchase`, `useNoPurchaseWrite`. |
| RF-15 | Entregar una prueba gratuita a un prospecto, programar retiro y cerrar con o sin conversión a cliente. | Chofer; ADMIN consulta/configura | `PromotionsController`, módulo `promotions`. |
| RF-16 | Registrar recargas de varios productos sobre una salida abierta. | ADMIN | `/api/sessions/{id}/restocks`, `useRestockWrite`. |
| RF-17 | Recibir llenos y vacíos, cerrar salida y calcular faltantes/sobrantes. | ADMIN | `CloseSessionCommandHandler`, recepción. |
| RF-18 | Registrar caja recibida y corregir una recepción sin borrar su historia. | ADMIN | `SessionCashSettlement`, `SessionReceptionCorrection`. |
| RF-19 | Consultar cuenta corriente, precios particulares, historial y envases del cliente. | ADMIN / chofer habilitado | Cuenta del cliente y `CustomerProductPrice`. |
| RF-20 | Calcular actividad blanco/rojo/negro con umbrales configurables. | ADMIN configura; ambos consultan | `CustomerActivityPolicy`, `CustomerActivityController`, Configuración. |
| RF-21 | Solicitar una baja y resolverla conservando deuda, envases e historial. | Chofer solicita; ADMIN resuelve | `CustomerRemovalRequest`, pantalla de bajas. |
| RF-22 | Mostrar camiones en la calle, liquidaciones, resumen diario y rendimiento por camión. | ADMIN | Home, `ReportsController`, `DailySummary`, `vehicle-performance`. |
| RF-23 | Configurar horario laboral y registrar/confirmar excepciones según la operación. | ADMIN configura; ambos operan | `WorkingHoursService`, módulo `workingHours`. |
| RF-24 | Conservar envíos inciertos y reintentarlos con la misma clave sin duplicar efectos. | Chofer / sistema | Outbox local, `clientRequestId`, `expectedSessionId`, índices y transacciones. |

## 6. Reglas de negocio determinantes

- **Carga previa:** sin productos pendientes preparados por ADMIN no se abre la salida. Esa carga se consume una vez al vincularla con el reparto.
- **Asignaciones:** un cliente nuevo requiere camión, zona y al menos un día ISO válido. Varios choferes pueden tener asignado el mismo camión, pero no pueden mantener salidas abiertas simultáneas sobre él.
- **Kilómetros:** se admite igualdad o aumento; el odómetro no debe retroceder. El cierre tampoco puede ser inferior a la apertura.
- **Alcance:** un chofer no elige libremente una salida ni un cliente ajeno para imputar ventas. El servidor obtiene su salida y verifica camión, zona, días y estado del cliente.
- **Orden:** no se calcula una ruta geográfica óptima. `RouteOrder` conserva una secuencia operativa, iniciada por orden de alta y ajustable por ADMIN.
- **Precio:** se usa el precio particular del cliente si existe y, en su defecto, el catálogo. La venta conserva su precio histórico aunque después cambie el catálogo.
- **Envase por saldo:** una unidad vendida de un producto `ByBalance` implica un envase entregado; no debe sumarse dos veces si el navegador también lo informa.
- **Devoluciones:** no se permite recuperar más envases que los registrados más los entregados en esa visita. Los préstamos de productos sin envase tienen su validación propia contra el saldo previo.
- **Stock:** venta y préstamo no pueden consumir, en conjunto, más llenos que los disponibles. Los bloqueos transaccionales coordinan operaciones simultáneas.
- **Dinero:** la venta a deuda sigue siendo venta. Confirmar el cobro completo usa el total definitivo del servidor y no cobra automáticamente deudas anteriores.
- **Sin compra:** el motivo registra que hubo una visita, sin mover stock, dinero ni envases. Actualiza la última visita, pero no la última compra.
- **Actividad:** cuentan semanas en que hubo una salida ya recibida compatible con el cliente y no hubo compra. Una semana sin reparto no penaliza. Una venta reinicia el indicador; un retiro de envases, cobro o promoción no lo reinicia. Los umbrales de rojo y negro se pueden editar desde ADMIN.
- **Promoción:** una prueba nueva no crea inmediatamente un cliente ni un cargo. Debe poder convertirse o cerrarse registrando lo recuperado y lo perdido.
- **Recepción:** la hace ADMIN. Las correcciones posteriores agregan movimientos y motivo en lugar de sobrescribir el historial original.
- **Reintentos:** el mismo envío debe mantener identificador y cuerpo. La salida esperada evita imputar un pendiente de ayer al reparto nuevo.

## 7. Ejemplo verificable de un recorrido corto

Datos ilustrativos, sin conexión con registros reales: carga de **10 bidones de agua y 6 sifones**; precios de **$3.500** y **$1.800**. Todos los saldos monetarios iniciales son cero.

| Cliente ficticio | Envases previos | Entrega / devolución | Cobro de la visita | Resultado |
| --- | --- | --- | --- | --- |
| Ana | 0 bidones | Recibe 3 bidones; no devuelve. | $10.500 en efectivo. | Debe 3 envases y $0. |
| Beto | 3 bidones | Recibe 4 bidones; devuelve 3. | No paga. | Debe 4 envases y $14.000. |
| Carla | 2 sifones | Recibe 1 sifón; devuelve 1. | $1.800 en efectivo. | Conserva 2 envases pendientes y $0. |

Al regresar se esperan **3 bidones llenos, 5 sifones llenos, 3 bidones vacíos y 1 sifón vacío**. Se vendieron $26.300, se cobraron $12.300 y quedaron $14.000 de deuda nueva. Si ADMIN cuenta esas cantidades y recibe $12.300 de efectivo, las diferencias del ejemplo son cero.

Este caso evidencia por qué una sola columna de “deuda” no reemplaza el control de envases ni el de caja.

## 8. Arquitectura encontrada

### Vista general

La solución tiene **un frontend web modular, un backend organizado por capas y una base PostgreSQL**. El backend es un monolito modular: sus funcionalidades se ejecutan dentro de una misma API. No se encontraron microservicios ni un broker de mensajes como parte del circuito de negocio.

ADMIN y chofer usan distintas experiencias dentro de la misma aplicación React. El navegador consulta la API por HTTP/JSON con Bearer JWT. No accede directamente a PostgreSQL. Clientes y prospectos quedan fuera del límite de autenticación del producto.

### Frontend

- React 19, TypeScript, Vite y Tailwind CSS.
- React Router separa rutas de ADMIN y chofer; el panel administrativo se carga de forma diferida para no enviarlo completo al celular.
- TanStack Query coordina consultas, caché e invalidación después de operaciones.
- Módulos por capacidad: `customers`, `deliveries`, `sessions`, `vehicleLoads`, `promotions`, `routePlanning`, `schedule`, `reports`, `workingHours`, `drivers`, `vehicles`, `products`, `zones`, `auth` y `core`.
- `core` contiene componentes y mecanismos técnicos reutilizables, no reglas de ventas o envases. Los contratos públicos del módulo pasan por sus archivos de entrada; dependency-cruiser controla los límites.
- El cliente HTTP centraliza token y errores. El token y la identidad visual de la cuenta se conservan en `localStorage`; el backend decide los permisos efectivos.
- Existe una outbox local para visitas pendientes de confirmación y reenvío. Es tolerancia a cortes de señal, no una operación offline integral: el service worker no cachea la aplicación completa ni garantiza abrirla sin red.

### Backend

| Capa | Responsabilidad | Dependencias entre proyectos |
| --- | --- | --- |
| `GMSoft.API` | Entrada HTTP, autenticación/autorización, controllers, middleware y composición de servicios. | Application y Data. |
| `GMSoft.Application` | Casos de uso, comandos/consultas, validaciones, DTOs y contratos de repositorios/servicios. | Domain. |
| `GMSoft.Domain` | Entidades, enumeraciones y conceptos propios del negocio. | Ninguna otra capa del proyecto. |
| `GMSoft.Data` | EF Core/Npgsql, Identity, repositorios, transacciones y migraciones. | Application y Domain. |

MediatR organiza comandos y consultas dentro del proceso; no implica mensajería distribuida ni bases separadas de lectura y escritura. FluentValidation participa de la validación de casos de uso. Mapster apoya los mapeos. La mayor parte de la coordinación de reglas se encuentra en Application, no concentrada exclusivamente en las entidades de Domain.

En ejecución: controller → caso de uso → interfaces de repositorio → implementación Data → PostgreSQL. La dependencia de compilación es distinta: **Application no referencia Data**; Data implementa los contratos de Application y API conecta las implementaciones mediante inyección de dependencias.

### Persistencia y controles transversales

EF Core mantiene la base relacional, sus relaciones e índices. `IUnitOfWork` permite agrupar escrituras; los casos de uso críticos emplean transacciones y bloqueos de PostgreSQL para coordinar stock, reintentos y cierres. No todas las filas se representan como eventos inmutables: conviven entidades de estado, saldos y libros de movimientos.

Identity administra cuentas y roles; la API emite y valida JWT. Hay errores ProblemDetails, auditoría de entidades y bajas lógicas. Las solicitudes de baja operativa de clientes tienen además su propio flujo de aprobación. El arranque y las migraciones son configurables; existe logging con Serilog y protección de intentos de login.

Los archivos permiten correr la solución en una red local y contienen preparación para Railway/Docker. Eso no demuestra cuál es el alojamiento actual ni que sus respaldos estén activos. El diagrama representa componentes lógicos, no un inventario de infraestructura desplegada.

Las herramientas locales de desarrollo, incluida la integración Claude/Codex de `.tooling`, no forman parte de la arquitectura de ejecución del reparto.

## 9. Modelo conceptual del dominio

| Conjunto | Entidades principales | Relación que expresa |
| --- | --- | --- |
| Organización | `Vehicle`, `Driver`, `Zone`, `Customer` | El chofer tiene vehículo asignado; el cliente pertenece a camión, zona y días. |
| Catálogo | `Product`, `CustomerProductPrice` | El producto define precio y seguimiento; el cliente puede tener precio particular. |
| Reparto | `VehicleLoad`, `DeliverySession`, `SessionRestock`, `SessionStockMovement` | La salida consume carga inicial y registra todos los cambios a bordo. |
| Visitas | `Delivery`, `DeliveryItem` | Cada visita pertenece a una salida y un cliente; no toda visita contiene venta. |
| Envases / préstamos | `CustomerContainerBalance`, `ContainerMovement`, `ContainerUnit` | Cantidades y unidades en poder de clientes, con movimientos de entrega/retorno/ajuste. |
| Dinero | `Payment`, `SessionCashSettlement`, `SessionReceptionCorrection` | Pagos, caja recibida y correcciones asociadas al reparto. |
| Captación | `Promotion`, `PromotionLine`, `PromotionContainerMovement` | La prueba pertenece inicialmente a un prospecto y puede vincularse con un cliente al convertir. |
| Configuración / control | `WorkingHoursSettings`, `CustomerActivitySettings`, `VehicleScheduleDay`, `CustomerRemovalRequest` | Políticas de la empresa, agenda y solicitudes de baja. |

Un cliente puede acumular visitas en distintas salidas. Cada salida conserva su chofer, vehículo, zona y selección de días. `VehicleId` y `VisitDays` admiten nulos para datos heredados; las altas nuevas exigen completarlos. `LastVisitAt` y la última compra no significan lo mismo.

## 10. Requisitos de calidad y evidencia

| Requisito | Mecanismo observado | Alcance de la evidencia |
| --- | --- | --- |
| Consistencia de ventas, cobros y envases | Transacciones y unidad de trabajo. | Código de casos de uso y repositorios; no nueva ejecución contra BD. |
| Evitar duplicados y sobreventa concurrente | Claves de reintento, índices únicos y bloqueos. | Implementaciones y pruebas específicas existentes. |
| Facilidad de uso en reparto | Interfaz móvil, cantidades con +/−, totales y confirmaciones. | Componentes y vistas; no estudio de usabilidad con choferes. |
| Separación de responsabilidades | Capas de backend y módulos de frontend. | Referencias de proyectos, guardas arquitectónicas y reglas dependency-cruiser. |
| Trazabilidad | Auditoría, movimientos, históricos y correcciones explícitas. | Modelo y consultas; no garantía de auditoría externa inviolable. |
| Manejo de conectividad imperfecta | Outbox local y reintentos de visitas. | Guardado antes de enviar y reenvío por usuario; no modo offline completo. |
| Rendimiento razonable | Paginación, carga diferida, consultas específicas y caché. | Diseño observado; no mediciones de carga o SLA. |
| Permisos | JWT/Identity, roles y comprobación de recorrido en servidor. | Lectura de código; no pentest. El uso de localStorage mantiene exposición ante XSS. |
| Calidad de cambios | CI con build, pruebas y validaciones de arquitectura/modelo. | Workflows y suites existentes; no se consultó el resultado actual de GitHub Actions. |
| Tiempo de negocio coherente | UTC en registros y conversión a fecha/hora argentina. | `BusinessTime` y contratos; filtros de reportes pueden usar apertura o cierre según el caso. |

## 11. Límites y decisiones que siguen abiertas

1. **Depósito global:** hay carga y movimientos por camión, pero no se identificó un inventario integral del depósito con entradas, producción, compras y disponibilidad general. No debe presentarse como un ERP completo de inventario.
2. **Cobros fuera de la venta:** la API contempla importes explícitos y pagos en el modelo; el flujo móvil habitual confirma el cobro de la venta actual. Hace falta un circuito de usuario claramente definido para deuda anterior o pagos parciales independientes.
3. **Offline:** la bandeja local mejora los reintentos, pero no reemplaza la descarga de datos y aplicación para operar completamente sin conexión. Un envío pendiente todavía no es una venta confirmada por el servidor.
4. **Reconstrucción histórica:** algunos indicadores y listados usan la asignación actual del cliente. Cambiar camión, zona o días puede alterar interpretaciones del pasado; conviene definir qué reportes necesitan una fotografía histórica de la pertenencia al recorrido.
5. **Exposición a otros negocios:** no se observó un modelo multiempresa. El horario y varios ajustes son globales. Convertirlo en un servicio para múltiples distribuidoras requiere una decisión de aislamiento de datos y configuración.
6. **Alcance comercial y geográfico:** no se identificaron facturación fiscal, portal de clientes, optimización automática de recorridos ni seguimiento GPS en tiempo real. El enlace al mapa ayuda a abrir una dirección; no equivale a seguimiento de flota.
7. **Documentación desactualizada:** README y `customer-activity.md` todavía conservan frases que dicen que no existe un endpoint para editar umbrales. El controller, sus casos de uso y Configuración demuestran que sí existe. En este informe prevalece ese código.

La evolución puede seguir dentro del monolito modular: mantener contratos claros, reducir duplicación de reglas, definir el inventario de depósito y reforzar los puntos anteriores. El tamaño de la solución por sí solo no justifica dividirla en microservicios.

## 12. Cómo evaluar el resultado del sistema

Las siguientes son métricas propuestas; no resultados medidos:

- Diferencias de llenos, vacíos y efectivo por salida recibida.
- Porcentaje de visitas completadas respecto de clientes programados.
- Dinero vendido, cobrado y pendiente, sin confundir transferencia con efectivo recibido.
- Envases pendientes de recuperación y préstamos de promociones vencidas.
- Conversión de prospectos a clientes.
- Cantidad y antigüedad de envíos pendientes de confirmación.
- Tiempo y errores del chofer al registrar una visita.

Un buen resultado sería poder explicar cada diferencia desde sus movimientos y reducir operaciones que dependen de recordar lo ocurrido.

## 13. Diagramas y edición

- [Usuarios y arquitectura — PNG](usuarios-arquitectura.png) · [SVG](usuarios-arquitectura.svg).
- [Ciclo del reparto y controles — PNG](ciclo-reparto.png) · [SVG](ciclo-reparto.svg).
- [Archivo editable de Excalidraw: dos láminas](gmsoft-arquitectura.excalidraw).
- [Generador de los diagramas](generar_diagramas.py): Python + Pillow, sin dependencias del frontend o backend. Genera las escenas editables y las vistas estáticas a partir de las mismas coordenadas.

Los PNG/SVG son vistas de estilo boceto; el `.excalidraw` contiene figuras, textos y flechas editables. La arquitectura es lógica. Las flechas sólidas representan uso/llamadas y las discontinuas una interacción presencial; no representan todas las referencias de compilación.

## 14. Fuentes del análisis

Lectura del código y documentos locales; no se accedió a datos de clientes ni a la BD de Linux, no se arrancaron los proyectos y no se modificó código de negocio.

| Fuente | Qué respalda |
| --- | --- |
| [Backend README](../../README.md), archivos `.csproj` y [ArchitectureTests](../../GMSoft.Application.Tests/ArchitectureTests.cs) | Capas, tecnologías y regla de dependencias. |
| [Customer](../../GMSoft.Domain/Entities/Customer.cs), [Driver](../../GMSoft.Domain/Entities/Driver.cs), [DeliverySession](../../GMSoft.Domain/Entities/DeliverySession.cs), [Product](../../GMSoft.Domain/Entities/Product.cs) | Relaciones y conceptos del dominio. |
| [Apertura](../../GMSoft.Application/Features/Sessions/Open/OpenSessionCommandHandler.cs), [registro de visita](../../GMSoft.Application/Features/Deliveries/Register/RegisterDeliveryCommandHandler.cs), [recepción](../../GMSoft.Application/Features/Sessions/Close/CloseSessionCommandHandler.cs) | Reglas críticas y transacciones. |
| [CustomersController](../../GMSoft.API/Controllers/CustomersController.cs), [SessionsController](../../GMSoft.API/Controllers/SessionsController.cs), [CustomerActivityController](../../GMSoft.API/Controllers/CustomerActivityController.cs) | Permisos y funcionalidades expuestas. |
| [Política de actividad](../../GMSoft.Application/Features/Customers/Common/CustomerActivityPolicy.cs), [umbrales](../../GMSoft.Application/Features/Customers/ActivitySettings/CustomerActivitySettings.cs) | Semanas perdidas y configuración vigente. |
| [Ventas](../delivery-rules.md), [cuenta](../customer-account.md), [promociones](../promotions.md), [visitas sin compra](../no-purchase.md), [bajas](../customer-removal.md) | Circuitos y contratos complementarios. |
| [Recorridos](../route-planning.md), [agenda](../vehicle-schedule.md), [recargas](../restocks.md), [resumen](../daily-summary.md), [rendimiento](../vehicle-performance.md), [horarios](../working-hours.md) | Planificación, control y reportes. |
| [Router frontend](../../../GM-SoftFE/src/app/router.tsx), [rutas ADMIN](../../../GM-SoftFE/src/app/admin/AdminRoutes.tsx), [configuración](../../../GM-SoftFE/src/app/admin/ConfigurationView.tsx) | Experiencias y pantallas disponibles. |
| [Layout del chofer](../../../GM-SoftFE/src/modules/sessions/views/DriverSessionLayout.tsx), [cliente HTTP](../../../GM-SoftFE/src/modules/core/lib/api.ts), [outbox](../../../GM-SoftFE/src/modules/core/lib/outbox.ts), [service worker](../../../GM-SoftFE/public/sw.js) | Autenticación, recepción y límites de conectividad. |
| Workflows [backend](../../.github/workflows/ci.yml) y [frontend](../../../GM-SoftFE/.github/workflows/ci.yml) | Verificaciones automatizadas previstas. |

Para el formato editable se consultaron el [formato de exportación de Excalidraw](https://github.com/excalidraw/excalidraw/blob/master/packages/excalidraw/data/json.ts) y sus [utilidades oficiales de exportación](https://docs.excalidraw.com/docs/@excalidraw/excalidraw/api/utils/export). No se subió el proyecto a un servicio externo para generar los diagramas.
