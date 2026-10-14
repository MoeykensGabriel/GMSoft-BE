# Publicar GM-Soft en Railway

Se usan tres servicios dentro del mismo proyecto: API (`GM-SoftBE`), web (`GM-SoftFE`) y PostgreSQL administrado por Railway. No hace falta instalar Docker en tu PC. Los archivos están preparados; este trabajo no crea servicios, ni hace commit o push.

## 1. Crear el proyecto y la base

1. Entrá al panel de Railway y elegí **New Project** (nuevo proyecto).
2. Agregá una base **PostgreSQL** desde **Add/New → Database**. Esperá a que esté disponible. Conservá el nombre `Postgres` para usar las referencias de abajo; si elegís otro, seleccioná ese servicio con el autocompletado de variables.
3. Usá el mismo proyecto y entorno para los tres servicios, para que la API acceda a la base por la red privada.
4. Antes de usar datos reales, confirmá en Railway si el Postgres del plan Hobby incluye respaldos, si están activados, su frecuencia y retención, y cómo restaurarlos. No des por hecho que están incluidos o configurados.

## 2. Publicar primero la API

Los archivos de despliegue deben estar en GitHub: cuando decidas publicarlos, incorporalos a `main` en cada repo.

1. Dentro del proyecto, elegí **Add/New → GitHub Repo** y seleccioná `GM-SoftBE`. Autorizá a Railway a acceder al repo si lo pide.
2. En **Settings → Source**, seleccioná la rama `main` y dejá habilitados los despliegues automáticos. Usá la raíz del repo (no la carpeta `GMSoft.API`). Cada push a `main` debe iniciar un despliegue.
3. Railway toma el `Dockerfile` y `railway.json`. Dejá sin reemplazos manuales los comandos de build y arranque.
4. En **Variables**, cargá esta tabla y aplicá los cambios. Si el primer intento arrancó antes de cargar las variables y falló, desplegá otra vez.

| Variable de API | Valor o de dónde sale |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `CONNECTION_STRING` | `${{Postgres.DATABASE_URL}}`. Es una referencia a la variable del servicio Postgres, seleccionable con el autocompletado. Usá la URL interna, no la pública. |
| `JWT_SECRET_KEY` | Generá un secreto aleatorio de al menos 64 caracteres con un gestor de contraseñas y pegalo solamente en Railway. Guardalo en ese gestor. No uses una frase inventada ni lo subas a GitHub. |
| `CORS_ORIGINS` | La URL pública HTTPS de la web, sin barra final ni rutas. Todavía no la tenés: podés omitir esta variable durante el primer arranque y completarla en el paso 4. |
| `Database__MigrateOnStartup` | `true` (son dos guiones bajos). Aplica las migraciones y crea el administrador inicial al arrancar. |
| `Seed__AdminUserName` | El nombre de usuario que vas a usar para entrar. Por ejemplo, `admin`. Usá letras y números sin espacios; Identity también admite `-._@+`. Son dos guiones bajos en el nombre de la variable. |
| `SEED_ADMIN_PASSWORD` | Una contraseña nueva generada y guardada en tu gestor. Mínimo 8 caracteres, con mayúscula, minúscula y número; se recomienda bastante más larga. Los símbolos son opcionales. Pegala solo en Railway. |

`DatabaseSeeder` lee `Seed:AdminUserName` mediante `Seed__AdminUserName`. Para la contraseña, prioriza `SEED_ADMIN_PASSWORD` y luego `Seed:AdminPassword` (también configurable como `Seed__AdminPassword`); usá solo la primera para evitar confusión. El email es opcional. Si el usuario ya existe, el seed no cambia su contraseña. Si faltan usuario o contraseña, omite su creación; si la contraseña no cumple los requisitos, registra un error pero la API puede quedar saludable igual. Revisá el log y probá iniciar sesión.

La conexión admite `CONNECTION_STRING`, luego `DATABASE_URL`, luego `DATABASE_PUBLIC_URL`, en ese orden. Configurá solo `CONNECTION_STRING` con la referencia indicada. La URI interna `postgresql://...@postgres.railway.internal:5432/railway` se convierte al formato de Npgsql. `Prefer` permite conectar sin TLS si el servidor interno no lo ofrece; si la URI incluye `sslmode=`, se respeta (incluidos `disable`, `require`, `verify-ca` y `verify-full`). Un modo inválido produce un error explícito.

