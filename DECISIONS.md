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

## 2026-08-04 — Aprovisionamiento tenant por invitación

**Decisión:** `OWNER` y `PLATFORM_ADMIN` crean organizaciones junto con una cuenta inicial `SUPER_ADMIN` sin contraseña compartida. La cuenta se activa mediante una invitación de un solo uso y 24 horas.

**Consecuencia:** la creación se confirma antes del envío; un fallo de Resend conserva el tenant y habilita reenvío. Corregir o reenviar revoca el token anterior.

## 2026-08-04 — Unicidad global de correos en base de datos

**Decisión:** `account_emails` es el registro central cuya clave primaria es `normalized_email`; referencia exactamente una cuenta de plataforma o tenant.

**Motivo:** dos índices en tablas separadas no evitan carreras entre tipos de cuenta.

**Consecuencia:** bootstrap, aprovisionamiento y corrección de cuentas deben crear o mover la reserva dentro de su transacción.

## 2026-08-04 — Organización legal y DIVIPOLA congelado

**Decisión:** NIT y DV se guardan separados; NIT usa solo dígitos y se valida con módulo 11. La ubicación usa la capa oficial DIVIPOLA MGN 2025 de DANE, congelada en la migración `OrganizationProvisioningAndDivipola`.

**Consecuencia:** la aplicación no consulta DANE durante la operación normal. Una actualización del catálogo requiere otra migración versionada. La migración actual falla deliberadamente si encuentra organizaciones antiguas sin datos empresariales, en lugar de inventarlos.

## 2026-08-04 — Suspensión reversible de organizaciones

**Decisión:** solamente `OWNER` suspende o reactiva. La suspensión invalida inmediatamente el uso de cualquier identidad tenant mediante la validación de organización activa, sin revocar sesiones persistidas.

**Consecuencia:** reactivar recupera acceso y sesiones todavía vigentes sin perder información.

## 2026-08-04 — Acceso dinámico e histórico por sucursal

**Decisión:** los accesos de `BRANCH_ADMIN` se almacenan en `UserBranchAccess` como concesiones con fecha y actor de otorgamiento/revocación. No se incluyen identificadores de sucursal en el JWT.

**Consecuencia:** cada operación consulta PostgreSQL y una revocación tiene efecto inmediato. Volver a conceder acceso crea una fila nueva y conserva el historial anterior.

## 2026-08-04 — Suspensión fuerte del administrador de sucursal

**Decisión:** suspender una cuenta `BRANCH_ADMIN` rota `security_stamp` y revoca sesiones refresh e invitaciones pendientes, sin eliminar sus accesos por sucursal.

**Consecuencia:** JWT, login y refresh quedan bloqueados inmediatamente. Reactivar exige iniciar sesión de nuevo; una cuenta todavía pendiente debe recibir una invitación nueva.

## 2026-08-04 — Invitación tenant reutilizable

**Decisión:** el ciclo de emisión, reemplazo, entrega, estado y aceptación de invitaciones tenant se centraliza para `SUPER_ADMIN` y `BRANCH_ADMIN`.

**Consecuencia:** ambos flujos conservan 24 horas de vigencia, token SHA-256 de un solo uso, entrega posterior al commit y estado `REVOKED` cuando una suspensión deja la cuenta pendiente sin invitación utilizable.

## 2026-08-05 — Administración integrada en trabajadores

**Decisión:** no existe un panel tenant independiente de administradores. Los trabajadores se crean o asignan desde la sucursal y, opcionalmente, reciben una cuenta vinculada con rol `BRANCH_ADMIN` e invitación segura.

**Consecuencia:** todo nuevo `BRANCH_ADMIN` tiene `employee_id`; la relación laboral y el acceso administrativo por sucursal se conservan como conceptos independientes. El primer `SUPER_ADMIN` aprovisionado continúa siendo la única excepción permitida sin trabajador.

## 2026-08-05 — Base laboral para asignaciones por sucursal

**Decisión:** `EmploymentRelationship` conserva cada vínculo laboral, `EmployeeAssignment` su historial de sucursal/cargo y `JobPosition` el catálogo de cargos del tenant.

**Consecuencia:** un trabajador puede tener varias asignaciones activas, pero solo una principal activa por relación laboral. Las claves foráneas compuestas impiden referencias cruzadas entre organizaciones.

## 2026-08-05 — Ciclo laboral sin solapamientos

**Decisión:** las fechas laborales no pueden ser futuras; una transición termina el periodo anterior el día previo y una relación finalizada cierra sus asignaciones activas.

**Consecuencia:** una recontratación crea otra relación. Si existe una cuenta `BRANCH_ADMIN`, el retiro la suspende y revoca sesiones e invitaciones sin borrar accesos históricos ni reactivarla automáticamente al recontratar.

## Pendientes deliberados

Estas decisiones no están cerradas y no deben inventarse:

- Renovaciones y versiones de contratos.
- Aprobación de vacaciones.
- Aprobadores de permisos.
- Firma digital definitiva.
- Flujo disciplinario completo.
- Política general de anulación y eliminación.
- Manejo detallado de trabajadores retirados.
