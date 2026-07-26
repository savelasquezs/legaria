# Flujo de desarrollo

## Inicio de tarea

1. Leer documentación relevante.
2. Revisar estado de git.
3. Buscar implementaciones y componentes relacionados.
4. Definir alcance mínimo.
5. Identificar pruebas necesarias antes de modificar.

## Implementación

- Cambiar una feature por vez.
- Mantener backend, frontend y contrato alineados.
- Crear migración cuando cambia el esquema.
- No mezclar refactors grandes con una feature salvo necesidad directa.

## Verificación

Backend:

```bash
dotnet format backend/Legaria.sln --verify-no-changes
dotnet build backend/Legaria.sln
dotnet test backend/Legaria.sln
```

Frontend:

```bash
cd frontend
npm run lint
npm run test
npm run build
```

Revisar además:

- diff completo;
- secretos o credenciales;
- datos de otro tenant;
- accesibilidad básica;
- estados de carga, vacío y error;
- documentación afectada.

## Commits

Commits pequeños con mensaje imperativo y descriptivo. No incluir archivos generados, secretos, bases locales ni dependencias descargadas.

## Definición de terminado

- Requerimiento implementado sin extras inventados.
- UI completa y coherente.
- Reglas de negocio aplicadas en backend.
- Pruebas agregadas y pasando.
- Compilación exitosa.
- Documentación actualizada si cambió una decisión o contrato.
