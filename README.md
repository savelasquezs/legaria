# Legaria

Legaria es un SaaS multitenant para administrar trabajadores, sucursales, documentos, expedientes laborales y autenticación segura.

## Estado actual

El repositorio contiene el primer incremento funcional:

- API ASP.NET Core 8 con PostgreSQL y EF Core.
- Autenticación diferenciada para cuentas de plataforma y cuentas tenant.
- JWT corto, refresh token rotatorio en cookie segura y recuperación/verificación por correo.
- Bootstrap transaccional del primer propietario de plataforma.
- Integración de correo con Resend detrás de `IEmailSender`.
- Aplicación Vue 3 + Quasar para login, recuperación, restablecimiento y verificación.
- Pruebas unitarias, de integración con PostgreSQL/Testcontainers y de frontend.

Este incremento no incluye registro público, administración de organizaciones, dashboard, planes, pagos ni módulos laborales.

## Estructura

```text
frontend/                 Vue 3, Quasar, Pinia, Axios y Vitest
backend/
  Legaria.sln
  src/
    Legaria.API/
    Legaria.Application/
    Legaria.Domain/
    Legaria.Infrastructure/
  tests/
    Legaria.UnitTests/
    Legaria.IntegrationTests/
docs/                     Documentación funcional y técnica
AGENTS.md                  Reglas obligatorias para agentes
BUSINESS_RULES.md          Reglas de negocio aprobadas
DECISIONS.md               Registro de decisiones duraderas
```

## Configuración local

1. Copiar `.env.example` a un archivo local no versionado o definir sus variables en el entorno.
2. Proporcionar PostgreSQL, JWT, Resend, URL del frontend y las credenciales de bootstrap.
3. Aplicar la migración antes de iniciar la API:

```powershell
dotnet ef database update `
  --project backend/src/Legaria.Infrastructure `
  --startup-project backend/src/Legaria.API
```

4. Iniciar backend y frontend:

```powershell
dotnet run --project backend/src/Legaria.API

cd frontend
npm.cmd install
npm.cmd run dev
```

El bootstrap se ejecuta al arrancar la API, después de que la migración haya sido aplicada. Si todavía no existe ningún `PlatformUser`, exige las variables `BootstrapOwner__*`, crea un único `OWNER` con correo prevalidado y registra auditoría. Después del primer arranque exitoso debe retirarse `BootstrapOwner__Password` del entorno.

La cookie de refresh siempre es `Secure`; el desarrollo local debe usar HTTPS. Frontend y API se despliegan bajo el mismo sitio y la cookie usa `SameSite=Lax`.

## Verificación

```powershell
dotnet format backend/Legaria.sln --verify-no-changes
dotnet build backend/Legaria.sln
dotnet test backend/Legaria.sln

cd frontend
npm.cmd run lint
npm.cmd run test
npm.cmd run build
```

Las pruebas de integración requieren un motor Docker operativo para crear PostgreSQL mediante Testcontainers.

## Documentación obligatoria

Antes de modificar el sistema, leer `AGENTS.md`, `BUSINESS_RULES.md` y los documentos relacionados en `docs/`.
