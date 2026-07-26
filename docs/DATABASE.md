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

### UserAccount
Cuenta de acceso de una organización, opcionalmente vinculada con Employee.

### SystemRole y UserRole
Roles iniciales SUPER_ADMIN y BRANCH_ADMIN. Mantener separación del cargo.

### UserBranchAccess
Sucursales autorizadas para administradores de sucursal.

### RefreshSession
Hash del token, familia, expiración, revocación, reemplazo, IP y user agent.

## Restricciones mínimas

- Employee único por organización, tipo y número de documento.
- JobPosition único por organización y nombre normalizado.
- Email de UserAccount único según la estrategia de login que se defina; la primera versión debe decidir si es global o por organización.
- Relaciones y foreign keys deben impedir referencias cruzadas de tenant.
- Índices para `organization_id` combinado con filtros frecuentes.

## Eliminación

No aplicar soft delete universal. Cada entidad define si usa desactivación, cierre, reemplazo o anulación. Los expedientes y registros históricos no se eliminan normalmente.
