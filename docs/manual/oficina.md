# Manual de oficina — Villa Esperanza

Para usar el panel de **ADMIN**, en `/panel`. [Índice de documentación](../README.md).

## El trabajo del día

1. Revisá en Home qué camiones siguen en la calle y qué avisos hay.
2. Prepará la **Carga inicial** con los productos llenos y los días que cubrirá el camión.
3. El chofer abre la salida con zona y kilómetros. También podés abrirla desde oficina.
4. Si necesita más mercadería, registrá una **Recarga** sobre esa misma salida.
5. Cuando vuelve, contá llenos, vacíos y efectivo. Hacé la **Recepción del camión**.
6. Revisá **Liquidación** y **Resumen diario**. Completá las rendiciones pendientes y corregí cualquier error de recepción.

Una carga preparada todavía no es una salida abierta. Una venta a deuda sigue siendo una venta. La deuda de dinero y los envases prestados se controlan por separado.

Para empezar desde cero, primero cargá zonas y productos, después vehículos y choferes con su usuario y vehículo asignado. Configurá la agenda y los clientes. Las pantallas están explicadas abajo en el orden del menú.

## Home

La tarjeta **Camiones en la calle** muestra las salidas abiertas, aunque hayan empezado otro día. Indica vehículo, patente, chofer, zona, hora y productos llenos con los que salió.

Esa carga es la inicial. No disminuye con las ventas ni suma las recargas. Para ver lo que lleva ahora, entrá a **Ver salida**. También podés elegir **Recargar**.

Se actualiza periódicamente y al volver a la pantalla. Usá **Actualizar** si necesitás revisar en ese momento. Una salida recibida o anulada deja de aparecer.

Home también avisa sobre **bajas pedidas** y clientes cuyos días no coinciden con la agenda del camión.

## Clientes

### Clientes

Elegí un camión. Después podés filtrar por día y buscar por nombre, dirección o teléfono. Abrí una fila para consultar la ficha, la cuenta y los envases. **Editar** permite corregir los datos.

Para dar un alta, elegí **Nuevo cliente** y completá:

- Nombre y apellido, dirección y teléfono.
- Zona de reparto y camión asignado.
- Uno o más días de visita. Hoy viene marcado; podés cambiarlo.
- Comercio o razón social, email e indicaciones, si hacen falta.

Piso, departamento, casa de atrás o timbre se escriben en **Indicaciones para el chofer**. No se pide fecha de nacimiento. Guardá con **Guardar cliente**.

El alta de oficina no registra una venta ni una visita. El cliente se agrega al final del recorrido de su zona. Si el chofer lo crea al vender, toma el camión de esa salida y elige entre sus zonas; por defecto, la primera.

En la ficha se distinguen:

- **Saldo:** dinero pendiente. Un saldo negativo es dinero a favor del cliente.
- **Envases en poder del cliente:** cantidades por producto y, cuando corresponde, unidades con número de serie. También incluye el dispenser prestado sin cargo.
- **Movimientos de la cuenta:** ventas, cobros, visitas, promociones convertidas, préstamos y otros movimientos registrados. Un movimiento de envases no cancela dinero.

Ejemplo ficticio: si un cliente tenía tres bidones, recibe cuatro y devuelve tres, queda con cuatro bidones pendientes. No se puede registrar una devolución mayor que lo permitido para ese producto. Si el registro no coincide con lo contado, consultá el caso antes de seguir; el chofer no tiene una corrección automática de la mezcla.

Los colores **blanco, rojo y negro** muestran actividad de compra. Se cuentan semanas en las que hubo un reparto recibido que le correspondía y no compró. Una semana sin reparto no suma. Una venta vuelve a iniciar la cuenta. Estar rojo o negro no equivale a estar dado de baja.

Para desactivar o reactivar un cliente, entrá a **Editar** y revisá **Cliente activo**. El historial, la deuda y los envases se conservan. Un cliente inactivo sale de la hoja; el retiro posterior de sus envases no tiene hoy un circuito especial en la app del chofer. Revisá los pendientes antes de desactivarlo.