5. Cuando el despliegue esté listo, abrí **Settings → Networking → Public Networking → Generate Domain**. Si pide puerto de destino, usá el puerto que muestra el log de arranque y que coincide con `PORT`.
6. Guardá la URL HTTPS pública y abrí esa URL seguida de `/api/health`. Debe responder con `status: "ok"`; no necesita iniciar sesión. La raíz y Swagger no son la prueba de salud en producción.

Railway inyecta `PORT`. El arranque del Dockerfile hace que la API escuche en `0.0.0.0` en ese puerto, o en `8080` si no existe. No hay que definir `ASPNETCORE_URLS` ni cambiar la configuración local. La imagen final usa el usuario no root `app` y contiene solamente la publicación de la API, sin tests ni tools.

## 3. Publicar la web

1. En el mismo proyecto elegí **Add/New → GitHub Repo** y seleccioná `GM-SoftFE`.
2. En **Settings → Source**, seleccioná `main`, activá los despliegues automáticos y usá la raíz del repo. Dejá que Railway use el Dockerfile sin comandos manuales.
3. En **Variables**, agregá lo siguiente antes de construir la versión definitiva:

| Variable de web | Valor o de dónde sale |
| --- | --- |
| `VITE_API_URL` | La URL pública HTTPS de la API del paso anterior, sin barra final y sin `/api`. Ejemplo ilustrativo: `https://ejemplo.up.railway.app`. |

Railway pasa esta variable como argumento de construcción (`ARG`) al Dockerfile. Vite la incorpora a los archivos de la web durante el build: cambiarla exige una nueva construcción/despliegue, no alcanza con reiniciar el contenedor. Es una URL pública, nunca pongas secretos en variables `VITE_`.

4. Aplicá los cambios y desplegá. En **Settings → Networking**, generá el dominio público de la web. Usá como puerto de destino el `PORT` asignado por Railway si el panel lo solicita.

nginx toma `PORT` usando el mecanismo de templates de la imagen oficial; fuera de Railway el valor predeterminado es `8080`. Las rutas de la aplicación vuelven a `index.html`, que no se almacena en caché; los archivos de `/assets/` tienen caché de un año.

## 4. Completar la conexión entre web y API

1. Volvé a **API → Variables** y cargá `CORS_ORIGINS` con la URL pública de la web, sin barra final. Si hay más de un dominio permitido, separalos con comas.
2. Aplicá el cambio y esperá el nuevo despliegue de la API.
3. Abrí la web e iniciá sesión con el administrador configurado. Probá recargar una pantalla interna para verificar las rutas de la web.

Después, cada push a `main` despliega el servicio del repo correspondiente. Un cambio de API no reconstruye la web ni viceversa. No borres el servicio Postgres para volver a desplegar la aplicación.

## Logs y problemas frecuentes

Seleccioná el servicio, abrí **Deployments**, entrá al despliegue y consultá los logs. Los logs de build muestran errores de compilación; los de ejecución muestran el arranque, migraciones y errores de conexión. La API escribe también a consola, visible en Railway. No compartas capturas que contengan secretos o URLs con contraseña.

Si falla el healthcheck de API:

- Confirmá que la ruta sea `/api/health` y que el arranque escuche en `0.0.0.0` en el `PORT` asignado. Quitá cualquier comando de arranque manual que reemplace el del Dockerfile.
- Revisá que exista `JWT_SECRET_KEY` y que la referencia de `CONNECTION_STRING` apunte al servicio correcto, dentro del mismo entorno y con Postgres disponible.
- Buscá errores de migraciones o conexión: con `Database__MigrateOnStartup=true`, un fallo en esa etapa impide iniciar la API. Si la primera migración tarda más que el límite del healthcheck, revisá primero los logs y luego ajustá ese tiempo en Railway.
- `/api/health` comprueba que la API responde; no consulta la base. Un resultado correcto no reemplaza la prueba de inicio de sesión y uso de la aplicación.

Si la web abre pero no conecta, revisá `VITE_API_URL` (reconstruí después de cambiarla), la URL de `CORS_ORIGINS` y los logs de la API. Si aparece el login pero no podés entrar, revisá los mensajes de creación del admin; cambiar las variables del seed no cambia una cuenta ya existente.

## Referencias

- [Dockerfiles y argumentos de build en Railway](https://docs.railway.com/builds/dockerfiles).
- [Variables y referencias entre servicios](https://docs.railway.com/variables).
- [Configuración de Railway como archivo](https://docs.railway.com/config-as-code/reference).
- [TLS y SslMode de Npgsql](https://www.npgsql.org/doc/security.html).
