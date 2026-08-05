# Reglas de negocio

## Alcance

Este documento contiene las reglas aprobadas para el sistema de gestión de personal. Codex no debe completar vacíos con supuestos de negocio. Las reglas nuevas se agregan cuando el propietario las aprueba.

## Multitenancy

1. Cada organización es un tenant aislado.
2. Los datos de una organización nunca pueden consultarse o modificarse desde otra.
3. `organization_id` se obtiene de la identidad autenticada, no del frontend.
4. Todas las entidades de negocio que pertenezcan a una organización deben conservar `organization_id`.
5. Suspender una organización bloquea el acceso sin eliminar sus datos.

## Organizaciones y sucursales

1. El NIT de una organización es globalmente único, se almacena sin puntuación y separado de su dígito de verificación.
2. El NIT admite de 6 a 14 dígitos y el DV se valida con módulo 11.
3. La ubicación se selecciona del catálogo DIVIPOLA versionado; la organización guarda el código de municipio y el departamento se deriva de este.
4. `OWNER` y `PLATFORM_ADMIN` pueden listar, crear, consultar y editar organizaciones, incluido NIT/DV.
5. Solo `OWNER` puede suspender o reactivar una organización.
6. Suspender una organización bloquea inmediatamente login, refresh y JWT tenant, sin eliminar datos ni sesiones; reactivarla recupera el acceso.
7. Una organización puede tener múltiples sucursales.
8. Una sucursal puede desactivarse sin borrar su historial.
9. Un trabajador puede tener asignaciones activas en varias sucursales.
10. Una asignación puede marcarse como principal.
11. Solo puede existir una asignación principal activa por relación laboral.
12. El nombre de una sucursal es único dentro de su organización después de normalizar espacios y mayúsculas/minúsculas.
13. Una sucursal conserva dirección y municipio DIVIPOLA obligatorios; correo y teléfono de contacto son opcionales.
14. Desactivar una sucursal conserva sus datos y accesos. Los accesos vuelven a ser efectivos si la sucursal se reactiva.
15. Una sucursal inactiva no puede asignarse en una operación nueva.
16. Después de crear una organización, `OWNER` o `PLATFORM_ADMIN` pueden crear su primera sucursal únicamente mientras la organización no tenga sucursales.
17. Omitir o fallar la creación guiada de la primera sucursal no revierte la organización ni su superadministrador inicial.
18. Las sucursales posteriores se crean desde el contexto tenant por un `SUPER_ADMIN`.

## Aprovisionamiento del primer superadministrador

1. Crear una organización crea en la misma transacción una cuenta tenant no verificada con el único rol `SUPER_ADMIN`.
2. El primer superadministrador no crea automáticamente un `Employee`; `employee_id` permanece nulo.
3. La API nunca recibe, genera para mostrar ni devuelve una contraseña conocida del administrador inicial.
4. La cuenta recibe un hash de un valor aleatorio descartado y se activa mediante invitación de 24 horas.
5. Aceptar la invitación define la contraseña, verifica el correo, rota `security_stamp` y consume el token una sola vez.
6. Reenviar revoca inmediatamente todas las invitaciones activas anteriores.
7. Mientras la cuenta no haya aceptado, `OWNER` o `PLATFORM_ADMIN` pueden corregir nombre, apellido o correo; la corrección genera una invitación nueva.
8. Si Resend falla, la organización y la cuenta permanecen creadas con estado público `DELIVERY_FAILED` y se permite reenviar.
9. El correo de contacto de la empresa no es único y puede coincidir con el correo del administrador.

## Trabajadores y usuarios

1. `Employee` representa a la persona.
2. Un trabajador puede existir sin cuenta de usuario.
3. Un usuario puede estar vinculado opcionalmente con un trabajador.
4. Los administradores también son trabajadores cuando tienen relación laboral con la organización.
5. Un trabajador no se duplica para una recontratación.
6. La combinación organización, tipo y número de documento debe ser única.

