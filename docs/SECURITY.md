# Seguridad

Finalizar la relación laboral de un `BRANCH_ADMIN` suspende su cuenta, rota el `security_stamp` y revoca sesiones e invitaciones pendientes; las concesiones por sucursal se conservan como historial.

## Objetivo

Priorizar aislamiento multitenant, protección de sesiones y autorización sobre recursos. La seguridad no puede depender del frontend.

## Access token

- JWT firmado con HMAC SHA-256 y duración de 10 minutos.
- Validar issuer, audience, firma, lifetime y `ClockSkew=0`.
- Claims comunes: `sub`, `account_type`, rol, `security_stamp`, `jti`, `iat`, `nbf`, `exp`.
- Claims tenant: `organization_id` obligatorio y `employee_id` opcional.
- Un token de plataforma nunca contiene `organization_id`.
- No incluir datos laborales o sensibles.
- El frontend lo mantiene en memoria.

## Refresh token

- Valor aleatorio criptográficamente seguro.
- Cookie HttpOnly, Secure y SameSite configurado según despliegue.
- No devolverlo en JSON.
- Almacenar solo SHA-256 u otro hash apropiado del valor aleatorio.
- Rotar en cada uso.
- Registrar familia y token reemplazado.
- Detectar reutilización y revocar la familia.
- Expiración de siete días.
- Cookie host-only, `HttpOnly`, `Secure`, `SameSite=Lax` y path `/api/auth`.

## Endpoints iniciales

- `POST /api/auth/login`.
- `POST /api/auth/refresh`.
- `POST /api/auth/logout`.
- `POST /api/auth/logout-all`.
- `GET /api/auth/me`.
- `POST /api/auth/verify-email`.
- `POST /api/auth/resend-verification`.
- `POST /api/auth/forgot-password`.
- `POST /api/auth/reset-password`.
- `POST /api/auth/accept-invitation`.

## Login

- Mensaje genérico para credenciales inválidas.
- Rate limiting por IP: login 5 cada 5 minutos.
- Bloqueo de cuenta por 15 minutos después de cinco fallos.
- Reiniciar contador tras éxito.
- Usar `PasswordHasher<TUser>` o configuración aprobada de ASP.NET Identity.
- Validar contraseñas entre 8 y 128 caracteres, sin complejidad de composición obligatoria.

## Security stamp

Cambiar cuando:

- se modifica contraseña;
- se desactiva usuario;
- se revocan todas las sesiones;
- cambian permisos críticos si se requiere invalidación inmediata.

La implementación inicial consulta estado, `security_stamp`, roles y organización activa en cada request autenticado para garantizar revocación inmediata.

## Tokens de cuenta y correo

- Valores aleatorios de 256 bits.
- Persistir únicamente SHA-256 e indexar el hash.
- Verificación: 24 horas.
- Invitación tenant: 24 horas.
- Restablecimiento: 30 minutos.
- Uso único; emitir uno nuevo revoca los anteriores del mismo propósito.
- Recuperación y reenvío responden siempre de forma genérica.
- Reset y verificación tienen límite de 10 cada 15 minutos; recuperación y reenvío, 3 cada 15 minutos.
- Resend se usa detrás de `IEmailSender`, con timeout de 10 segundos, cancelación y sin reintentos automáticos.
- Las plantillas HTML codifican contenido y construyen enlaces desde `Frontend__BaseUrl`.
- Fallos del proveedor se registran sin correo, contenido, token ni secreto.
- El estado de entrega de una invitación se persiste después del commit; el fallo de correo no revierte la organización.
- Aceptar una invitación establece la contraseña elegida, verifica el correo y rota `security_stamp` en una sola transacción.

## Autorización

- Políticas para permisos generales.
- Autorización basada en recurso para tenant, sucursal y autoacceso.
- `BRANCH_ADMIN` no accede a su propio expediente.
- `BRANCH_ADMIN` solo lista trabajadores con asignación activa en una sucursal autorizada y su detalle se limita a las asignaciones de sucursales autorizadas.
- 404 puede usarse para evitar revelar recursos fuera del alcance.
- Un rol nunca reemplaza validación de organización.
- `OWNER` y `PLATFORM_ADMIN` administran datos de organizaciones; solo `OWNER` puede suspender o reactivar.
- La organización activa se comprueba en login, refresh y cada JWT tenant autenticado.
- `SUPER_ADMIN` administra sucursales y cuentas `BRANCH_ADMIN`; `BRANCH_ADMIN` solo puede leer sucursales con una concesión activa de su propia organización.
- Todo nuevo `BRANCH_ADMIN` se crea desde un trabajador y queda vinculado mediante `employee_id`; no se permite crear cuentas administrativas tenant huérfanas.
- Los accesos de sucursal se consultan en base de datos para cada operación y nunca se incluyen en el JWT.
- Suspender un `BRANCH_ADMIN` rota `security_stamp` y revoca refresh sessions e invitaciones pendientes. Reactivar no recupera sesiones anteriores.
- Una sucursal inactiva puede consultarse como historial por quien conserve acceso, pero no puede participar en asignaciones nuevas.

## Multitenancy

Toda consulta y modificación incluye el tenant autenticado. Pruebas obligatorias con dos organizaciones y IDs conocidos. Las claves foráneas y validaciones deben reducir referencias cruzadas.

## CORS y CSRF

- Lista explícita de orígenes.
- No `AllowAnyOrigin` junto con credenciales.
- Como refresh usa cookie, proteger el endpoint con SameSite apropiado y, cuando el despliegue lo requiera, token CSRF o validación de origen.
- Cookies Secure en producción.

## Archivos

- Buckets privados.
- Nombres internos aleatorios.
- Validar tamaño, tipo permitido y contenido cuando sea viable.
- No confiar en extensión.
- URLs firmadas de corta duración o streaming autorizado.
- No exponer rutas internas.

## Logs

No registrar:

- contraseñas;
- access o refresh tokens;
- cookies;
- documentos completos;
- datos médicos innecesarios.

Registrar eventos de seguridad con IDs internos, fecha, IP normalizada y resultado, evitando información secreta.

`SecurityAuditEvent` cubre bootstrap, verificación, cambio/restablecimiento de contraseña, cierre de sesiones, reutilización de refresh token y mutaciones de sucursales y administradores. Distingue actor tenant, cuenta afectada, organización y sucursal; nunca almacena correo, contraseña, token o clave externa.

## Rate limiting

- Login: 5 solicitudes por IP cada 5 minutos.
- Recuperación y reenvío: 3 por IP cada 15 minutos.
- Reset y verificación: 10 por IP cada 15 minutos.
- Refresh: 30 por IP por minuto.
- Los rechazos usan HTTP 429 y `ProblemDetails`.

## Configuración

- Secretos por variables o gestor de secretos.
- Fallar al iniciar si faltan claves críticas.
- HTTPS obligatorio en producción.
- Swagger restringido o deshabilitado según ambiente.
- Cabeceras de seguridad en frontend y API cuando correspondan.
