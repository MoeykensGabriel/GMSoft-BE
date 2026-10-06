# Visitas pospuestas

El chofer abre la ficha del cliente y puede consultar sus datos, cuenta y envases.
La edición de datos continúa bajo control de ADMIN.

POST /api/sessions/current/customers/{customerId}/postpone marca al cliente pendiente
en la salida abierta del chofer autenticado. No recibe un id de sesión externo.
Se rechaza si no hay salida abierta o si el cliente no está activo en el camión,
zona y días de la salida.
Repetir la acción no duplica la marca. No modifica días habituales, orden, dinero,
stock ni movimientos de envases. La marca persiste en DeliverySession y se devuelve
como deferredCustomerIds en los DTO de sesión, también para ADMIN.

Al registrar una entrega válida se quita la marca de ese cliente dentro de la
misma transacción; las demás marcas permanecen. Una entrega rechazada conserva
la marca. La salida siguiente comienza sin marcas. Posponer no cambia la posición
del cliente ni genera una visita con fecha futura. Tampoco actualiza LastVisitAt;
la definición de si posponer cuenta como visita queda pendiente.

La migración agrega una columna nullable uuid[] sin modificar registros anteriores.
Actualizar primero BE y después FE. El detalle de deuda muestra los últimos 50
movimientos de cuenta; el saldo se calcula con todo el historial.
