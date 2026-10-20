# Glosario del negocio y del código

Para compartir vocabulario entre oficina, reparto y desarrollo. [Índice](README.md). Los nombres entre comillas de código son identificadores, no opciones que deba escribir el usuario.

## Personas y organización

| Término de uso | Nombre en el código | Qué significa |
| --- | --- | --- |
| Oficina / ADMIN | Rol `Admin` | Administra el negocio, recibe camiones y revisa sus controles. |
| Chofer / repartidor | `Driver`, rol `Driver` | Perfil de quien reparte. Tiene una cuenta de acceso y puede tener vehículo asignado. |
| Usuario | `ApplicationUser` | Identidad de acceso. No es lo mismo que un cliente ni que una salida. |
| Cliente | `Customer` | Persona o comercio con cuenta, asignación de vehículo, zona y días. |
| Prospecto | Datos de prospecto en `Promotion` | Persona que recibe una prueba antes de convertirse en cliente. No hay una entidad `Prospect` separada ni se crea un `Customer` al registrar la prueba. |
| Camión / vehículo | `Vehicle` | Unidad que transporta mercadería. Su asignación al chofer está en `Driver.VehicleId`; la del cliente, en `Customer.VehicleId`. |
| Zona de reparto | `Zone` | Agrupación de clientes que se elige al abrir una salida. |
| Agenda del camión | `VehicleScheduleDay` | Zona prevista para cada día de la semana. Es orientativa. |
| Días habituales del cliente | `Customer.VisitDays` | Días ISO de visita: lunes 1 a domingo 7. |
| Activo | `Customer.IsActive` / `Driver.IsActive` | Condición de actividad operativa. No es el color de compras del cliente. |

## Reparto y visitas

| Término de uso | Nombre en el código | Qué significa |
| --- | --- | --- |
| Carga inicial preparada | `VehicleLoad` | Llenos registrados antes de salir. Quedan pendientes hasta que una salida los consume. |
| Carga pendiente | `VehicleLoad.ConsumedBySessionId == null` | Carga no eliminada que todavía puede usarse al abrir una salida. |
| Días de la salida | `VehicleLoad.RouteDays` → `DeliverySession.RouteDays` | Selección de días que cubrirá ese reparto. Se copia al abrir y no cambia a medianoche. |
| Salida / sesión de reparto | `DeliverySession` | Recorrido operativo con chofer, camión, zona, días, carga y kilómetros. No es la sesión de autenticación. |
| En la calle | `SessionStatus.Open` | Salida abierta, todavía pendiente de recepción o anulación válida. |
| Recarga | `SessionRestock`, movimiento `Restock` | Tanda adicional de llenos en una salida abierta. No es otra salida. |
| Hoja / recorrido de clientes | Filtro de camión + zona + días de `DeliverySession` | Clientes habilitados para atender en esa salida. |
| Orden del recorrido | `Customer.RouteOrder` | Posición del cliente en su zona. Un solo orden, no uno distinto por día ni una ruta GPS optimizada. |
| Organizar recorrido | `SaveDriverRouteOrderCommand` / `SaveRoutePlanningCommand` | Guardado del orden desde chofer u oficina. La oficina también cambia asignaciones. |
| Registro de cambio de orden | `RouteOrderChange` | Quién reordenó, cuándo y cuántos clientes cambiaron de posición; puede vincularse a una salida. |
| Visitar más tarde | `routeLaterStorage` | Preferencia local por salida. No crea una visita ni modifica `RouteOrder`. |
| Visita | `Delivery` | Registro de atención. Puede ser venta, movimiento de envases/préstamos o visita sin compra. |
| Venta | `DeliveryType.Sale`, `DeliveryItem` | Productos vendidos con sus cantidades y precios guardados. Puede quedar cobrada o a deuda. |
| Solo envases / préstamos | `DeliveryType.ContainerOnly` | Visita sin venta para retirar envases o prestar/retirar bienes habilitados. |
| No compró | `DeliveryType.NoPurchase`, `NoPurchaseReason` | Visita con motivo y sin movimiento de dinero, stock ni envases. |
| Sin visitar | `GetUnvisitedCustomersQuery` | Clientes de la salida que no tienen una visita registrada en ella. No equivale a “No compró”. |
| Última visita | `Customer.LastVisitAt` | Momento de una visita registrada. No es necesariamente una compra. |
| Última compra | `lastPurchaseAt` | Fecha obtenida de ventas `Sale`; no es una columna equivalente a `LastVisitAt`. |
| Blanco / rojo / negro | `White`, `Red`, `Black`; `CustomerActivityPolicy` | Actividad calculada por semanas sin compra con reparto recibido compatible. Se configura con `CustomerActivitySettings`. |
| Recepción / cierre del camión | `CloseSessionCommand`, `SessionStatus.Closed` | Conteo de lo que volvió y cierre de la salida; puede incluir la rendición de efectivo. No hay una entidad independiente llamada `Reception`. |
| Corrección de recepción | `SessionReceptionCorrection` y sus líneas | Antecedente de cambios posteriores en cantidades, kilómetros o efectivo, con motivo. No reabre la salida. |
| Anulación de salida | `CancelSessionCommand`, `SessionStatus.Cancelled` | Solo sin actividad. Libera la carga y oculta la salida mediante baja lógica; conserva autor, fecha y motivo. |
| Baja pedida del cliente | `CustomerRemovalRequest` | Solicitud del chofer: `Pending`, `Confirmed` o `Rejected`. No borra deuda ni envases. |

## Productos, envases y préstamos