### Recorridos

Elegí **camión, zona y día**. Los tres son necesarios para ver la lista.

Podés subir o bajar clientes, arrastrarlos y cambiar sus días, camión o zona. Revisá cuáles saldrán de la lista por esos cambios. Nada se aplica hasta **Guardar cambios**. Podés descartar el borrador.

Hay un solo orden por cliente dentro de su zona. Si visita varios días, no tiene un orden distinto para cada uno. Reordenar un día cambia la posición relativa de esos clientes también en otros días.

El chofer también puede guardar el orden desde **Organizar recorrido**. La oficina ve el último cambio, quién lo hizo y cuándo. Si alguien cambió la lista mientras la estabas editando, el sistema pide recargar para no pisar el trabajo ajeno.

Los avisos de cobertura comparan días, zona y agenda semanal del vehículo. Son una ayuda: podés revisar y **Guardar igual**. No reemplazan la decisión de oficina.

### Bajas pedidas

El chofer puede pedir una baja con motivo. Cuando el pedido llega al servidor, el cliente deja de aparecer en su hoja y no puede recibir nuevas ventas. La decisión final es de oficina.

1. Abrí el pedido. Revisá motivo, fecha, chofer, deuda y envases declarados en ese momento.
2. **Confirmá** si corresponde: el cliente queda inactivo.
3. **Rechazá** si debe seguir: vuelve a su lugar del recorrido.

La baja no borra el historial, no perdona la deuda y no registra una devolución de envases. Los préstamos del dispenser también cuentan entre lo que falta recuperar. Solo ADMIN puede reactivar después a un cliente inactivo desde su edición.

### Promociones

Elegí vehículo y, si hace falta, estado y fechas de registro. Abrí el detalle para ver persona, productos, envases prestados, retiro previsto y resultado.

Una promoción es una **prueba gratis a una persona que todavía no es cliente**. La registra el chofer. Descuenta mercadería del camión, pero no genera cobro ni deuda de dinero.

- **Pendiente:** falta resolverla; el filtro incluye las vencidas.
- **Vencida:** pasó la fecha de retiro y sigue pendiente.
- **Convertida:** la persona se hizo cliente; sus envases pasaron al saldo del cliente.
- **No convertida:** se cerró la prueba. Lo recuperado y lo perdido quedan registrados por separado.

La oficina consulta y controla el plazo en **Configuración**. El registro y el cierre se hacen desde la app del chofer. Cambiar el plazo general no mueve las fechas de promociones ya registradas.

## Reparto

### Carga inicial

Se usa antes de abrir la salida, para registrar los **llenos** que llevará el vehículo.

1. Elegí el vehículo. Revisá la patente y los choferes asignados.
2. Marcá los **Días de reparto que cubrirá esta salida**. Podés combinar varios para recuperar un reparto atrasado.
3. Anotá las cantidades de cada producto.
4. Presioná **Confirmar**. Revisá vehículo, productos, cantidades, días y hora en el resumen.
5. Confirmá definitivamente. Esperá el aviso **Carga registrada**.

Abrir o cerrar el resumen no registra nada. La hora definitiva la pone el sistema al guardar. Confirmar la carga habilita la apertura; no abre la salida por sí solo.

Podés sumar otra tanda antes de salir. El chofer verá la suma por producto: cinco más dos aparecen como siete. Oficina conserva las tandas separadas para poder bajar una en particular.

Si hubo un error antes de salir:

- **Bajar** quita ese renglón pendiente.
- **Vaciar carga** quita toda la carga pendiente del vehículo.
- Ambas acciones piden confirmación. El antecedente queda en **Cargas bajadas**.
- **Guardar días del recorrido** cambia los días sin agregar productos.

Lo que ya salió no se baja desde esta pantalla. Un camión en la calle se atiende desde Recargas. La carga no comprueba un inventario general del depósito: registra lo que se sube al vehículo.

### Recargas

Elegí un camión en la calle y tocá **Recargar**. Anotá los llenos que se agregan, revisá el resumen y confirmá.

