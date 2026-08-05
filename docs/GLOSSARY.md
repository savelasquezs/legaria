# Glosario

- **Organization:** empresa cliente y tenant.
- **Branch:** sucursal de la organización.
- **Employee:** persona trabajadora.
- **EmploymentRelationship:** vínculo laboral de un Employee con la organización durante un periodo.
- **EmployeeAssignment:** asignación a una sucursal y cargo.
- **JobPosition:** cargo laboral creado por la organización.
- **DocumentCategory:** categoría organizacional de tipos documentales con alcance de trabajador o sucursal.
- **DocumentType:** plantilla de requisitos y evidencias para un documento futuro.
- **JobPositionDocumentRequirement:** tipo documental exigido explícitamente para un cargo.
- **UserAccount:** cuenta con acceso al sistema.
- **PlatformUser:** cuenta global de operación de Legaria; no pertenece a una organización.
- **AccountType:** discriminador `PLATFORM` o `TENANT` usado en identidad y sesiones.
- **OWNER:** propietario global creado únicamente por bootstrap.
- **PLATFORM_ADMIN:** administrador global de plataforma.
- **SystemRole:** rol técnico de autorización; no es un cargo laboral.
- **UserBranchAccess:** sucursal que un usuario puede administrar.
- **EmployeeCase:** expediente laboral genérico.
- **CaseType:** tipo del expediente.
- **CaseComment:** comentario administrativo asociado al expediente.
- **CaseDocument:** archivo adjunto a un expediente.
- **EmployeeDocument:** documento propio del trabajador.
- **EmployeeDocumentEvidence:** archivo privado o enlace que sustenta una versión documental.
- **RefreshSession:** sesión persistente representada por un refresh token hasheado.
- **AccountToken:** token de verificación o restablecimiento almacenado únicamente como hash.
- **SecurityAuditEvent:** registro persistente de un evento de seguridad sin correo, token ni secreto.
- **SecurityStamp:** valor que invalida JWT y sesiones anteriores cuando cambia la seguridad de una cuenta.
- **SUPER_ADMIN:** usuario con alcance sobre todas las sucursales de su organización.
- **BRANCH_ADMIN:** usuario limitado a sucursales asignadas y sin acceso a su propio expediente.

Usar estos términos en código y documentación. No alternar con Worker, Staff, Record, File o TenantAccount sin una decisión registrada.
