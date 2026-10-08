# Ventas y envases

## Promociones de prueba

Las nuevas pruebas tienen un circuito propio en POST /api/promotions: guardan
un prospecto sin crear cliente, fecha de retiro y cierre con o sin conversion.
Los contratos, reglas, reintentos y reporte estan en [promotions.md](promotions.md).

El tipo historico `Promotion` de POST /api/deliveries conserva su comportamiento
y sus tests: importe cero, sin cobro, contra cliente existente o newCustomer.
No se elimina ni migra ese historial. El frontend usara el circuito nuevo en
su proxima tanda.

## Reglas comunes

Para una venta, los productos con seguimiento `ByBalance` generan automaticamente
tantos envases entregados como unidades vendidas. El cliente de la API puede enviar
`containersOut: []`. Si envia cantidades explicitas, deben coincidir con la venta;
no se suman dos veces. Los productos `None` no generan deuda de envases y los
productos `ByUnit` conservan su circuito separado de asignacion por numero de serie.

Saldo nuevo = saldo anterior + envases entregados - envases recuperados.
Ejemplo: tenia 3, recibe 4 llenos y devuelve 3 vacios: queda con 4 pendientes.
Una devolucion que supera el saldo anterior mas lo entregado en esta visita se
rechaza. Se permite una visita sin venta para recuperar envases.

Antes de registrar la venta se consulta el stock de llenos del recorrido y se
rechazan cantidades superiores a las disponibles. Una visita `ContainerOnly`
no admite lineas de venta. Las reglas se prueban a nivel de aplicacion; aun falta
verificacion de integracion con PostgreSQL y de solicitudes concurrentes. Estas
consultas de saldo no sustituyen un mecanismo de bloqueo o control de concurrencia.

Pendiente de definicion: stock global de deposito versus carga por camion, y quien
registra los cobros independientes de una venta. Cantidades explicitas de envases
que contradigan la venta reciben un error de la API.

## Cobro de la venta

`payment: { "method": "Cash" }` sin `amount` significa "cobro la venta completa":
el importe es el total que calcula el servidor con los precios del cliente, no un
valor del telefono. Solo aplica a `Sale`; en otra visita se rechaza. `payment: null`
deja el total como deuda. No cobra deuda anterior.

La pantalla del chofer muestra el total y pregunta "Si, cobre / No, queda a deuda"
y la forma de pago; ya no pide escribir el monto. La API sigue aceptando `amount`
explicito (mayor a cero) para cobros parciales o de deuda anterior, que todavia no
tienen pantalla.

## Reintentos

`clientRequestId` (uuid, opcional) identifica el envio. En POST /api/deliveries,
repetirlo devuelve la visita ya registrada sin volver a vender, mover envases ni
cobrar; un indice unico lo garantiza aunque lleguen dos a la vez. En
POST /api/vehicles/{id}/load, repetirlo devuelve la hora de la carga original sin
sumar de nuevo. Sin el campo, el comportamiento es el de antes. Migracion
`ClientRequestIds`: dos columnas nullable, no modifica registros anteriores.
