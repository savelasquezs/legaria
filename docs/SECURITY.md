# Seguridad

## Objetivo

Priorizar aislamiento multitenant, protección de sesiones y autorización sobre recursos. La seguridad no puede depender del frontend.

## Access token

- JWT firmado con algoritmo aprobado por la plataforma.
- Duración inicial sugerida: 10 minutos.
- Validar issuer, audience, firma, lifetime y `ClockSkew` estricto.
- Claims mínimos: `sub`, `organization_id`, `employee_id` opcional, rol, `security_stamp`, `jti`, `iat`, `nbf`, `exp`.
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
- Expiración inicial sugerida: siete días.

## Endpoints iniciales

- `POST /api/auth/login`.
- `POST /api/auth/refresh`.
- `POST /api/auth/logout`.
- `POST /api/auth/logout-all`.
- `GET /api/auth/me`.

## Login

- Mensaje genérico para credenciales inválidas.
- Rate limiting por IP y, cuando convenga, por cuenta normalizada.
- Bloqueo temporal progresivo después de intentos fallidos.
- Reiniciar contador tras éxito.
- Usar `PasswordHasher<TUser>` o configuración aprobada de ASP.NET Identity.
- No limitar silenciosamente contraseñas largas a un tamaño inseguro; validar máximo razonable.

## Security stamp

Cambiar cuando:

- se modifica contraseña;
- se desactiva usuario;
- se revocan todas las sesiones;
- cambian permisos críticos si se requiere invalidación inmediata.

La validación puede consultar estado de usuario con una estrategia equilibrada. No agregar una consulta de base de datos por request sin medirla; sí garantizar revocación efectiva para operaciones sensibles.

## Autorización

- Políticas para permisos generales.
- Autorización basada en recurso para tenant, sucursal y autoacceso.
- `BRANCH_ADMIN` no accede a su propio expediente.
- 404 puede usarse para evitar revelar recursos fuera del alcance.
- Un rol nunca reemplaza validación de organización.

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

## Configuración

- Secretos por variables o gestor de secretos.
- Fallar al iniciar si faltan claves críticas.
- HTTPS obligatorio en producción.
- Swagger restringido o deshabilitado según ambiente.
- Cabeceras de seguridad en frontend y API cuando correspondan.
