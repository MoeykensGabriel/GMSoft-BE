# Ventas y envases

## Promociones de prueba

Enviar `type: "Promotion"` al mismo endpoint de registro de visitas, con los
productos en `items`, `containersOut: []` y `payment: null`. La API fija precio
unitario y total en cero, descuenta los llenos del camion y registra automaticamente
los envases retornables prestados. Admite clientes existentes o `newCustomer` para
dar de alta a quien recibe su primera prueba. Requiere productos y stock suficiente;
aplican los mismos limites de devolucion que en una venta.

La promocion se conserva con su tipo propio en el historial del recorrido, no genera
deuda monetaria ni cobro y no cuenta como compra para reiniciar el estado de actividad.
Una compra posterior se registra como `Sale`; los envases pendientes se conservan
hasta su devolucion. Si el cliente ya debia dinero, esa deuda permanece.
No requiere migracion porque el tipo se almacena como entero. La seleccion de
promocion en la pantalla del chofer queda pendiente para la etapa de frontend.

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
registra los cobros independientes de una venta. El frontend conserva sus campos
actuales hasta abordar la etapa de pantallas; cantidades explicitas de envases que
contradigan la venta reciben un error de la API.
