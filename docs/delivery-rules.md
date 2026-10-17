# Ventas y envases

## Promociones de prueba

Las nuevas pruebas tienen un circuito propio en POST /api/promotions: guardan
un prospecto sin crear cliente, fecha de retiro y cierre con o sin conversion.
Los contratos, reglas, reintentos y reporte estan en [promotions.md](promotions.md).

POST /api/deliveries rechaza `type: "Promotion"` con 400 e indica usar
POST /api/promotions. El valor `Promotion` se conserva solo para leer historial:
recorrido, liquidacion, resumen diario, reportes y cuenta del cliente.
No se elimina ni modifica ese historial.

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
no admite lineas de venta. La venta bloquea la fila de la salida durante su
transaccion: dos visitas simultaneas de la misma salida se resuelven de a una, asi
que juntas no pueden vender mas de lo que hay a bordo. Las reglas se prueban a nivel
de aplicacion y contra PostgreSQL real, concurrencia incluida (docs/testing.md).

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

Las recargas durante el reparto usan POST /api/sessions/{id}/restocks, con varios
productos y clientRequestId obligatorio. Reemplaza al antiguo POST de un producto
/api/sessions/{id}/stock. Contrato y consulta para ADMIN/chofer: [restocks.md](restocks.md).

`clientRequestId` (uuid, opcional) identifica el envio. En POST /api/deliveries,
repetirlo devuelve la visita ya registrada sin volver a vender, mover envases ni
cobrar; un indice unico lo garantiza aunque lleguen dos a la vez. En
POST /api/vehicles/{id}/load, repetirlo devuelve la hora de la carga original sin
sumar de nuevo. Sin el campo, el comportamiento es el de antes. Migracion
`ClientRequestIds`: dos columnas nullable, no modifica registros anteriores.

## Visitas sin compra

Contrato de `NoPurchase`, motivos, reintentos y clientes sin visitar: [no-purchase.md](no-purchase.md).

## Préstamos sin cargo

`Product.CanBeLent` vale `false` por defecto. Alta y edición solo permiten activarlo
con `ContainerTracking.None`. La venta conserva su precio y no genera saldo de préstamo.

La visita acepta `loansOut` y `loansIn`: listas opcionales, vacías por defecto, con
`{ productId, quantity }`. Solo admiten productos prestables sin envase, cantidades
de 1 a 1000 y un renglón por producto en cada lista. No se admiten en «No compró».
Una visita `ContainerOnly` puede consistir únicamente en préstamos o retiros.

Lo vendido más lo prestado del mismo producto no puede superar los llenos a bordo.
Prestar genera `Delivered` negativo en llenos y `LoanedToCustomer = 4` positivo en
el saldo del cliente. Retirar genera `CollectedEmpty` positivo en vacíos y
`LoanReturned = 5` negativo en el cliente. El retiro se valida contra el saldo
previo bloqueado: no incluye lo prestado en esa misma visita. Los saldos se ajustan
atómicamente dentro de la transacción. Los préstamos no agregan cargos ni cobros.

Se mantienen `clientRequestId` y `expectedSessionId`: un reintento ya registrado
devuelve la visita original antes de controlar la salida y el stock.
El detalle de salida devuelve `loansOut` y `loansIn` con cantidades positivas,
separadas de `containers`. El estado de cuenta incluye renglones `Loan` y
`LoanReturn`, sin importe y con el movimiento en `containerChanges`. La baja
incluye los préstamos pendientes entre los envases por retirar.
