# Estado de actividad de clientes

La API devuelve `activityStatus` (`White`, `Red`, `Black`) en el listado y detalle
de clientes. Es un estado calculado, independiente de `isActive`: no bloquea ventas
ni elimina clientes de la zona.

El estado no se mide en dias de calendario sino en **turnos perdidos**: semanas en
que el camion salio a visitar al cliente y volvio sin venderle. `weeksWithoutPurchase`
devuelve ese numero en el listado, el detalle y la cuenta del cliente.

- Le "toco" cuando una salida **ya recibida** fue con su camion, su zona y alguno de
  sus dias de visita. Una semana sin reparto (feriado, camion roto) no suma. Una
  salida que sigue en la calle tampoco: el chofer todavia puede venderle.
- Se cuenta **por semana** (lunes a domingo, hora argentina), no por visita. Un
  cliente de lunes y jueves que no compro el lunes pero si el jueves no perdio nada,
  y dos salidas en la misma semana son un solo turno.
- Cuentan las semanas posteriores a la de su ultima venta. Si nunca compro, desde
  el alta; `lastPurchaseAt` sigue nulo para no inventar una compra.
- Solo una venta corta la cuenta. Promociones, cobros, retiros de envases y visitas
  pospuestas no la reinician.
- Se evalua con la asignacion actual del cliente (camion, zona y dias). Un cliente
  sin camion o sin dias no entra en ningun recorrido y queda en cero.

Configurar `CustomerActivity:RedAfterMissedWeeks` y `CustomerActivity:BlackAfterMissedWeeks`
en `GMSoft.API/appsettings.json` o con las variables `CustomerActivity__RedAfterMissedWeeks`
y `CustomerActivity__BlackAfterMissedWeeks`. Reiniciar la API para aplicar los cambios.
Los valores iniciales de 2 y 4 son provisionales. Se exige `0 < rojo < negro`; una
configuracion invalida impide el inicio. Reemplazan a `RedAfterDays` y `BlackAfterDays`,
que ya no se leen.

El estado cambia al alcanzar el umbral (inclusive) y vuelve a blanco con una nueva
venta. `daysWithoutPurchase` se sigue informando como dato, pero ya no decide el
color. No requiere tarea programada ni migracion. El reporte de clientes inactivos y
el filtro `inactiveSinceDays` siguen midiendo dias corridos.

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
