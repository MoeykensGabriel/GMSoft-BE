# Días de visita

ADMIN crea y edita clientes con `visitDays`, una lista obligatoria de enteros ISO:
lunes 1, martes 2, miércoles 3, jueves 4, viernes 5, sábado 6, domingo 7.
Se rechazan listas vacías, repetidos y valores fuera de 1–7.

La migración CustomerVisitDays agrega una columna nullable integer[] sin cambiar
los datos anteriores. Null significa pendiente de configurar por ADMIN. Las altas
del chofer también quedan pendientes: el chofer no configura la frecuencia.

GET /api/customers?zoneId=…&onlyActive=true&todayOnly=true devuelve los clientes
programados para el día argentino y los pendientes, manteniendo RouteOrder.
El filtro se aplica antes del conteo y la paginación. Sin todayOnly se conserva
el listado completo del administrador. El filtro organiza la agenda; no prohíbe
una venta excepcional a un cliente fuera de su día.

Actualizar días en la misma zona conserva el orden; cambiar de zona sin indicar
RouteOrder coloca al cliente al final de la nueva zona. Ventas, pagos y envases
no se modifican. Los consumidores de POST/PUT de clientes deben enviar visitDays.

Despliegue: actualizar y reiniciar primero BE para aplicar la migración automática;
después actualizar FE. No requiere recrear la base de datos.