| Término de uso | Nombre en el código | Qué significa |
| --- | --- | --- |
| Producto | `Product` | Catálogo, precio y modo de seguimiento. |
| Publicado | `Product.IsPublished` | Habilitado para los circuitos de carga y venta. No es una cantidad de stock. |
| Lleno / vacío | `ContainerState.Full` / `Empty` | Estado del stock del camión. Un dispenser común retirado de préstamo entra como vacío. |
| Stock a bordo | Saldo de `SessionStockMovement` | Entradas y salidas por producto y estado dentro de una salida. |
| Envases por saldo / por cantidad | `ContainerTracking.ByBalance`, `CustomerContainerBalance` | Cantidad pendiente por cliente y producto, sin identificar cada bidón o sifón. |
| Envases por unidad / serie | `ContainerTracking.ByUnit`, `ContainerUnit` | Unidades identificadas por número de serie; distinto del préstamo común. |
| No lleva envase | `ContainerTracking.None` | La venta no genera devolución de envases. Puede habilitarse aparte un préstamo sin cargo. |
| Envases entregados / devueltos | `containersOut` / `containersIn` | Cantidades de envases de una visita. Para productos por saldo, la venta completa automáticamente los entregados. |
| Se puede prestar | `Product.CanBeLent` | Habilita préstamo sin cargo de un producto con seguimiento `None`, manteniendo la posibilidad de venderlo. |
| Prestar / retirar dispenser común | `loansOut` / `loansIn` | Operaciones sin cargo. Usan `CustomerContainerBalance` y movimientos `LoanedToCustomer` / `LoanReturned`. En la cuenta aparecen como `Loan` / `LoanReturn`. |
| Libro de envases | `ContainerMovement` | Historial de entregas, devoluciones, ajustes, pérdidas y préstamos. |
| Ajuste / pérdida | `ContainerMovementType.Adjustment` / `Lost` | Corrige el saldo real o registra lo perdido. No equivale a una devolución ni genera un pago. |
| Faltante / sobrante de productos | Saldo después de `ReturnedAtClose` | Diferencia entre movimientos y descarga. Positivo: falta. Negativo: sobra. Mientras está abierta es stock, no faltante final. |

## Dinero, promociones y soporte

| Término de uso | Nombre en el código | Qué significa |
| --- | --- | --- |
| Cobro | `Payment` | Pago del cliente: `Cash`, `Transfer` o `Card`. No es la plata recibida por oficina. |
| A deuda / a cuenta | Venta sin pago completo; saldo monetario | El cargo queda en la cuenta. No significa que falten envases o efectivo cobrado. |
| Cuenta corriente / saldo | `CustomerAccountDto`, `AccountStatement`; `balance` | Cargos menos pagos acumulados. `statement` muestra el historial; `movements` es un campo legado y no debe sumarse otra vez. |
| Precio particular | `CustomerProductPrice` | Precio de un producto para un cliente, usado antes del precio de catálogo. |
| Rendición de efectivo | `SessionCashSettlement` | Dinero que oficina recibió de una salida. Puede registrarse al recibir o después. |
| Liquidación | `GetSessionDetailedSettlementQuery` y `SessionSettlementDto` | Consultas de ventas, clientes, cobros y rendición. No es sinónimo de ganancia. |
| Diferencia de caja | `CashSettlementCalculation.Difference` | Efectivo cobrado menos recibido. No compara transferencias ni tarjetas; sin rendición es pendiente. |
| Resumen diario | `GetDailySummaryQuery` | Conciliación por camión y día argentino de apertura, con varias salidas si corresponde. |
| Promoción / prueba gratis | `Promotion`, `PromotionLine` | Prueba a un prospecto, sin cobro ni deuda. El tipo legado `DeliveryType.Promotion` queda para historial; no se usa para nuevas pruebas. |
| Fecha de retiro | `Promotion.PickupDate` | Fecha fijada al registrar según el plazo general. No cambia al editar después ese plazo. |
| Pendiente / vencida | `PromotionStatus.Pending` / presentación `Overdue` | Vencida se calcula por fecha; no es otro estado persistido. |
| Convertida / no convertida | `Converted` / `NotConverted` | Cierre de la prueba creando cliente y traspasando envases, o registrando recuperados y pérdidas. |
| Fuera de horario | Marcas `IsOutsideWorkingHours` y equivalentes | Resultado registrado contra `WorkingHoursSettings`. No implica que todas las operaciones estén bloqueadas. |
| Visita guardada en el celular | Elemento `pending` de `outbox` | Envío cuya confirmación falta. No es todavía una venta confirmada en el servidor. |
| Envío rechazado | Elemento `failed` de `outbox` | La API devolvió un rechazo definitivo. Requiere revisar el caso con oficina. |
| Clave de reintento | `clientRequestId` | Identifica la misma operación lógica. Se conserva junto con el cuerpo al reintentar. |
| Salida esperada | `expectedSessionId` | Evita registrar una visita pendiente en una salida nueva. |
| Sesión de acceso | JWT y estado de `AuthProvider` | Autenticación del navegador. Vencerla o cerrarla no recibe el camión. |

## Referencias

Los identificadores se contrastaron con las [entidades](../GMSoft.Domain/Entities), [enumeraciones](../GMSoft.Domain/Enums), [casos de uso](../GMSoft.Application/Features), [cuenta](../GMSoft.Application/Features/Customers/Account/AccountStatement.cs) y [estado local del frontend](../../GM-SoftFE/docs/arquitectura.md#guardado-local-alcance-de-cada-dato).

No forman parte del funcionamiento vigente las ganancias mensuales de la tarea 20 ni la corrección de mezcla de envases de la tarea 21. Para reglas y contratos ampliados, seguí los enlaces del [índice](README.md).
