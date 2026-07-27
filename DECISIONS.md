# Registro de decisiones

Este archivo documenta decisiones duraderas. Agregar una entrada cuando cambie arquitectura, seguridad, modelo de negocio o una convención que futuras tareas deban respetar.

## 2026-07-26 — Nombre del proyecto

**Decisión:** el producto, el repositorio y los proyectos de código se llamarán Legaria.

**Consecuencia:** la solución .NET usará `Legaria.sln` y los proyectos y namespaces usarán el prefijo `Legaria`.

## 2026-07-26 — Monorepo inicial

**Decisión:** frontend, backend y documentación vivirán en un solo repositorio.

**Motivo:** facilita coordinación de contratos, pruebas y cambios completos durante la primera etapa.

**Consecuencia:** las tareas deben indicar qué parte modifican y ejecutar las verificaciones correspondientes.

## 2026-07-26 — Backend como ASP.NET Core Web API

**Decisión:** el backend será una API .NET 8 organizada en API, Application, Domain e Infrastructure.

**Motivo:** reutilizar experiencia y patrones útiles de El Señor Arroz sin copiar complejidad innecesaria.

## 2026-07-26 — SaaS multitenant

**Decisión:** cada organización es un tenant y las tablas de negocio relevantes incluyen `organization_id`.

**Consecuencia:** el tenant se obtiene de la identidad autenticada y todas las consultas lo aplican.

## 2026-07-26 — Administradores como trabajadores

**Decisión:** una cuenta de usuario puede referenciar un `Employee`; los administradores laborales tendrán esa relación.

**Consecuencia:** se puede aplicar la prohibición de autoacceso al expediente.

## 2026-07-26 — Administrador sin acceso a su expediente

**Decisión:** `BRANCH_ADMIN` no consulta ni modifica su propio expediente. `SUPER_ADMIN` sí puede hacerlo.

**Consecuencia:** la autorización debe evaluar el recurso, no solo el rol.

## 2026-07-26 — Múltiples sucursales por trabajador y administrador

**Decisión:** un trabajador puede tener varias asignaciones activas y un administrador acceso a varias sucursales.

## 2026-07-26 — Núcleo común de expedientes

**Decisión:** `EmployeeCase` contiene información común. Cada tipo tiene tabla especializada y todos pueden usar comentarios y documentos.

**Motivo:** evitar una tabla gigante sin crear una infraestructura genérica excesiva.

## 2026-07-26 — JWT y refresh token seguro

**Decisión:** access token corto en memoria; refresh token rotatorio mediante cookie HttpOnly; hash en base de datos.

**Consecuencia:** el frontend restaura sesión mediante `/api/auth/refresh` y no persiste tokens en almacenamiento web.

## 2026-07-26 — Identidad global y tenant separadas

**Decisión:** `PlatformUser` representa cuentas globales `OWNER` y `PLATFORM_ADMIN`; `UserAccount` representa cuentas tenant. Todos los identificadores son `Guid` y el correo es único globalmente entre ambos conjuntos.

**Consecuencia:** los JWT de plataforma no contienen `organization_id`; los JWT tenant siempre conservan su organización y validan que permanezca activa.

## 2026-07-26 — Bootstrap del propietario

**Decisión:** el primer `OWNER` se crea transaccionalmente al arrancar después de aplicar la migración. Su correo nace validado y las variables son obligatorias solo mientras no exista ningún `PlatformUser`.

**Consecuencia:** la contraseña de bootstrap debe retirarse del entorno después de la primera creación exitosa.

## 2026-07-26 — Resend detrás de un límite propio

**Decisión:** el SDK oficial de Resend se encapsula mediante `IEmailSender`; verificación y restablecimiento usan plantillas HTML embebidas y URLs derivadas de `Frontend__BaseUrl`.

**Consecuencia:** las pruebas sustituyen el sender, no hay reintentos automáticos y ningún secreto de Resend se versiona. El remitente compartido de El Señor Arroz solo se probará cuando se proporcionen variables externas.

## 2026-07-26 — Auditoría de seguridad persistente

**Decisión:** bootstrap, verificación, cambios de contraseña, cierres de sesión y reutilización de tokens generan `SecurityAuditEvent` en PostgreSQL.

**Consecuencia:** los eventos contienen identificadores internos y contexto técnico limitado, nunca correos, tokens o secretos.

## 2026-07-26 — Migraciones fuera del startup

**Decisión:** la API no ejecuta migraciones automáticamente.

**Consecuencia:** producción debe ejecutar `dotnet ef database update` antes del arranque; después el startup puede ejecutar el bootstrap.

## Pendientes deliberados

Estas decisiones no están cerradas y no deben inventarse:

- Renovaciones y versiones de contratos.
- Aprobación de vacaciones.
- Aprobadores de permisos.
- Firma digital definitiva.
- Flujo disciplinario completo.
- Política general de anulación y eliminación.
- Manejo detallado de trabajadores retirados.
