# Estado de actividad de clientes

La API devuelve `activityStatus` (`White`, `Red`, `Black`) en el listado y detalle
de clientes. Es un estado calculado, independiente de `isActive`: no bloquea ventas
ni elimina clientes de la zona.

Configurar `CustomerActivity:RedAfterDays` y `CustomerActivity:BlackAfterDays` en
`GMSoft.API/appsettings.json` o con las variables `CustomerActivity__RedAfterDays`
y `CustomerActivity__BlackAfterDays`. Reiniciar la API para aplicar los cambios.
Los valores iniciales de 15 y 30 dias son provisionales y deben ajustarse al negocio.
Se exige `0 < RedAfterDays < BlackAfterDays`; una configuracion invalida impide el inicio.

El estado cambia al alcanzar el plazo (inclusive). Se cuentan dias calendario
argentinos desde la ultima venta. Cobros y retiros de envases no reinician el plazo.
Si nunca compro, se cuenta desde el alta; `lastPurchaseAt` y `daysWithoutPurchase`
siguen siendo nulos para no inventar una compra. Una nueva compra devuelve el estado
a blanco. No se requiere una tarea programada ni una migracion de base de datos.

Esta configuracion es del servidor: todavia no existe un endpoint para editarla
desde un panel administrativo.

## Inicio del reparto existente

El chofer inicia sesion con usuario y clave y abre el reparto con zona y kilometraje.
Antes, el administrador selecciona el camion y registra su carga de productos llenos.
Sin una carga pendiente, la API rechaza el inicio del recorrido. La carga se consume
al abrir la salida y no se reutiliza en la siguiente.
El vehiculo proviene de su asignacion. Se admite kilometraje igual o mayor al ultimo
registrado. La consulta de clientes filtrada por zona utiliza `RouteOrder`, asignado
al final del recorrido al dar de alta cada cliente. La venta se imputa a la sesion
abierta del chofer autenticado.

Al regresar, el administrador recibe el camion registrando por producto los vacios
recuperados y los llenos no vendidos. La recepcion cierra la salida y muestra las
diferencias entre el stock esperado y lo contado. Una nueva salida requiere otra carga.
