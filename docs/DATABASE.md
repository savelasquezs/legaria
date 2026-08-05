# Modelo conceptual de datos

## Entidades principales

### Organization
Tenant propietario de sucursales, usuarios, trabajadores y expedientes. Conserva nombre comercial, razón social, NIT, DV, contacto, dirección, `municipality_code`, estado y fechas. `nit` tiene índice único global.

### Department y Municipality
Catálogo oficial DIVIPOLA MGN 2025 congelado. `Municipality` pertenece a `Department`; la organización referencia únicamente el municipio de cinco dígitos.

### Branch
Sucursal de una organización. Conserva nombre normalizado, contacto opcional, dirección, municipio DIVIPOLA, estado y fechas. Tiene nombre único por organización y puede desactivarse sin eliminar datos ni accesos.

### Employee
Persona trabajadora. No representa contrato, cargo ni cuenta de acceso.

### EmploymentRelationship
Vínculo laboral durante un periodo. Conserva fechas de inicio y terminación y permite recontrataciones del mismo Employee.

### EmployeeAssignment
Asignación histórica de una relación laboral a una sucursal y cargo. Puede haber varias activas y solo una principal activa por relación laboral.

### JobPosition

La base garantiza una sola `EmploymentRelationship` activa por trabajador y una sola `EmployeeAssignment` activa por relación y sucursal mediante índices parciales. `EmploymentLifecycleConstraints` agrega estas protecciones sin modificar el historial existente.
Cargo activo o inactivo definido por la organización y reutilizable en sucursales. Su nombre normalizado es único dentro del tenant.

## Documentos

### DocumentCategory
Categoría organizacional con nombre normalizado, descripción opcional, alcance inmutable `EMPLOYEE|BRANCH`, estado y fechas. Es única por organización, alcance y nombre normalizado.

### DocumentType
Pertenece a una categoría del mismo tenant y define nombre, descripción, obligatoriedad por defecto, modos `NEVER|OPTIONAL|REQUIRED` para expedición y vencimiento, multiplicidad de versiones vigentes y evidencias, y una lista no vacía de `PDF|IMAGE|VIDEO|LINK`. Es único por categoría y nombre normalizado.

Desactivar una categoría no modifica sus tipos, pero su disponibilidad efectiva exige que ambos registros estén activos. La clave foránea compuesta evita asociar tipos con categorías de otra organización.

### JobPositionDocumentRequirement

Relaciona un cargo con un tipo documental requerido mediante claves foráneas compuestas por organización. Su clave primaria evita duplicados y solo conserva la configuración vigente; cambiar la selección no altera el catálogo documental.

### EmployeeDocument
Archivo y metadatos entregados para un trabajador. Puede apuntar a asignación cuando aplique.

## Expedientes

### CaseType
Catálogo de tipos aprobados.

### EmployeeCase
Cabecera común del expediente: organización, trabajador, relación laboral opcional, sucursal opcional, tipo, estado básico, título, descripción y fechas.

### CaseComment
Comentario vinculado a cualquier expediente, con autor y fecha.

### CaseDocument
Adjunto reutilizable de cualquier expediente.

### Tablas de detalle

Una relación uno a uno desde EmployeeCase cuando el tipo requiera datos propios:

- ContractCase.
- DisabilityLeaveCase.
- PermissionCase.
- VacationCase.
- DisciplinaryCase.
- UniformDeliveryCase.

No crear columnas específicas de todos los módulos en EmployeeCase.

## Autenticación

### PlatformUser
Cuenta global `OWNER` o `PLATFORM_ADMIN`. No contiene organización y su correo normalizado es único.

### UserAccount
Cuenta de acceso de una organización, opcionalmente vinculada con Employee. `is_initial_administrator` identifica la única cuenta aprovisionada durante la creación del tenant.

### AccountEmail
Registro central de correos de cuenta. `normalized_email` es la clave primaria y cada fila referencia exactamente un `PlatformUser` o un `UserAccount`. El backfill de la migración incorpora las cuentas existentes.

### SystemRole y UserRole
Roles iniciales SUPER_ADMIN y BRANCH_ADMIN. Mantener separación del cargo.

### UserBranchAccess
Historial de sucursales autorizadas para administradores. Cada fila conserva organización, cuenta, sucursal, fecha y actor de concesión, y fecha y actor de revocación opcionales. Un índice parcial permite solo una concesión activa por pareja cuenta/sucursal y claves foráneas compuestas impiden referencias cruzadas de tenant.

### RefreshSession
Hash del token, familia, expiración, revocación, reemplazo, IP y user agent.

Debe pertenecer exactamente a un `PlatformUser` o un `UserAccount`. La base aplica un check constraint tanto sobre las claves como sobre `account_type`.

### AccountToken
Token de verificación, restablecimiento o invitación tenant. Guarda únicamente SHA-256, propósito, expiración, consumo, revocación y resultado de entrega (`delivered_at`/`delivery_failed_at`). También pertenece exactamente a un tipo de cuenta.

### SecurityAuditEvent
Evento persistente para operaciones sensibles. No contiene correo, token ni secreto.

## Restricciones mínimas

- Employee único por organización, tipo y número de documento.
- JobPosition único por organización y nombre normalizado.
- Email normalizado globalmente único mediante la clave primaria de `AccountEmail`.
- Un único `is_initial_administrator = true` por organización.
- NIT globalmente único y municipio válido por clave foránea.
- Relaciones y foreign keys deben impedir referencias cruzadas de tenant.
- Índices para `organization_id` combinado con filtros frecuentes.

## Migración inicial

`InitialIdentityAndAuthentication` crea la fundación de identidad. Las migraciones se aplican de forma explícita antes de iniciar la API; el startup no modifica el esquema.

`OrganizationProvisioningAndDivipola` agrega datos empresariales, reserva global de correos, invitaciones, organización en auditoría y el catálogo de 33 departamentos/1.122 municipios. La fuente y checksum están en `backend/src/Legaria.Infrastructure/Persistence/Data/README.md`.

`BranchesAndBranchAdministrators` agrega sucursales, historial `user_branch_accesses`, actor tenant y sucursal afectada en auditoría. No modifica ni inventa datos de las organizaciones y cuentas existentes.

`EmployeesEmploymentAndIntegratedBranchAdministration` agrega relaciones laborales, cargos y asignaciones. También hace único el vínculo cuenta-trabajador y falla si encuentra un `BRANCH_ADMIN` sin `employee_id`, en lugar de inventar una persona o documento.

`DocumentCatalog` agrega categorías y tipos documentales sin insertar datos iniciales ni crear todavía documentos o evidencias concretas.

`JobPositionDocumentRequirements` agrega la relación multitenant entre cargos y tipos documentales, sin calcular cumplimiento ni crear documentos de trabajadores.

## Eliminación

No aplicar soft delete universal. Cada entidad define si usa desactivación, cierre, reemplazo o anulación. Los expedientes y registros históricos no se eliminan normalmente.
