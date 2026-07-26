# Estrategia de pruebas

## Regla obligatoria

Después de cada cambio importante, crear o actualizar pruebas y ejecutar compilación. No cerrar una tarea con fallos introducidos.

## Pirámide práctica

- Unitarias para reglas puras, validadores y servicios sin infraestructura.
- Integración para EF Core, autenticación, autorización, endpoints y aislamiento multitenant.
- Frontend para comportamiento de componentes, formularios y flujos críticos.
- E2E únicamente para recorridos de alto valor cuando la aplicación ya tenga estabilidad.

## Backend

### Unitarias

Cubrir:

- caso exitoso;
- validación principal;
- regla de negocio negativa;
- límites de fechas o estados cuando apliquen.

### Integración

Obligatorias para:

- login, refresh, rotación y logout;
- token expirado, revocado o reutilizado;
- 401 versus 403;
- acceso entre dos organizaciones;
- acceso de administrador a sucursal no asignada;
- intento del BRANCH_ADMIN de consultar su propio expediente;
- consultas por ID que no incluyan recursos de otro tenant;
- migraciones importantes.

Usar una base PostgreSQL real efímera o Testcontainers cuando se implemente; no confiar únicamente en EF InMemory para comportamiento relacional.

## Frontend

Probar:

- renderizado de carga, error y vacío;
- validaciones visibles;
- modal abre, confirma y cancela;
- submit evita duplicación;
- permisos ocultan o deshabilitan acciones según contrato;
- renovación de sesión coordina solicitudes concurrentes;
- formularios conservan datos tras error.

Evitar pruebas atadas a clases CSS internas o estructura irrelevante.

## Bugs

Todo bug corregido debe incluir una prueba de regresión cuando sea técnicamente viable. La prueba debe fallar antes de la corrección y pasar después.

## Comandos

```bash
dotnet build backend/GestionPersonal.sln
dotnet test backend/GestionPersonal.sln

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
