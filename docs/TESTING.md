# Estrategia de pruebas

## Regla obligatoria

Después de cada cambio importante, crear o actualizar pruebas y ejecutar compilación. No cerrar una tarea con fallos introducidos.

## Pirámide práctica

- Unitarias para reglas puras, validadores y servicios sin infraestructura.
- Integración para EF Core, autenticación, autorización, endpoints y aislamiento multitenant.
- Frontend para comportamiento de componentes, formularios y flujos críticos.
- E2E únicamente para recorridos de alto valor cuando la aplicación ya tenga estabilidad.

## Backend

Los ciclos laborales deben cubrir transiciones y cierres sin solapamiento, aislamiento tenant y el retiro de un administrador con suspensión y revocación de acceso.

### Unitarias

Cubrir:

- caso exitoso;
- validación principal;
- regla de negocio negativa;
- límites de fechas o estados cuando apliquen.

### Integración

Obligatorias para:

- bootstrap único y correo prevalidado;
- login diferenciado de plataforma y tenant;
- suspensión, correo sin verificar y lockout;
- claims y ausencia de contexto tenant en JWT de plataforma;
- login, refresh, rotación y logout;
- token expirado, revocado o reutilizado;
- restablecimiento con cambio de stamp, revocación de sesiones y auditoría;
- 401 versus 403;
- acceso entre dos organizaciones;
- acceso de administrador a sucursal no asignada;
- intento del BRANCH_ADMIN de consultar su propio expediente;
- consultas por ID que no incluyan recursos de otro tenant;
- migraciones importantes.

Usar PostgreSQL real efímero mediante Testcontainers; no confiar únicamente en EF InMemory para comportamiento relacional. La máquina de desarrollo o CI debe disponer de un motor Docker operativo.

## Frontend

Probar:

- renderizado de carga, error y vacío;
- validaciones visibles;
- modal abre, confirma y cancela;
- submit evita duplicación;
- permisos ocultan o deshabilitan acciones según contrato;
- renovación de sesión coordina solicitudes concurrentes;
- formularios conservan datos tras error.
- guards separan rutas `PLATFORM` y `TENANT`;
- el access token permanece solo en memoria.

Evitar pruebas atadas a clases CSS internas o estructura irrelevante.

## Bugs

Todo bug corregido debe incluir una prueba de regresión cuando sea técnicamente viable. La prueba debe fallar antes de la corrección y pasar después.

## Comandos

```bash
dotnet build backend/Legaria.sln
dotnet test backend/Legaria.sln
dotnet ef migrations script --idempotent --project backend/src/Legaria.Infrastructure --startup-project backend/src/Legaria.API

cd frontend
npm run lint
npm run test
npm run build
```

Ejecutar suite completa antes de integrar cambios amplios. Para cambios pequeños puede ejecutarse primero la suite focalizada, pero la compilación del proyecto afectado sigue siendo obligatoria.

## Evidencia en la respuesta de Codex

Codex debe indicar:

- comandos ejecutados;
- resultados reales;
- pruebas añadidas;
- verificaciones que no pudo ejecutar y motivo.

Nunca afirmar que compiló o pasó pruebas sin haber ejecutado los comandos.
