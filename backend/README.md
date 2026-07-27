# Backend de Legaria

Solución ASP.NET Core 8 organizada en API, Application, Domain e Infrastructure, sin MediatR ni capas ceremoniales.

## Proyectos

```text
Legaria.sln
src/
  Legaria.API/             HTTP, JWT, rate limiting y composición
  Legaria.Application/     Casos de uso y contratos
  Legaria.Domain/          Entidades e invariantes
  Legaria.Infrastructure/  EF Core, PostgreSQL, JWT, hash y Resend
tests/
  Legaria.UnitTests/
  Legaria.IntegrationTests/
```

## Base de datos

La migración inicial es `InitialIdentityAndAuthentication`. La API no aplica migraciones automáticamente:

```powershell
dotnet ef database update `
  --project src/Legaria.Infrastructure `
  --startup-project src/Legaria.API
```

Una vez migrada la base, el startup crea transaccionalmente el primer `OWNER` si no existe ningún `PlatformUser`.

## Ejecución

Definir las variables documentadas en `../.env.example` y ejecutar:

```powershell
dotnet run --project src/Legaria.API
```

El correo usa el SDK oficial de Resend mediante `IEmailSender`. Las pruebas sustituyen ese límite y nunca envían correo real.
