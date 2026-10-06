# Home de ADMIN

El panel usa un menú lateral compartido. `/` dirige ADMIN a `/panel` y al chofer
a `/reparto`. Home contiene la card «Camiones en la calle», independiente de las
otras secciones, y cada camión enlaza al detalle existente de su salida.

GET `/api/sessions/active-departures` requiere ADMIN. Devuelve todas las salidas
abiertas, incluso de días anteriores, con camión, patente, chofer, zona, hora
de salida y `initialLoad` agrupado por producto. No usa el stock pendiente del
depósito ni el saldo actual: solo movimientos InitialLoad de llenos, que quedaron
registrados al abrir la salida. Ventas, devoluciones y recargas posteriores no
alteran ese resumen. Las salidas cerradas desaparecen y las antiguas sin carga
registrada muestran ese estado explícitamente.

El repositorio usa lectura sin seguimiento y una consulta dividida de cantidad
fija, sin pedir un detalle por camión. No se limita la lista a la primera página.
Home actualiza al recuperar el foco, cada 60 segundos y con el botón Actualizar.
Tiene estados de carga, error y lista vacía. En pantallas pequeñas el menú se
abre con un botón; en escritorio permanece visible. No necesita migraciones.
