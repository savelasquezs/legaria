# AGENTS.md

## Propósito

Este archivo define cómo deben trabajar Codex y otros agentes dentro del repositorio. Sus reglas son obligatorias salvo instrucción explícita del propietario del proyecto.

## Principio rector

Construir la solución más sencilla que cumpla los requerimientos aprobados, sea segura, fácil de mantener y visualmente profesional. No agregar abstracciones, módulos, entidades, permisos, estados, integraciones ni opciones que no hayan sido solicitados o documentados.

## Antes de modificar código

1. Leer este archivo, `BUSINESS_RULES.md` y los documentos relacionados con la tarea.
2. Revisar el código existente antes de proponer una estructura nueva.
3. Identificar qué componentes, servicios, validaciones, estilos y pruebas pueden reutilizarse.
4. Confirmar el alcance real. Resolver detalles menores con decisiones conservadoras; no inventar requisitos materiales.
5. Revisar el impacto multitenant, de autorización, de sucursal y sobre el propio expediente del administrador.
6. Si la tarea contradice una regla documentada, detener la implementación y señalar la contradicción.

## Forma de trabajar

- Hacer cambios pequeños, coherentes y completos.
- Priorizar claridad sobre patrones sofisticados.
- Mantener los nombres del glosario.
- No reescribir áreas no relacionadas.
- No introducir dependencias sin justificar su necesidad.
- No crear capas, interfaces o wrappers de una sola implementación cuando no aporten aislamiento, pruebas o un contrato útil.
- Sí usar interfaces en límites importantes: servicios externos, almacenamiento, reloj, usuario actual, autenticación, repositorios cuando el patrón ya esté adoptado.
- No duplicar lógica de negocio entre controlador, handler, servicio y frontend.
- La seguridad y las reglas de negocio se aplican en backend aunque también exista validación de interfaz.

## Flujo obligatorio para cada tarea

1. Inspeccionar.
2. Describir brevemente el enfoque.
3. Implementar el cambio mínimo completo.
4. Agregar o actualizar pruebas.
5. Ejecutar formateo o lint del área modificada.
6. Compilar frontend y backend cuando corresponda.
7. Ejecutar las pruebas relevantes.
8. Corregir cualquier error introducido.
9. Revisar el diff para detectar duplicación, secretos, código muerto y cambios accidentales.
10. Resumir archivos cambiados, comportamiento, pruebas y cualquier limitación real.

No se debe declarar una tarea terminada si el código modificado no compila o sus pruebas relevantes fallan, salvo bloqueo externo claramente demostrado.

## Cambios importantes

Se considera cambio importante cualquiera que afecte:

- autenticación o autorización;
- modelo de datos o migraciones;
- reglas multitenant;
- permisos por sucursal;
- expedientes o documentos;
- flujo principal de una pantalla;
- contratos públicos de API;
- componentes compartidos;
- dependencias o configuración de despliegue.

Después de cada cambio importante se deben crear pruebas y ejecutar, como mínimo, compilación y suite relevante.

## Backend

### Arquitectura

- ASP.NET Core Web API.
- Capas: API, Application, Domain e Infrastructure.
- Organizar Application por feature, no por carpetas globales gigantes.
- Los controladores deben ser delgados: recibir, autorizar, delegar y responder.
- La lógica de negocio vive en Application o Domain según su naturaleza.
- Infrastructure implementa persistencia y proveedores externos.
- Domain no depende de Infrastructure ni API.

### Reglas de implementación

- Usar operaciones asíncronas y `CancellationToken` en I/O.
- Usar `Guid` para los identificadores del modelo.
- Validar entrada con un mecanismo consistente.
- Usar fechas UTC en persistencia; convertir solo en bordes de presentación.
- Nunca confiar en `organizationId`, roles, sucursales o identidad enviados por el frontend.
- Consultar siempre por identificador y `OrganizationId`.
- Evitar consultas N+1 y cargas completas innecesarias.
- No exponer entidades de EF directamente en la API.
- No registrar contraseñas, tokens, documentos sensibles ni datos médicos completos en logs.
- Tratar `PlatformUser` y `UserAccount` como tipos distintos: una cuenta de plataforma nunca tiene `organization_id`.
- Mantener la unicidad global de correo entre ambos tipos de cuenta en los casos de uso.
- No ocultar excepciones con `catch` vacío.
- Responder 401 para falta o invalidez de autenticación y 403 para autenticación válida sin permiso.

### Nuevas features

Cada feature nueva debe contener únicamente lo necesario. Una estructura típica puede incluir:

```text
Features/Employees/
  Commands/
  Queries/
  DTOs/
  Validators/
```

No crear todas las carpetas si permanecerán vacías.

## Frontend

### Objetivo visual

Toda interfaz debe verse terminada, consistente y agradable; no basta con que funcione. Debe ser responsive, clara, accesible y apropiada para uso diario administrativo.

### Antes de crear una pantalla

1. Revisar componentes reutilizables existentes.
2. Identificar patrones compartidos: encabezado, búsqueda, filtros, tabla, formulario, confirmación, carga, error y estado vacío.
3. Crear componentes reutilizables cuando exista uso real inmediato o una repetición clara; no crear un sistema de diseño teórico antes de necesitarlo.
4. Mantener estilos, espaciado, tipografía, iconografía y comportamiento consistentes.

