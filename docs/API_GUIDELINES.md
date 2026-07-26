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

## Idempotencia y concurrencia

No agregar infraestructura genérica de idempotencia. Aplicarla a endpoints que realmente puedan duplicar operaciones sensibles. Usar tokens de concurrencia cuando haya riesgo real de sobrescribir cambios importantes.
