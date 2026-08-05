# Guías de API

## Estilo

- API REST sobre HTTPS.
- Rutas en plural y minúsculas: `/api/employees`.
- Usar verbos HTTP y no verbos redundantes en rutas.
- Acciones de dominio explícitas solo cuando CRUD no representa bien el cambio, por ejemplo `/api/auth/logout-all`.

## Respuestas

Usar códigos HTTP correctamente:

- 200 lectura o actualización exitosa.
- 201 creación con ubicación cuando aplique.
- 204 operación exitosa sin cuerpo.
- 400 entrada inválida.
- 401 no autenticado o token inválido.
- 403 autenticado sin permiso.
- 404 recurso no encontrado dentro del tenant visible.
- 409 conflicto de estado o unicidad.
- 422 solo si el proyecto adopta esa distinción consistentemente.
- 429 límite de solicitudes.

No envolver cada respuesta exitosa en estructuras ceremoniales si el DTO directo es suficiente. Para errores, preferir `ProblemDetails` con `code`, `title`, `detail`, `status` y errores por campo.

## Seguridad

- `organization_id` nunca determina seguridad desde query, header o body.
- Los identificadores de sucursal enviados por un SUPER_ADMIN siguen siendo validados contra su organización.
- Buscar recursos por `id` y `organization_id`.
- No revelar si un recurso existe en otro tenant.

## Listados

Agregar paginación cuando el volumen lo justifique. Contrato sugerido:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalItems": 0,
  "totalPages": 0
}
```

Validar límites máximos de `pageSize`. Ordenamiento y filtros usan listas permitidas, no nombres SQL libres.

## Fechas y enums

- Fechas en ISO 8601.
- Backend persiste UTC.
- Enums como strings estables en la API cuando mejoren legibilidad.
- No reutilizar el texto mostrado en UI como código persistido.

## Versionado

No introducir `/v1` hasta que exista necesidad real de mantener versiones incompatibles. Documentar contratos mediante OpenAPI desde el inicio.

## Autenticación inicial

Todos los contratos viven bajo `/api/auth`:

- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`
- `POST /api/auth/logout-all`
- `GET /api/auth/me`
- `POST /api/auth/verify-email`
- `POST /api/auth/resend-verification`
- `POST /api/auth/forgot-password`
- `POST /api/auth/reset-password`
- `POST /api/auth/accept-invitation`

Login y refresh retornan `accessToken`, `expiresAtUtc` y una cuenta tipada. La cuenta contiene `accountType`, identidad y roles; `organizationId` y `employeeId` aparecen únicamente para cuentas tenant.

Los errores de autenticación usan `ProblemDetails` y un `code` estable. Los códigos públicos incluyen:

- `auth.invalid_credentials`
- `auth.account_locked`
- `auth.email_not_verified`
- `auth.token_invalid`
- `auth.token_expired`
- `auth.token_used`

Recuperación y reenvío siempre responden con un mensaje genérico, exista o no la cuenta. Refresh y logout consumen la cookie host-only cuyo path es `/api/auth`; no se admite el token en el cuerpo.

## Organizaciones de plataforma

| Método | Ruta | Política |
| --- | --- | --- |
| `GET` | `/api/platform/organizations` | `OWNER`, `PLATFORM_ADMIN` |
| `POST` | `/api/platform/organizations` | `OWNER`, `PLATFORM_ADMIN` |
| `GET` | `/api/platform/organizations/{id}` | `OWNER`, `PLATFORM_ADMIN` |
| `PUT` | `/api/platform/organizations/{id}` | `OWNER`, `PLATFORM_ADMIN` |
| `POST` | `/api/platform/organizations/{id}/suspend` | `OWNER` |
| `POST` | `/api/platform/organizations/{id}/reactivate` | `OWNER` |
| `PUT` | `/api/platform/organizations/{id}/initial-admin` | `OWNER`, `PLATFORM_ADMIN` |
| `POST` | `/api/platform/organizations/{id}/initial-admin/invitations` | `OWNER`, `PLATFORM_ADMIN` |
| `GET` | `/api/catalogs/departments` | usuario de plataforma |
| `GET` | `/api/catalogs/departments/{code}/municipalities` | usuario de plataforma |