La recarga suma productos a la misma salida. Puede haber varias. El chofer ve el stock actualizado y un aviso. Si la respuesta no se confirma, reintentá desde el mismo formulario, conservando el pedido.

No hay edición ni anulación de una recarga registrada. Un error se revisa al recibir y conciliar la salida. No anotes vacíos en una recarga: se cuentan juntos en la recepción final, aunque se hayan descargado antes.

### Salidas y recepción

Por defecto ves las recibidas **hoy** y las que siguen en la calle. Las abiertas pueden ser de días anteriores. Para consultar recepciones anteriores, cambiá **Recibidas el día**.

El filtro usa la fecha de recepción, no la de apertura. Una salida de ayer recibida hoy aparece hoy.

#### Abrir una salida desde oficina

Entrá a **Abrir salida**, elegí al chofer y revisá el vehículo que figura. Comprobá la carga desde Carga inicial. Revisá las zonas precargadas, agregá o quitá las necesarias y ordenalas con las flechas. Anotá kilometraje y abrí la salida. El vehículo es el asignado a ese chofer. Si ya está abierta, el chofer entra directamente a esa salida desde su cuenta.

No se puede abrir sin carga pendiente, sin vehículo asignado ni con kilómetros menores al registro del vehículo. Tampoco puede haber dos salidas abiertas para el mismo camión o chofer.

La agenda sugiere las zonas en orden; podés cambiarlas. Si la lista o el orden difieren, aparece un aviso y podés abrir igual. Los días vienen de la carga y quedan fijados para esa salida.

#### Recibir el camión y la plata

1. Entrá a **Ver salida → Recibir el camión**.
2. Anotá el **Kilometraje de vuelta**. Debe ser igual o mayor al de salida.
3. Contá y anotá los **llenos y vacíos** que volvieron, por producto. Incluí lo retirado de préstamos en el conteo que corresponda; el dispenser retirado figura como vacío.
4. Contá el dinero y completá **Efectivo entregado**. La pantalla muestra cuánto se cobró en efectivo para compararlo.
5. Si la plata llega después, marcá esa opción. La salida se recibe y queda **Falta rendir**.
6. Revisá los clientes sin visitar. Es un aviso; no impide recibir.
7. Tocá **Recibir el camión**. Revisá kilómetros, diferencias de productos y efectivo. Elegí **Volver a contar** o **Confirmar y cerrar**.

Anotá lo que contaste, aunque no coincida. El sistema conserva las diferencias. No descuenta automáticamente esos faltantes a una persona.

Las transferencias y tarjetas se consultan aparte: no son efectivo que el chofer deba entregar. Una venta a deuda tampoco es un faltante de caja.

La recepción cierra la salida. El celular vuelve al login cuando detecta el cierre; sin conexión puede demorarse.

#### Corregir una recepción

En una salida ya recibida, elegí **Corregir recepción**. Cambiá solo el dato incorrecto y escribí el motivo. Guardá con **Guardar corrección**.

Las cantidades son el **total correcto que volvió**, no cuánto querés sumar o restar. Podés corregir llenos, vacíos, kilómetros y efectivo ya rendido. Si todavía falta la rendición, cargala desde el detalle de la salida, sección **Liquidación**.

Los kilómetros corregidos no pueden ser menores a los de apertura ni superar los de una salida posterior del mismo vehículo. Las correcciones quedan con fecha, usuario, motivo, valor anterior y nuevo. La salida sigue cerrada; no se reabre ni se eliminan las ventas.

#### Anular una salida

Usá **Anular salida**, en rojo, solo si se abrió pero todavía no hubo actividad. Escribí el motivo y confirmá.

No se permite si tiene visitas —incluido “No compró”—, cobros, recargas, registro o cierre de promociones u otros movimientos distintos de la carga inicial. En ese caso hay que recibir el camión.

La carga vuelve a quedar pendiente para abrir otra salida. La anulada sale de los listados e informes habituales, pero conserva quién la anuló, cuándo y por qué. El chofer vuelve al login cuando su celular detecta la anulación.

