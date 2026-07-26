# Legaria

Legaria es un SaaS multitenant para administrar trabajadores, sucursales, documentos, expedientes laborales y autenticación segura.

## Estado inicial

Este repositorio inicia con la estructura base y la documentación que gobierna el trabajo de desarrollo. No contiene funcionalidades inventadas ni módulos fuera del alcance aprobado.

## Tecnologías objetivo

- Backend: ASP.NET Core Web API (.NET 8), Entity Framework Core y PostgreSQL.
- Frontend: Vue 3, TypeScript, Quasar, Pinia y Axios.
- Archivos: Firebase Storage o un proveedor equivalente configurado por infraestructura.
- Autenticación: JWT de corta duración y refresh token rotatorio en cookie HttpOnly.

## Estructura

```text
frontend/                 Aplicación web Vue + Quasar
backend/                  Solución .NET y API
  src/
    Legaria.API/
    Legaria.Application/
    Legaria.Domain/
    Legaria.Infrastructure/
  tests/
    Legaria.UnitTests/
    Legaria.IntegrationTests/
docs/                     Documentación funcional y técnica
AGENTS.md                  Reglas obligatorias para Codex y otros agentes
BUSINESS_RULES.md          Reglas de negocio aprobadas
DECISIONS.md               Registro de decisiones del proyecto
```

## Lectura obligatoria para agentes

1. `AGENTS.md`
2. `BUSINESS_RULES.md`
3. `docs/ARCHITECTURE.md`
4. `docs/SECURITY.md`
5. `docs/UI_GUIDELINES.md`
6. `docs/TESTING.md`
7. El documento específico del módulo que se vaya a modificar.

## Inicio esperado

El primer incremento funcional será autenticación y autorización segura. Antes de implementarlo, Codex debe comprobar que el modelo y las reglas siguen coincidiendo con la documentación.

## Comandos futuros

```bash
# Backend
dotnet restore backend/Legaria.sln
dotnet build backend/Legaria.sln
dotnet test backend/Legaria.sln

# Frontend
cd frontend
npm install
npm run lint
npm run test
npm run build
```

Los comandos se habilitarán cuando se creen los proyectos reales. No deben simularse resultados mientras las aplicaciones estén vacías.
