# Arquitectura

## Objetivo

Mantener una arquitectura conocida, clara y suficiente para un SaaS de gestión de personal. Se reutilizan aprendizajes de El Señor Arroz, pero se evita copiar deuda técnica o patrones no necesarios.

## Monorepo

```text
frontend/     Vue 3 + Quasar
backend/      ASP.NET Core Web API
              Domain, Application, Infrastructure y API
docs/         Reglas y guías
```

## Backend

```text
HTTP Request
  -> Middleware de errores, seguridad y autenticación
  -> Controller
  -> Command o Query de Application
  -> Reglas y autorización sobre recurso
  -> DbContext / proveedor externo
  -> DTO de respuesta
```

### Legaria.Domain

Contiene entidades, enums pequeños, reglas invariantes y contratos estrictamente propios del dominio. No referencia EF Core, ASP.NET ni proveedores externos.

### Legaria.Application

Contiene casos de uso, DTO, validadores, interfaces de bordes y autorización de negocio. Se organiza por feature. Puede usar MediatR si se adopta desde el inicio, pero no es obligatorio crear commands y queries ceremoniales para operaciones triviales.

### Legaria.Infrastructure

Contiene EF Core, PostgreSQL, implementaciones de servicios, almacenamiento, hash de refresh token y proveedores externos.

### Legaria.API

Contiene controllers, middleware, filtros, configuración de autenticación, OpenAPI y composición de dependencias.

## Frontend

```text
src/
  boot/ or plugins/
  components/
    common/
  layouts/
  pages/
  router/
  services/
  stores/
  composables/
  types/
```

Organizar componentes específicos cerca del módulo o página. `components/common` se reserva para reutilización real.

## Datos

- PostgreSQL.
- EF Core con configuraciones por entidad.
- Migraciones versionadas en el repositorio.
- Identificadores consistentes; escoger `Guid` o entero antes de crear entidades y mantener la decisión.
- Fechas persistidas en UTC.
- Índices y restricciones para unicidad e aislamiento.

## Multitenancy

Se usa una base compartida con filas separadas por `organization_id`.

Reglas obligatorias:

- El usuario actual expone `OrganizationId` desde claims validados.
- Las consultas agregan el tenant explícitamente o mediante un mecanismo central probado.
- Las escrituras asignan el tenant en backend.
- La autorización valida pertenencia de las sucursales y recursos.
- Las pruebas intentan acceso cruzado entre dos organizaciones.

No se implementarán bases por tenant ni esquemas por tenant en esta fase.

## Contratos entre frontend y API

- DTO tipados.
- Errores consistentes con `ProblemDetails` o formato equivalente documentado.
- Paginación solo donde exista volumen real.
- El frontend no depende de nombres de columnas de base de datos.
- Cambios incompatibles deben actualizar frontend, pruebas y documentación en el mismo incremento.

## Dependencias

Agregar una dependencia solo cuando:

1. resuelve un problema real presente;
2. no existe una solución estándar adecuada;
3. su mantenimiento y seguridad son aceptables;
4. se documenta el motivo si afecta arquitectura.

No agregar Redis, colas, microservicios, GraphQL ni sistemas de eventos anticipadamente.