## Roles y alcance

Roles iniciales:

- `SUPER_ADMIN`: administra toda la organización.
- `BRANCH_ADMIN`: administra únicamente las sucursales asignadas.

Reglas:

1. El rol del sistema es independiente del cargo laboral.
2. Un `BRANCH_ADMIN` puede tener acceso a una o varias sucursales.
3. Un `SUPER_ADMIN` tiene acceso a todas las sucursales de su organización.
4. Tener acceso a una sucursal no autoriza datos de otra organización.
5. El frontend puede ocultar acciones, pero el backend siempre valida el permiso.
6. `SUPER_ADMIN` crea, edita, desactiva y reactiva sucursales, e invita y administra cuentas `BRANCH_ADMIN` de su organización.
7. `BRANCH_ADMIN` solo consulta las sucursales con una concesión activa; las concesiones no se incluyen en el JWT.
8. Un administrador activo debe conservar al menos una sucursal activa al crear o modificar sus accesos. La desactivación posterior de una sucursal no elimina la concesión histórica.
9. Todo `BRANCH_ADMIN` nuevo debe estar vinculado a un `Employee`. Invitarlo crea cuenta no verificada, único rol `BRANCH_ADMIN`, reserva global de correo, accesos e invitación en una transacción.
10. Mientras la invitación siga pendiente, `SUPER_ADMIN` puede corregir identidad y sucursales; la corrección y el reenvío revocan invitaciones anteriores.
11. Suspender un `BRANCH_ADMIN` rota su `security_stamp`, revoca sesiones refresh e invitaciones pendientes y bloquea login, refresh y JWT. Reactivar no restaura las sesiones revocadas.
12. Los accesos por sucursal conservan fecha y actor de concesión y revocación; volver a conceder crea un registro histórico nuevo.

## Restricción sobre el propio expediente

1. Un `BRANCH_ADMIN` no puede consultar su propio expediente laboral.
2. Un `BRANCH_ADMIN` no puede modificar su propio expediente laboral.
3. La restricción aplica a datos personales, contratos, documentos, incapacidades, permisos, vacaciones, procesos disciplinarios, dotación, comentarios y adjuntos del expediente.
4. `SUPER_ADMIN` sí puede gestionar el expediente del administrador.
5. La validación se realiza en backend usando el `employee_id` vinculado a la cuenta.
6. Un `BRANCH_ADMIN` puede consultar trabajadores con asignación activa en sus sucursales autorizadas, excepto su propio expediente.
7. La consulta de detalle para `BRANCH_ADMIN` solo incluye asignaciones de sus sucursales autorizadas y no expone la administración de cuentas.

## Relación laboral y asignaciones

1. `EmploymentRelationship` representa cada vínculo laboral.
2. Una recontratación crea una nueva relación laboral para el mismo trabajador.
3. Finalizar una relación laboral no elimina su información.
4. `EmployeeAssignment` representa sucursal y cargo durante un periodo.
5. Cambiar de cargo o sucursal debe conservar historial: se cierra la asignación anterior y se crea otra cuando corresponda.
6. Pueden existir varias asignaciones simultáneas si el trabajador presta servicios en varias sucursales.
7. Crear un trabajador desde una sucursal crea su primera relación laboral y asignación activa; asignarlo después a otra sucursal reutiliza la relación activa.
8. Solo puede existir una cuenta tenant vinculada a un mismo trabajador.

9. No se permiten relaciones laborales ni asignaciones con fecha de inicio futura.
10. Finalizar una relación laboral cierra todas sus asignaciones activas en la misma fecha.
11. Si el trabajador tiene una cuenta `BRANCH_ADMIN` activa, finalizar la relación la suspende y revoca sus sesiones e invitaciones; los accesos por sucursal permanecen como historial.
12. Una recontratación no reactiva automáticamente una cuenta suspendida.
13. Una transición de cargo o sucursal termina la asignación anterior el día previo a la fecha efectiva y no permite periodos solapados en la misma sucursal.

