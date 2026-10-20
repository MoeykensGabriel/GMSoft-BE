# Documentación de GM-Soft — Villa Esperanza

Entrada a la documentación de los dos repositorios. Manuales y guías revisados contra el código disponible el **20 de octubre de 2026**. Describen esa versión; no certifican qué commit está publicado.

## Para usar

| Documento | Para quién y para qué |
| --- | --- |
| [Manual de oficina](manual/oficina.md) | ADMIN y oficina: menú, preparación, reparto, recepción y control del día. |
| [Manual del chofer](manual/chofer.md) | Repartidores: hoja, visitas, ventas, envases y problemas de señal. |
| [Glosario](glosario.md) | Oficina, choferes y desarrollo: términos del negocio y sus nombres en el código. |
| [Caso de estudio](caso-de-estudio/caso-de-estudio.md) | Dueño, nuevos integrantes y analistas: problema, actores, requisitos y alcance. |

## Para desarrollar

| Documento | Para quién y para qué |
| --- | --- |
| [README del backend](../README.md) | Desarrollo backend: solución, convenciones y arranque local. |
| [README del frontend](../../GM-SoftFE/README.md) | Desarrollo web: instalación, organización y verificaciones. |
| [Arquitectura técnica](arquitectura-tecnica/arquitectura-tecnica.md) | Desarrollo e infraestructura: stack, datos, transacciones, seguridad y Docker. |
| [Arquitectura del frontend](../../GM-SoftFE/docs/arquitectura.md) | Desarrollo web: módulos, dependencias, caché, sesiones y envíos del celular. |
| [Sistema visual](../../GM-SoftFE/docs/design-system.md) | Diseño y desarrollo web: tokens, componentes y adaptación a celular. |
| [Home de ADMIN](admin-home.md) | Desarrollo: consulta de salidas abiertas y carga inicial de cada camión. |
| [Ventas y envases](delivery-rules.md) | Desarrollo: stock, cobro completo, devoluciones, préstamos y reintentos. |
| [Cuenta del cliente](customer-account.md) | Desarrollo: historial, pagos, saldos y contrato de la cuenta corriente. |
| [Actividad de clientes](customer-activity.md) | Desarrollo: semanas sin compra y colores de actividad; ver salvedad abajo. |
| [Asignación y días de visita](customer-visit-days.md) | Desarrollo: camión, zona, días, orden y última visita. |
| [Bajas pedidas](customer-removal.md) | Desarrollo: solicitud del chofer, confirmación y rechazo de oficina. |
| [Promociones](promotions.md) | Desarrollo: prospectos, pruebas, retiro y conversión a cliente. |
| [Recargas](restocks.md) | Desarrollo: tandas sobre una salida abierta, stock e idempotencia. |
| [Recorridos](route-planning.md) | Desarrollo: orden y asignaciones de oficina; orden guardado por el chofer. |
| [Agenda semanal del camión](vehicle-schedule.md) | Desarrollo: sugerencias de zona y control de cobertura. |
| [Visitas sin compra](no-purchase.md) | Desarrollo: motivos, visitas y clientes sin visitar; ver salvedad abajo. |
| [Horario laboral](working-hours.md) | Desarrollo: configuración semanal, excepciones y marcas de horario. |
| [Resumen diario](daily-summary.md) | Desarrollo: conciliación de productos y efectivo por día de apertura. |
| [Rendimiento por camión](vehicle-performance.md) | Desarrollo: indicadores, filtros y límites de interpretación. |
| [Pruebas backend](testing.md) | Desarrollo y QA: aplicación, PostgreSQL efímero y recorridos HTTP. |
| [Simulación local de actividad](../tools/ActivitySimulation/README.md) | Desarrollo y QA: escenarios locales y reversión; no es una herramienta de producción. |
| [Usuarios y arquitectura — SVG](caso-de-estudio/usuarios-arquitectura.svg) | Analistas y desarrollo: lámina del caso de estudio; [PNG](caso-de-estudio/usuarios-arquitectura.png). |
| [Ciclo del reparto — SVG](caso-de-estudio/ciclo-reparto.svg) | Analistas y desarrollo: lámina del circuito; [PNG](caso-de-estudio/ciclo-reparto.png). |
| [Caso de estudio editable](caso-de-estudio/gmsoft-arquitectura.excalidraw) | Quien mantiene los diagramas: las dos láminas en Excalidraw. |
| [Stack y capas — SVG](arquitectura-tecnica/stack-capas.svg) | Desarrollo: lámina técnica; [PNG](arquitectura-tecnica/stack-capas.png). |
| [Datos y transacciones — SVG](arquitectura-tecnica/datos-transacciones.svg) | Desarrollo: lámina de persistencia; [PNG](arquitectura-tecnica/datos-transacciones.png). |
| [Docker y seguridad — SVG](arquitectura-tecnica/docker-seguridad.svg) | Infraestructura: lámina de despliegue; [PNG](arquitectura-tecnica/docker-seguridad.png). |
| [Arquitectura técnica editable](arquitectura-tecnica/gmsoft-tecnico.excalidraw) | Quien mantiene los diagramas: las tres láminas en Excalidraw. |