### Liquidación

Elegí vehículo y fecha de **apertura** del reparto. Ves lo atendido por cliente, productos, envases, pagos por medio y saldo. Si hubo varias salidas, se muestran separadas y se suman los totales.

Para completar la plata pendiente, entrá al enlace de la salida. En la sección **Liquidación**, escribí **Cuánta plata llegó** y elegí **Registrar rendición**. Si ya se rindió mal, usá **Corregir recepción**.

No confundas **vendió**, **cobró** y **entregó**. Lo vendido menos lo cobrado es un saldo neto a cuenta. La diferencia de caja compara solo efectivo cobrado con efectivo entregado.

### Resumen diario

Elegí camión y fecha de **apertura**. Un bloque por salida muestra productos y dinero. Si hay varias, también aparece un total del día.

- **Llenos:** cargados, entregados, rendidos y diferencia. Incluye carga inicial y recargas. Las promociones y préstamos también afectan la mercadería entregada, aunque no sean ventas cobradas.
- **Vacíos:** contabilizados, rendidos y diferencia.
- **Dinero:** efectivo esperado, declarado y diferencia. Transferencia y tarjeta son informativas.

Con la salida abierta, lo pendiente sigue a bordo: el control es provisional. Después de recibir, una diferencia positiva significa faltante; una negativa, sobrante. Si falta alguna rendición, el efectivo total del día queda pendiente, no se presenta como un cuadre completo.

Una salida que abrió ayer y volvió hoy se busca por ayer en Liquidación y Resumen diario; en Salidas y recepción se busca por hoy.

### Hoja de ruta

Elegí el camión y la fecha (por defecto, hoy). Aparecen todos los clientes que ese camión visita ese día de la semana, en orden de recorrido: nombre, dirección, los envases que tiene cada uno por tipo y su saldo (positivo es deuda). Si el día tiene más de una zona, cada zona va con su título, en el orden de la agenda del camión.

Con **Imprimir o guardar PDF** se abre la impresión del navegador: elegí la impresora o **Guardar como PDF**. En el papel sale solo la hoja, sin el menú. Los clientes inactivos y los que tienen una baja pedida no aparecen, igual que en la hoja del chofer.

### Estadísticas

Elegí un período para comparar camiones: ventas, cobros, visitas, motivos sin compra, productos, kilómetros y diferencias. Puede incluir salidas abiertas, cuyos valores todavía cambian.

Es un reporte operativo. No calcula ganancias, costos, gastos ni comisiones.

## Empresa

### Configuración

Reúne tres ajustes:

- **Horario laboral:** días de trabajo, desde/hasta y límite de apertura. Sin horario configurado no se aplican restricciones. Fuera del horario se bloquean carga, apertura y recarga; ADMIN puede confirmar una excepción explícita. El chofer no puede hacerlo. Ventas, recepción y rendición se registran igual, con una marca.
- **Actividad de clientes:** semanas para pasar a rojo y a negro. El negro debe venir después del rojo. Cambiarlo no necesita reiniciar el sistema.
- **Promociones:** días hasta el retiro de las nuevas pruebas. Las fechas ya guardadas se conservan.

### Vehículos

Creá el vehículo con nombre, patente, tipo y kilómetros actuales. Para modificarlo, abrí su edición. El kilometraje no puede retroceder desde este formulario.

Después de crear el vehículo, entrá a editarlo para completar su **agenda semanal**: hasta cinco zonas por día, en orden, o ninguna si no sale. Usá **Agregar zona**, **Quitar** y las flechas para subir o bajar; no se repiten zonas dentro del mismo día. La misma zona puede aparecer en varios días. Revisá el resumen antes de confirmar la agenda.

Si el sistema muestra una sugerencia tomada del historial, revisala y guardala para convertirla en agenda confirmada. La agenda orienta y ayuda a detectar clientes sin cubrir. No impide por sí sola elegir otra zona o guardar una asignación distinta.

La asignación del chofer se hace en **Choferes**, no en esta agenda.

### Choferes

