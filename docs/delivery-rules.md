# Ventas y envases

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