## Para operar

| Documento | Para quién y para qué |
| --- | --- |
| [Operación](operacion.md) | Responsable de producción: publicación, migraciones, copias, recuperación y scripts de la carpeta común. |
| [Publicar en Railway](deploy-railway.md) | Infraestructura: alta y configuración de servicios, variables y Dockerfiles. |
| [Prueba por red local](../../GM-SoftFE/docs/local-network.md) | Desarrollo y soporte: API en Linux y web en Windows dentro de la LAN; no describe producción. |

Los enlaces a `GM-SoftFE` y a herramientas de la carpeta común requieren tener los dos repositorios juntos. En GitHub, abrí el otro repositorio si el enlace relativo sale del actual.

## Cómo leer documentos de distintas fechas

El caso de estudio y la arquitectura técnica conservan la fecha y los commits de su análisis. Sus diagramas no incluyen necesariamente funciones posteriores, como la anulación o el orden guardado por el chofer. Los contratos describen la API; un endpoint disponible no implica que exista un botón para usarlo.

Diferencias detectadas al contrastar con el código actual:

- `customer-activity.md` explica los endpoints de configuración y luego dice que no existen. Sí existen y se usan desde Configuración. La frase equivalente del README backend también quedó vieja.
- El README frontend menciona una comprobación de recepción cada 10 segundos. `DriverSessionLayout` usa 30 segundos, además de foco y reconexión.
- `no-purchase.md` dice que un reintento necesita salida abierta. El handler actual busca primero un envío ya registrado y puede devolverlo después del cierre. Una visita nueva sí necesita salida abierta.
- `daily-summary.md` contrapone su cálculo de efectivo con un cálculo histórico de liquidación sobre todos los medios. Actualmente `GetSessionSettlementQueryHandler` también compara solamente efectivo.
- `customer-visit-days.md` generaliza que cambiar el camión mueve al cliente al final. La edición individual lo hace; el guardado en bloque de Recorridos conserva las posiciones del lote al cambiar solo camión. Cambiar de zona sí lo lleva al final de la zona nueva.
- `working-hours.md` llama futura a Configuración y dice que no se agregó horario a la suite HTTP. La pantalla existe y `testing.md` ya describe ese recorrido de pruebas.
- El README backend generaliza que nunca hay borrado físico y que los IDs siempre se asignan al guardar. `AppDbContext` limita la baja lógica a entidades `BaseEntity` en su guardado asíncrono y solo genera un ID si está vacío; no es una regla universal de todas las tablas o escrituras. La arquitectura técnica ya describe ese alcance.
- `local-network.md` conserva una paleta inicial neutra. La referencia visual actual es `design-system.md`. Su frase “sin animaciones” también tiene una excepción en el código: el menú móvil de ADMIN usa transiciones con respeto por movimiento reducido.
- El historial de tareas conserva estados anteriores. La tarea 10 decía que no habría “volver más tarde”; la 19 y el código actual sí lo incorporan. Las tareas 20 y 21 no forman parte de las funciones vigentes: ganancias quedó pausada y la corrección de mezcla de envases se descartó.

Las guías nuevas toman el código como referencia funcional. La producción en Railway y la publicación automática de `main` son datos informados por el dueño. Los controles operativos pendientes de verificación están reunidos como **«A confirmar»** en [Operación](operacion.md#puntos-a-confirmar).
