# Modelo conceptual de datos

## Entidades principales

### Organization
Tenant propietario de sucursales, usuarios, trabajadores y expedientes.

### Branch
Sucursal de una organización. Puede desactivarse, no eliminarse si tiene historial.

### Employee
Persona trabajadora. No representa contrato, cargo ni cuenta de acceso.

### EmploymentRelationship
Vínculo laboral durante un periodo. Permite recontrataciones del mismo Employee.

### EmployeeAssignment
Asignación de una relación laboral a una sucursal y cargo. Puede haber varias activas.

### JobPosition
Cargo definido por la organización y reutilizable en sucursales.

## Documentos

### DocumentType
Define el tipo, alcance y necesidad de fechas de emisión o vencimiento.

### PositionDocumentRequirement
Relaciona cargos con documentos requeridos.

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
Cuenta de acceso de una organización, opcionalmente vinculada con Employee.

### SystemRole y UserRole
Roles iniciales SUPER_ADMIN y BRANCH_ADMIN. Mantener separación del cargo.

### UserBranchAccess
Sucursales autorizadas para administradores de sucursal.

### RefreshSession
Hash del token, familia, expiración, revocación, reemplazo, IP y user agent.

Debe pertenecer exactamente a un `PlatformUser` o un `UserAccount`. La base aplica un check constraint tanto sobre las claves como sobre `account_type`.

### AccountToken
Token de verificación o restablecimiento. Guarda únicamente SHA-256, propósito, expiración, consumo y revocación. También pertenece exactamente a un tipo de cuenta.

### SecurityAuditEvent
Evento persistente para operaciones sensibles. No contiene correo, token ni secreto.

## Restricciones mínimas

- Employee único por organización, tipo y número de documento.
- JobPosition único por organización y nombre normalizado.
- Email normalizado globalmente único entre `PlatformUser` y `UserAccount`; EF protege cada tabla y Application protege el conjunto.
- Relaciones y foreign keys deben impedir referencias cruzadas de tenant.
- Índices para `organization_id` combinado con filtros frecuentes.

## Migración inicial

`InitialIdentityAndAuthentication` crea la fundación de identidad. Las migraciones se aplican de forma explícita antes de iniciar la API; el startup no modifica el esquema.

## Eliminación

No aplicar soft delete universal. Cada entidad define si usa desactivación, cierre, reemplazo o anulación. Los expedientes y registros históricos no se eliminan normalmente.
