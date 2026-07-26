# Registro de decisiones

Este archivo documenta decisiones duraderas. Agregar una entrada cuando cambie arquitectura, seguridad, modelo de negocio o una convención que futuras tareas deban respetar.

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

**Consecuencia:** el frontend restaura sesión mediante `/auth/refresh` y no persiste refresh tokens en almacenamiento web.

## Pendientes deliberados

Estas decisiones no están cerradas y no deben inventarse:

- Renovaciones y versiones de contratos.
- Aprobación de vacaciones.
- Aprobadores de permisos.
- Firma digital definitiva.
- Flujo disciplinario completo.
- Política general de anulación y eliminación.
- Manejo detallado de trabajadores retirados.
