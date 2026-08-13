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

```bash
dotnet run --project GMSoft.API
```

Swagger queda en la raíz: `http://localhost:5142`. El endpoint `GET /api/health`
no toca la base de datos.

La connection string va en `GMSoft.API/appsettings.Development.json` (no se versiona;
partí de `appsettings.Development.json.example`).

## Estado

Todavía no hay entidades ni migraciones: el modelo de datos está sin definir a propósito.
Mientras no exista la primera migración, la app arranca sin necesidad de tener Postgres
levantado.