El listado acepta `page` (1), `pageSize` (20, máximo 100), `search` y `status=ACTIVE|SUSPENDED`. Crear responde `201` con `Location`; nunca recibe contraseña. Los estados públicos de invitación son `PENDING_DELIVERY`, `SENT`, `DELIVERY_FAILED`, `EXPIRED` y `ACCEPTED`.

Códigos estables adicionales:

- `organization.invalid_nit`, `organization.duplicate_nit`, `organization.invalid_municipality`.
- `organization.invalid_status_transition`, `organization.initial_admin_already_accepted`.
- `account.duplicate_email`.
- `invitation.invalid`, `invitation.expired`, `invitation.used`, `invitation.organization_suspended`.

## Sucursales tenant

| Método | Ruta | Política |
| --- | --- | --- |
| `GET` | `/api/tenant/branches` | `SUPER_ADMIN`, `BRANCH_ADMIN` con resultado limitado a sus asignaciones |
| `POST` | `/api/tenant/branches` | `SUPER_ADMIN` |
| `GET` | `/api/tenant/branches/{id}` | `SUPER_ADMIN` o `BRANCH_ADMIN` asignado |
| `PUT` | `/api/tenant/branches/{id}` | `SUPER_ADMIN` |
| `POST` | `/api/tenant/branches/{id}/deactivate` | `SUPER_ADMIN` |
| `POST` | `/api/tenant/branches/{id}/reactivate` | `SUPER_ADMIN` |

El listado acepta `page` (1), `pageSize` (20, máximo 100), `search` y `status=ACTIVE|INACTIVE`. El tenant siempre se obtiene del JWT validado. Nombre, dirección y municipio DIVIPOLA son obligatorios; correo y teléfono son opcionales. El nombre normalizado es único dentro de la organización.

## Administradores de sucursal

| Método | Ruta | Política |
| --- | --- | --- |
| `GET` | `/api/tenant/branch-administrators` | `SUPER_ADMIN` |
| `POST` | `/api/tenant/branch-administrators` | `SUPER_ADMIN` |
| `GET` | `/api/tenant/branch-administrators/{id}` | `SUPER_ADMIN` |
| `PUT` | `/api/tenant/branch-administrators/{id}/pending-profile` | `SUPER_ADMIN`, solo pendiente |
| `PUT` | `/api/tenant/branch-administrators/{id}/branches` | `SUPER_ADMIN` |
| `POST` | `/api/tenant/branch-administrators/{id}/invitations` | `SUPER_ADMIN`, solo pendiente y activo |
| `POST` | `/api/tenant/branch-administrators/{id}/suspend` | `SUPER_ADMIN` |
| `POST` | `/api/tenant/branch-administrators/{id}/reactivate` | `SUPER_ADMIN` |

Crear y corregir reciben `firstName`, `lastName`, `email` y `branchIds`, nunca contraseña. Las sucursales seleccionadas deben estar activas, pertenecer al tenant autenticado y contener al menos un elemento. Actualizar una identidad pendiente genera y entrega una invitación nueva; actualizar solo `branchIds` no reinvita.

Estados públicos de cuenta: `ACTIVE|SUSPENDED`. Estados de invitación: `PENDING_DELIVERY|SENT|DELIVERY_FAILED|EXPIRED|ACCEPTED|REVOKED`.

Códigos estables adicionales:

- `branch.not_found`, `branch.invalid_data`, `branch.duplicate_name`, `branch.invalid_municipality`, `branch.invalid_status_transition`.
- `branch_administrator.not_found`, `branch_administrator.already_accepted`, `branch_administrator.invalid_status_transition`.
- `branch_access.required`, `branch_access.invalid`.

## Idempotencia y concurrencia

No agregar infraestructura genérica de idempotencia. Aplicarla a endpoints que realmente puedan duplicar operaciones sensibles. Usar tokens de concurrencia cuando haya riesgo real de sobrescribir cambios importantes.
