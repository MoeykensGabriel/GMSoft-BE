# Asignación de clientes y días de visita

Cada cliente nuevo requiere camión, zona y `visitDays` (enteros ISO lunes=1 a
 domingo=7). Se rechazan listas vacías, repetidos y valores fuera de 1–7.
Los formularios de alta preseleccionan el día actual de Argentina y permiten
cambiarlo o elegir varios días; editar conserva los días guardados.

## ADMIN

POST/PUT /api/customers exige `vehicleId` existente y `visitDays`. El formulario
permite elegir camión y zona. Crear en oficina no registra una visita.
Cambiar camión o zona coloca al cliente al final del recorrido de destino,
incluso si el request incluía una posición anterior. Cambiar solo los días
conserva RouteOrder. La numeración se mantiene por zona, así se preserva el
orden relativo histórico al filtrar cada camión (pueden quedar saltos).

El DTO agrega vehicleId, vehicleName, vehicleLicensePlate y lastVisitAt.
GET /api/customers para ADMIN conserva acceso a todos los clientes y admite
filtros opcionales por vehicleId, zoneId y visitDays/todayOnly.

## Chofer

El alta en la puerta sigue formando parte de POST /api/deliveries. NewCustomer
requiere visitDays; camión y zona se toman de la salida abierta autenticada,
nunca del navegador ni de la asignación posterior del chofer. Los días elegidos
pueden ser distintos de los de la salida: la primera venta/promoción se registra
y esa frecuencia se aplica a los siguientes recorridos.

La hoja de ruta siempre usa camión + zona + días de la salida. El servidor impone
esos filtros al chofer aunque modifique parámetros. Se exige cliente activo y
coincidencia con al menos un día. Se filtra antes de contar/paginar y se ordena
por RouteOrder, Id, sin duplicar clientes que tengan varios días.
Ficha, cuenta, precios, venta y posposición validan también el alcance del
recorrido. El chofer no puede editar clientes existentes.

## Varios días en una salida

ADMIN selecciona routeDays al cargar el vehículo; hoy queda preseleccionado.
Puede elegir varios días para recuperar un reparto. Esto no modifica los días
habituales del cliente. POST /api/vehicles/{id}/load guarda la selección en todas
las líneas pendientes. Omitirla conserva la existente o usa hoy si no hay ninguna.
PUT /api/vehicles/{id}/load/route-days permite corregirla sin sumar stock; requiere
ADMIN, carga pendiente y vehículo sin salida abierta.

Al abrir la salida se copia routeDays a DeliverySession. Permanece fijo al pasar
medianoche. Las sesiones antiguas sin selección usan el día argentino de apertura.

## Última visita y migración

Una venta, promoción o movimiento de envases confirmado actualiza LastVisitAt
con la hora UTC del servidor dentro de la misma transacción. La ficha muestra
fecha y hora argentinas. Una operación rechazada, una consulta o posponer no
actualizan la fecha. Última compra y los colores de inactividad siguen calculados
solo a partir de ventas; las promociones no los reinician.

CustomerVehicleAndLastVisit agrega columnas nullable y relación con Vehicles
con borrado restringido. Recupera LastVisitAt desde MAX(Deliveries.DeliveredAt).
No infiere camiones: los clientes anteriores quedan sin asignar y ADMIN debe
completar camión y días antes de incluirlos en el reparto. No modifica saldos,
ventas, envases ni orden existente. Un camión con clientes no puede eliminarse.

Despliegue: actualizar y reiniciar BE para aplicar la migración automática;
después actualizar FE. Ambos deben usar el nuevo contrato. No recrear la base.
