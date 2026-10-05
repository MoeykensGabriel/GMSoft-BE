# Días de visita

ADMIN crea y edita clientes con `visitDays`, una lista obligatoria de enteros ISO:
lunes 1, martes 2, miércoles 3, jueves 4, viernes 5, sábado 6, domingo 7.
Se rechazan listas vacías, repetidos y valores fuera de 1–7.

La migración CustomerVisitDays agrega una columna nullable integer[] sin cambiar
los datos anteriores. Null significa pendiente de configurar por ADMIN. Las altas
del chofer también quedan pendientes: el chofer no configura la frecuencia.

GET /api/customers?zoneId=…&onlyActive=true&todayOnly=true devuelve los clientes
programados para el día argentino y los pendientes, manteniendo RouteOrder.
El filtro se aplica antes del conteo y la paginación. Sin filtros de días se conserva
el listado completo del administrador. El filtro organiza la agenda; no prohíbe
una venta excepcional a un cliente fuera de su día.

Actualizar días en la misma zona conserva el orden; cambiar de zona sin indicar
RouteOrder coloca al cliente al final de la nueva zona. Ventas, pagos y envases
no se modifican. Los consumidores de POST/PUT de clientes deben enviar visitDays.

Despliegue: actualizar y reiniciar primero BE para aplicar la migración automática;
después actualizar FE. No requiere recrear la base de datos.

## Varios días en una salida

ADMIN selecciona `routeDays` al cargar el vehículo, con el día argentino actual
preseleccionado en el formulario. Puede elegir varios días (por ejemplo lunes y
martes para recuperar un reparto). La frecuencia habitual de los clientes no cambia.

POST /api/vehicles/{id}/load admite `routeDays` además de items. La selección se
guarda en todas las líneas pendientes de ese vehículo. Omitirla desde un cliente
antiguo conserva la selección existente; si no hay ninguna, toma el día actual.
PUT /api/vehicles/{id}/load/route-days permite corregirla sin volver a cargar stock.
Requiere ADMIN, una carga pendiente y que el vehículo no tenga una salida abierta.

Al abrir la salida se copia la selección en DeliverySession.RouteDays. Permanece
fija aunque pase la medianoche o se inicie en un día distinto. Las cargas anteriores
sin selección toman el día de apertura; las sesiones anteriores se muestran con su
día de apertura. La migración DepartureRouteDays solo agrega columnas nullable.

El chofer consulta GET /api/customers?zoneId=…&onlyActive=true&visitDays=1&visitDays=2
con los días de su sesión. Se incluye un cliente si coincide con cualquiera de esos
días, una sola vez y en RouteOrder, más los pendientes de configurar. `visitDays`
tiene prioridad sobre el filtro anterior `todayOnly`. La búsqueda aplica a toda
la consulta antes de paginar, no solo a los clientes que ya se ven en pantalla.