En **Nuevo chofer**, cargá nombre, apellido, documento, teléfono y vehículo. Completá el usuario de acceso y la contraseña con su confirmación; el email es opcional. **Crear chofer y usuario** guarda ambos juntos.

Sin vehículo asignado, el chofer no puede abrir una salida. No puede salir un segundo chofer con un vehículo que ya está en la calle.

Al editar podés cambiar su ficha, vehículo y condición de activo. El usuario y el email quedan como se definieron al crearlo. La contraseña se cambia en su formulario específico. Desactivar corta su acceso, pero no recibe físicamente el camión ni resuelve por sí solo una salida abierta.

### Productos

Creá o editá detalle, precio de lista y seguimiento. Podés completar detalle comercial e imagen. **Publicado** habilita el producto para los circuitos de carga y venta.

Elegí el seguimiento que corresponda:

- **Por cantidad:** bidones o sifones. Cada unidad entregada genera un envase pendiente; lo devuelto lo reduce.
- **Por número de serie:** unidades identificadas. Es un circuito distinto del préstamo común; este manual no ofrece una pantalla móvil de alta de series.
- **No lleva envase:** una venta no genera deuda de envases por sí misma.

Para prestar el dispenser común sin cobrar, elegí **No lleva envase** y marcá **Se puede prestar**. También puede venderse a su precio: el chofer debe elegir la cantidad de venta o la de préstamo según lo ocurrido. Prestar no es vender con precio cero.

Un préstamo descuenta mercadería y queda a nombre del cliente. El retiro reduce ese préstamo y sube al camión como vacío. Los cambios se ven en la ficha, la cuenta y la salida.

No se puede cambiar el modo de seguimiento cuando ya existe historia de envases. El sistema indica cómo continuar. El catálogo no es una pantalla de inventario global del depósito.

### Grupo de intercambio de envases

En Productos, al elegir seguimiento **Por cantidad**, aparece **Grupo de
intercambio**. Poné el mismo texto, por ejemplo «Bidones», en los productos cuyos
vacíos se pueden recibir cuando el cliente cambia de producto. No distingue
mayúsculas, admite hasta 50 caracteres y puede quedar vacío para no permitir
intercambios. No agrupes soda con bidones si no corresponde.

El cambio se habilita con una venta del mismo grupo y tiene como límite los
llenos comprados en esa visita. En el estado de cuenta, la fila de la visita
conserva «Devolvió» y agrega **Cambio de envase: devolvió 1 vacío de Bidón 20 L y
se llevó Bidón 12 L**. No es un ajuste manual ni una pérdida. El detalle de la
salida y la recepción cuentan únicamente los vacíos efectivamente recibidos.
Las devoluciones sin compra conservan las reglas habituales.

**Para activarlo** (una sola vez, también en producción después de subir la
versión): entrá a **Productos**, abrí **Bidón 12 L**, escribí «Bidones» en
**Grupo de intercambio** y guardá. Repetí con **Bidón 20 L**. Hasta que los dos
tengan el mismo grupo, el sistema sigue rechazando el vacío no registrado.

### Zonas de reparto

Creá o editá las zonas que usarán clientes, agendas y salidas. Completá nombre y notas si hacen falta; revisá que esté activa para ofrecerla al abrir el reparto.

Cambiar la agenda no cambia automáticamente la zona o los días de los clientes. Revisá los avisos de cobertura y corregí las asignaciones desde Clientes o Recorridos.

## Si algo no coincide

No repitas una carga o venta como una operación nueva si solo falta la respuesta. Usá el reintento del formulario y revisá el detalle. Antes de recibir, pedile al chofer que compruebe si tiene visitas guardadas en el celular sin enviar.

Para avisar un problema, anotá pantalla, camión, salida, hora y mensaje. Compartí los datos del caso por el canal de trabajo, sin publicar claves ni información de clientes.

Reglas detalladas para desarrollo: [ventas y préstamos](../delivery-rules.md), [recargas](../restocks.md), [recorridos](../route-planning.md) y [resumen diario](../daily-summary.md).
