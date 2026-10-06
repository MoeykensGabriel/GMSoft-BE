# GMSoft — Backend

Backend en .NET 10 con Clean Architecture, una capa por proyecto.

## Capas

La regla de dependencias apunta siempre hacia adentro. Las flechas son las únicas
referencias permitidas:

```
GMSoft.API  ──►  GMSoft.Application  ──►  GMSoft.Domain
     │                                          ▲
     └──────►  GMSoft.Data  ────────────────────┘
                    │
                    └──►  GMSoft.Application  (implementa sus interfaces)
```

- **GMSoft.Domain** — entidades, enums y reglas puras. No referencia ninguna otra capa
  ni ningún paquete de infraestructura. `BaseEntity` da `Id`, auditoría y soft delete.
- **GMSoft.Application** — casos de uso (MediatR), DTOs, validaciones (FluentValidation),
  mapeos (Mapster) y las *interfaces* de repositorios y servicios. No conoce EF Core.
- **GMSoft.Data** — EF Core sobre PostgreSQL: `AppDbContext`, configuraciones,
  migraciones y la implementación de los repositorios que Application declara.
- **GMSoft.API** — controllers, middleware, DI y arranque. Es el único proyecto ejecutable.
- **GMSoft.Application.Tests** — xUnit. Incluye guardas que fallan si se rompe la regla
  de dependencias.

Cada capa se registra con una sola llamada: `AddApplicationLayer()` y `AddDataLayer(configuration)`.

## Qué ya está resuelto

- Soft delete global: `SaveChangesAsync` intercepta los deletes y las queries filtran
  `IsDeleted` automáticamente. Nunca hay un DELETE físico.
- Auditoría automática: `Id`, `CreatedAt` y `UpdatedAt` los completa el `DbContext`.
  **Nunca se asigna `Id` a mano.**
- Errores como ProblemDetails (RFC 7807): las excepciones de `Application.Common.Exceptions`
  se traducen al status HTTP correcto en `GlobalExceptionHandler`.
- Validación automática de todo Command/Query vía `ValidationBehaviour` de MediatR.
- Logging con Serilog (consola + archivo diario en `logs/`).
- CORS por `CORS_ORIGINS` o `Cors:AllowedOrigins`.
- Connection string resuelta desde `CONNECTION_STRING` / `DATABASE_URL` /
  `DATABASE_PUBLIC_URL` o `appsettings`, aceptando el formato URI de Railway.

## Correr en local

Doble clic en `run-api.bat`, o desde la terminal:

```bash
dotnet run --project GMSoft.API
```

Swagger queda en la raíz: `http://localhost:5142`. El endpoint `GET /api/health`
no toca la base de datos.

La connection string va en `GMSoft.API/appsettings.Development.json` (no se versiona;
partí de `appsettings.Development.json.example`).

## Iniciar BE y FE juntos en Linux

Con ambos repositorios en carpetas hermanas (`GMSoft-BE` y `GMSoft-FE`),
desde la carpeta del backend:

```bash
bash tools/start-dev.sh
```

Inicia la API en el puerto 5000 y el frontend en el 3000, configura su comunicación
y permite acceder al panel desde otros dispositivos de la misma red. `Ctrl+C`
detiene ambos proyectos, incluyendo sus procesos hijos. Si uno falla al arrancar,
se detiene el otro y se muestra el error en esa terminal.

Requiere .NET, Node/npm y las dependencias del frontend instaladas (`npm install`,
una sola vez después de clonar). PostgreSQL y la conexión del backend deben estar
configurados previamente. No es necesario abrir dos sesiones SSH; la terminal
debe permanecer abierta mientras se usa el sistema.

Reutiliza `JWT_SECRET_KEY` o, si no está definida, la clave guardada en
`~/.config/gmsoft/jwt-secret`. También respeta la clave de `appsettings` si no hay
ninguna de las anteriores. No genera una firma nueva en cada arranque.

Si las carpetas tienen otros nombres, indicá la del frontend:

```bash
bash tools/start-dev.sh /ruta/al/frontend
```

En equipos con varias interfaces de red podés fijar la IP que usan el celular y Windows:

```bash
GMSOFT_SERVER_IP=192.168.1.71 bash tools/start-dev.sh
```

Para guardarla y seguir usando el comando corto, ejecutá una sola vez:

```bash
mkdir -p ~/.config/gmsoft
printf '%s\n' '192.168.1.71' > ~/.config/gmsoft/server-ip
```

El iniciador usa primero `GMSOFT_SERVER_IP`, después la IP guardada y, si no hay
ninguna, detecta una IPv4 del equipo. En este arranque el frontend consulta la API
de esa misma IP en el puerto 5000, incluso si había un `VITE_API_URL` anterior
apuntando a localhost. Si cambia la IP del servidor, actualizá el archivo guardado.

Desde Windows o el celular, abrí `http://192.168.1.71:3000/login`. `localhost`
significa el dispositivo donde corre el navegador, por lo que no sirve para entrar
al servidor Linux desde otro equipo. El iniciador no abre un navegador automáticamente.

## Prueba de humo del circuito

Con la API corriendo:

```bash
python tools/e2e-smoke.py
```

Recorre el negocio entero contra la base real: crea zona, vehiculo, producto, chofer y
cliente, abre una sesion con 100 bidones, registra una visita que vende 10 y retira 8
vacios, y cierra. Verifica que el camion quede en 90 llenos y 8 vacios, que el cliente
quede con 2 envases y su deuda, y que el cierre cuadre. Despues fuerza un faltante a
proposito y comprueba la rendicion. Cada corrida usa datos nuevos, asi que se puede
repetir sin limpiar nada.

## CI

`.github/workflows/ci.yml` corre en cada push y pull request: compila, pasa los tests de
unidad, verifica que el modelo coincida con la migracion, y despues **levanta la API contra
un Postgres real y corre la prueba de humo**. Esa ultima parte es la que importa: los errores
de integracion (saldos escritos dos veces, permisos mal combinados) no los ve ni el
compilador ni un test de unidad.

## Estado

Todavía no hay entidades ni migraciones: el modelo de datos está sin definir a propósito.
Mientras no exista la primera migración, la app arranca sin necesidad de tener Postgres
levantado.
