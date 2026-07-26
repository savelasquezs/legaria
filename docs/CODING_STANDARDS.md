# Estándares de código

## General

- Código legible antes que ingenioso.
- Nombres del dominio en inglés y textos de UI en español, salvo decisión distinta.
- Una responsabilidad clara por clase o componente, sin fragmentación excesiva.
- Eliminar código muerto.
- Comentarios explican el porqué, no repiten el código.
- Formato automático consistente.

## C#

- Nullable reference types habilitado.
- `async` hasta el borde de I/O.
- `CancellationToken` en operaciones asíncronas públicas de Application e Infrastructure.
- Records para DTO inmutables cuando sean apropiados.
- Entidades con setters controlados según necesidad, sin convertir todo en ceremony DDD.
- Excepciones de negocio consistentes y middleware central.
- No devolver `null` ambiguo cuando se requiere distinguir no encontrado, inválido o prohibido.
- Consultas EF con proyección a DTO y `AsNoTracking` para lecturas.
- Configuraciones de entidades separadas cuando crezcan.

## TypeScript y Vue

- TypeScript estricto.
- Composition API y `<script setup>`.
- Props y emits tipados.
- Evitar `any`; usar `unknown` y validar.
- Lógica reutilizable en composables cuando exista reutilización real.
- Servicios API separados de componentes.
- Componentes no deben conocer detalles de almacenamiento de tokens.
- No colocar lógica compleja en templates.
- Variables derivadas mediante `computed`.

## Naming

- C#: PascalCase para tipos y miembros públicos; camelCase para locales y parámetros.
- TypeScript: PascalCase componentes y tipos; camelCase funciones y variables.
- Rutas en kebab-case.
- Tablas y columnas según convención única definida por EF/PostgreSQL.
- Usar nombres del glosario y no crear sinónimos.

## Errores

- Backend devuelve errores estructurados.
- Frontend traduce errores técnicos a mensajes útiles.
- No ignorar promesas ni excepciones.
- Logs con contexto suficiente y sin secretos.
