# Control de sesiones

Para qué sirve: entender por qué a un chofer "se le sale solo el sistema". Registra cada entrada del chofer a la app, cada salida y cada vez que el servidor rechaza su acceso. La pantalla está en el panel, en Empresa → Control de sesiones (`/panel/control-sesiones`), y es del módulo extra Control y estadísticas.

Solo se registran los choferes. La oficina no.

## Qué se anota

Cada evento es un `AccessEvent` con la hora, el chofer, el celular y el navegador.

| Evento | Quién lo anota | Cuándo |
|---|---|---|
| Entrada (`Login`) | El servidor, al dar el token | El chofer entra con usuario y contraseña |
| Salida (`Logout`) | La app avisa con `POST /api/auth/logout` | Toca "Cerrar sesión" (`Manual`), la oficina recibe el camión (`ReceptionCompleted`) o anula la salida (`DepartureCancelled`) |
| Rechazo (`Rejected`) | El servidor, al validar el token | El token venció (`Expired`), cambió la contraseña o el usuario (`CredentialsChanged`) o la cuenta se desactivó (`Deactivated`) |

- **La app no puede anotar rechazos.** El endpoint de salida solo acepta los tres motivos de la app.
- **Un token rechazado se anota una sola vez.** La app manda varios pedidos a la vez y todos rebotan. Se guarda el `jti` del token con un índice único.
- **Un token vencido se anota solo si su firma es válida.** Antes de anotarlo se vuelve a validar sin mirar la fecha: un token inventado no carga eventos.
- **Anotar nunca cambia la respuesta.** Si falla la escritura, el login o el 401 siguen igual y queda un aviso en el log.

## Desde dónde entró

- **Celular y navegador** salen del User-Agent, resumidos ("Android 14 · Chrome 130").
- **Cliente**:
  - `InstalledApp`: el ícono instalado. La app lo detecta con `display-mode: standalone`.
  - `Browser`: una pestaña común.
  - `EmbeddedBrowser`: el navegador de adentro de WhatsApp, Facebook o Instagram, o una vista web de Android (`; wv)`). Suele perder la sesión.
- **Identificador del celular (`DeviceId`)**: la app lo crea la primera vez y lo guarda en `localStorage` (`gmsoft.device-id`). Lo manda en cada entrada.

## Sesión perdida

La app siempre avisa al cerrar la sesión, y el servidor anota sus rechazos. Por eso **una entrada que viene justo después de otra entrada, sin una salida en el medio, es una sesión que se perdió**:

| Caso | Qué significa |
|---|---|
| `DataCleared`: el identificador del celular cambió | El celular borró los datos de la página (limpiador, ahorro de batería, incógnito) o entró desde otro navegador |
| `NoExitRecorded`: mismo identificador | La salida no llegó al servidor: cerró sin señal o la app se cerró sin avisar |

Para la primera entrada del día se mira el último evento del chofer en los 7 días anteriores.

**Problemas** de un chofer en el día: las sesiones perdidas y los vencimientos. Los cambios de contraseña y las desactivaciones no cuentan, porque los decide la oficina.

## Código

- Dominio: `AccessEvent`, `AccessEventKind`, `AccessEndReason`, `AccessClient`.
- Aplicación: `Features/Access` (`AccessDevice`, `AccessRecording`, `AccessControl`). La entrada se anota en `LoginCommandHandler`.
- API: `AccessRejections` desde los eventos del JWT en `Program.cs`. Informe en `GET /api/reports/access-control?day=AAAA-MM-DD`, con `[RequiresModule(Control)]`.
- Web: `modules/driverControl/AccessControlView.tsx`. La salida se avisa desde `AuthProvider`.