## Cargos

1. Los cargos pertenecen a la organización.
2. Un cargo puede utilizarse en varias sucursales.
3. Los cargos con historial no se eliminan; se desactivan.
4. Los nombres de cargos deben ser únicos dentro de la organización, salvo decisión posterior.

5. Un cargo inactivo conserva su historial y no puede utilizarse en asignaciones nuevas.
6. Renombrar un cargo corrige el nombre del catálogo y no crea una asignación nueva.

## Documentos del trabajador

### Catálogo organizacional

1. Cada organización administra su propio catálogo vacío de categorías y tipos de documento.
2. Una categoría tiene alcance inmutable `EMPLOYEE` o `BRANCH`; sus tipos heredan ese alcance.
3. El nombre de categoría es único por organización y alcance, y el nombre de tipo es único dentro de su categoría, normalizando espacios y mayúsculas/minúsculas.
4. Categorías y tipos se desactivan o reactivan; no se eliminan físicamente.
5. Una categoría inactiva hace indisponibles sus tipos para usos nuevos sin modificar el estado propio de estos.
6. Un tipo solo puede moverse a una categoría activa del mismo alcance.
7. Cada tipo define obligatoriedad por defecto, modo de fecha de expedición y vencimiento, versiones vigentes, cantidad de evidencias y evidencias permitidas `PDF`, `IMAGE`, `VIDEO` o `LINK`.
8. Los modos de fecha son `NEVER`, `OPTIONAL` o `REQUIRED` y debe existir al menos una evidencia permitida.
9. `SUPER_ADMIN` administra ambos alcances. `BRANCH_ADMIN` consulta todo el catálogo, pero solo modifica el alcance `BRANCH`.
10. El catálogo se ordena alfabéticamente y no contiene plantillas ni registros precargados.

1. Existen documentos aplicables a todos los trabajadores, a cargos específicos u opcionales.
2. Los requisitos por cargo se definen mediante una relación explícita.
3. La cédula y otros documentos personales pertenecen al trabajador, no a una asignación.
4. Un documento puede asociarse opcionalmente a una asignación cuando su vigencia dependa del cargo o sucursal.
5. Reemplazar un documento conserva la versión anterior como historial.
6. Los estados de vigencia, próximo a vencer y vencido se calculan a partir de fechas; no se editan manualmente.
7. El archivo debe almacenarse de forma privada y entregarse mediante acceso autorizado.
8. Los requisitos documentales de un cargo solo pueden incluir tipos activos de categorías activas con alcance `EMPLOYEE` dentro de la misma organización.
9. La configuración de requisitos de un cargo es reemplazable y puede quedar vacía; quitar una selección no elimina ni desactiva el tipo documental.
10. Los requisitos efectivos futuros serán la unión entre los tipos obligatorios por defecto para todos los trabajadores y los tipos seleccionados explícitamente para el cargo. Esta fase solo configura la relación y no calcula cumplimiento.
11. Los requisitos efectivos de un trabajador activo son la unión de los tipos obligatorios por defecto y los exigidos por sus cargos activos visibles para el administrador.
12. Un requisito se considera pendiente cuando no existe una versión vigente sin reemplazar o cuando todas sus versiones vigentes están vencidas.
13. Los próximos vencimientos incluyen documentos que vencen entre la fecha actual y dos meses calendario después.
14. Los archivos se almacenan de forma privada en Firebase Storage con nombres internos aleatorios; nunca se persisten URLs públicas ni tokens de descarga.
15. La carga admite PDF, JPEG, PNG, WebP, MP4 y WebM validados por contenido, además de enlaces HTTPS cuando el tipo los permita. El límite total de la solicitud es 105 MB y cada archivo no puede superar 100 MB.

## Expedientes