### Componentes base prioritarios

Al surgir su primer uso real, priorizar componentes como:

- `AppDialog`: modal base consistente.
- `ConfirmDialog`: confirmación de acciones sensibles.
- `FormDialog`: modal para formularios cortos o medianos.
- `AppTooltip`: ayuda breve en controles no evidentes.
- `PageHeader`: título, descripción y acciones principales.
- `SearchField`: búsqueda con debounce cuando aplique.
- `EmptyState`, `ErrorState`, `LoadingSkeleton`.
- `StatusChip`.
- `AppDataTable` solo cuando exista un patrón compartido real.

No convertir cada botón o etiqueta en un componente. Reutilizar Quasar directamente cuando ya resuelva el problema de forma consistente.

### Modales

- Usar modal para tareas enfocadas que no justifican una ruta completa.
- No encerrar formularios largos o complejos en modales pequeños.
- El modal debe tener título, explicación cuando haga falta, acciones claras y manejo de carga.
- Cerrar con Escape cuando sea seguro.
- Confirmar antes de descartar cambios no guardados si existe pérdida real.
- Las acciones destructivas deben diferenciarse visualmente y requerir confirmación.

### Tooltips

- Usarlos para íconos sin texto, abreviaturas o acciones cuyo significado no sea evidente.
- No usarlos para esconder información esencial.
- El texto debe ser corto y específico.
- Todo control debe seguir siendo entendible mediante teclado y lectores de pantalla cuando aplique.

### Formularios

- Etiquetas claras y mensajes de error junto al campo.
- Valores iniciales explícitos.
- Deshabilitar envíos duplicados.
- Conservar datos cuando falle el servidor.
- No depender únicamente del color para comunicar estado.
- No mostrar campos que el usuario no puede usar.
- No inventar opciones, estados ni datos de ejemplo como si fueran requerimientos.

### Tablas y listas

- Mostrar carga, error y estado vacío.
- Búsqueda, filtros, ordenamiento y paginación solo si el volumen o requerimiento lo justifica.
- Acciones por fila consistentes y con tooltip.
- Evitar tablas inmanejables en móvil; usar diseño adaptable.
- No ocultar permisos en frontend como única medida de seguridad.

### Estado y API

- Pinia para estado compartido real; estado local para comportamiento local.
- Servicios API tipados.
- Interceptor único para access token y renovación de sesión.
- No guardar refresh token en `localStorage` o `sessionStorage`.
- No duplicar respuestas del servidor en varios stores sin necesidad.

## Diseño sin invención

Codex debe basarse en requerimientos, modelo aprobado y patrones existentes. Puede mejorar jerarquía visual, espaciado, responsive y accesibilidad, pero no debe inventar:

- campos de negocio;
- estadísticas;
- estados;
- permisos;
- flujos de aprobación;
- botones sin acción definida;
- datos falsos permanentes;
- automatizaciones no solicitadas.

Si una interfaz necesita contenido aún no definido, usar un estado vacío honesto o un placeholder claramente temporal.

## Seguridad

- Leer `docs/SECURITY.md` antes de tocar autenticación, autorización o datos sensibles.
- Los secretos nunca se guardan en el repositorio.
- No aplicar migraciones automáticamente en producción; ejecutarlas antes del arranque.
- No enviar correo real desde pruebas. Sustituir siempre `IEmailSender`.
- No registrar correos, tokens ni secretos en `SecurityAuditEvent`.
- No desactivar validación TLS, autorización, CORS o protección CSRF para “hacer que funcione”.
- Toda consulta de negocio debe quedar limitada por organización.
- El administrador de sucursal no puede consultar ni modificar su propio expediente.
- Los permisos se verifican en backend sobre el recurso real.

## Pruebas

- Leer `docs/TESTING.md`.
- Cada bug corregido debe tener una prueba que reproduzca el fallo cuando sea viable.
- Cada feature debe cubrir caso exitoso y al menos un caso de rechazo importante.
- Las reglas de aislamiento multitenant y autorización requieren pruebas de integración.
- En frontend, probar comportamiento visible y accesible, no detalles internos frágiles.

## Compilación y verificación

Después de cambios backend:

```bash
dotnet build backend/Legaria.sln
dotnet test backend/Legaria.sln
```

Después de cambios frontend:

```bash
cd frontend
npm run lint
npm run test
npm run build
```

Si los comandos aún no existen, Codex debe indicarlo con precisión y no fingir que fueron ejecutados.

## Documentación

Actualizar documentación cuando cambie una regla, contrato público, decisión arquitectónica, seguridad o flujo importante. Registrar decisiones duraderas en `DECISIONS.md`, no simples detalles de implementación.

## Prohibiciones

- No hacer sobreingeniería.
- No introducir microservicios.
- No crear event sourcing, buses, colas, Redis, GraphQL o motores de reglas sin requerimiento aprobado.
- No agregar soft delete universal por defecto.
- No crear repositorios genéricos que oculten consultas importantes.
- No guardar tokens o contraseñas en texto plano.
- No usar `organizationId` del request como fuente de seguridad.
- No modificar el esquema sin migración y pruebas.
- No dejar `TODO` para partes necesarias de la tarea sin explicarlo.
- No finalizar con pruebas fallando.