1. `EmployeeCase` es el núcleo común de los expedientes laborales.
2. Todo expediente pertenece a un trabajador y una organización.
3. Puede relacionarse con una relación laboral y sucursal cuando aplique.
4. Cada expediente tiene un tipo.
5. Cada tipo puede tener múltiples expedientes.
6. Todos los tipos de expediente pueden tener comentarios.
7. Todos los tipos pueden tener documentos adjuntos cuando sea necesario.
8. Los datos específicos de cada tipo se guardan en su tabla especializada; no en una tabla gigante.
9. Los expedientes no se eliminan físicamente como operación normal. Se anulan o cierran según la regla de cada módulo cuando esa regla sea definida.

## Tipos iniciales de expediente

- Contrato.
- Incapacidad.
- Permiso.
- Vacaciones.
- Proceso disciplinario.
- Entrega de dotación.

No agregar tipos nuevos sin aprobación.

## Comentarios de expediente

1. Un expediente puede tener cero o muchos comentarios.
2. Cada comentario registra autor y fecha.
3. Los comentarios no sustituyen evidencia o documentos formales.
4. La edición o eliminación de comentarios deberá definirse antes de implementarse; inicialmente se recomienda conservar historial.
5. Un usuario solo puede comentar expedientes que está autorizado a consultar.

## Contratos

Las reglas definitivas de renovación, firma, modificación y terminación aún no están cerradas. Hasta que se aprueben:

- No asumir que una renovación modifica el contrato original.
- No implementar cálculos de nómina.
- No inventar tipos de contrato o flujos de firma.

## Incapacidades, permisos y vacaciones

1. No implementar flujos de aprobación múltiples sin aprobación explícita.
2. La información médica debe tener acceso restringido.
3. El cálculo de vacaciones deberá respetar calendario colombiano cuando se implemente.
4. El sistema administra novedades; no liquida nómina en la primera versión.

## Procesos disciplinarios y dotación

1. No inventar etapas disciplinarias hasta que el flujo sea aprobado.
2. La evidencia debe conservar origen, autor y fecha.
3. La entrega de dotación puede contener varios elementos.
4. La firma o aceptación debe distinguirse del archivo entregado.

## Autenticación y sesiones

1. Existen cuentas globales de plataforma (`PlatformUser`) y cuentas de organización (`UserAccount`).
2. Los correos son globalmente únicos entre ambos tipos de cuenta.
3. Una cuenta de plataforma nunca pertenece a una organización ni lleva contexto tenant.
4. El primer `OWNER` se crea por bootstrap y su correo queda prevalidado.
5. Las contraseñas tienen entre 8 y 128 caracteres, sin reglas artificiales de composición.
6. Access token JWT de 10 minutos.
7. Refresh token rotatorio de siete días en cookie host-only, HttpOnly, Secure y `SameSite=Lax`.
8. El refresh token no se guarda en `localStorage`.
9. La base de datos almacena únicamente hash del refresh token.
10. Reutilizar un refresh token ya rotado revoca su familia de sesión.
11. Cambiar contraseña, desactivar usuario o revocar sesiones invalida accesos anteriores mediante `security_stamp`.
12. Los mensajes de login, recuperación y reenvío no deben permitir enumerar cuentas.
13. Las credenciales, correos y tokens nunca aparecen en auditoría ni logs de seguridad.
14. Los tokens de verificación duran 24 horas y los de restablecimiento 30 minutos; son de uso único y solo se persiste su SHA-256.
15. Cinco fallos de login bloquean la cuenta durante 15 minutos.
16. `account_emails` reserva de forma central y transaccional el correo normalizado de toda cuenta de plataforma o tenant.
17. Las invitaciones tenant almacenan únicamente SHA-256 del token y exponen `PENDING_DELIVERY`, `SENT`, `DELIVERY_FAILED`, `EXPIRED`, `ACCEPTED` o `REVOKED`.
