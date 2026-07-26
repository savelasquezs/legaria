# Guías de interfaz y experiencia

## Objetivo

Crear una interfaz administrativa moderna, clara y bonita, sin decorar por decorar ni inventar funcionalidades. Debe sentirse consistente desde la primera pantalla.

## Principios

1. Jerarquía visual evidente.
2. Acciones principales fáciles de encontrar.
3. Menos ruido y más claridad.
4. Estados completos: inicial, carga, vacío, error, éxito y sin permiso.
5. Responsive real, no solo reducción de ancho.
6. Accesibilidad básica desde el inicio.
7. Reutilización con propósito.

## Quasar

Usar componentes nativos de Quasar antes de recrearlos. Centralizar:

- paleta y tokens visuales;
- tamaños y espaciado comunes;
- estilo de dialogs, inputs y botones;
- notificaciones;
- iconografía.

No aplicar estilos globales agresivos que rompan componentes.

## Componentes reutilizables

Crear un componente común cuando tenga uso repetido o un comportamiento que deba ser idéntico. Prioridades al aparecer el primer caso real:

- `AppDialog`.
- `ConfirmDialog`.
- `FormDialog`.
- `PageHeader`.
- `EmptyState`.
- `ErrorState`.
- `LoadingSkeleton`.
- `StatusChip`.
- `SearchField`.

No construir un wrapper superficial para cada `QBtn`, `QInput` o `QCard`.

## Páginas

Una página administrativa normalmente incluye:

- encabezado con título, descripción breve y acción principal;
- filtros relevantes;
- contenido principal;
- estado de carga;
- estado vacío con orientación;
- error recuperable;
- respuesta adaptable para móvil.

No incluir KPIs, tarjetas o gráficas sin datos y propósito definidos.

## Modales

- Formularios breves o confirmaciones enfocadas.
- Ancho adecuado y scroll interno controlado.
- Botón primario a la derecha y cancelar claramente disponible.
- Estado de envío visible.
- No cerrar durante una operación crítica.
- Tooltips en botones de solo ícono.
- Confirmación destructiva con nombre del elemento cuando ayude a prevenir errores.

## Tooltips

Usar en:

- botones de ícono;
- términos técnicos poco evidentes;
- contenido truncado cuando el texto completo sea útil.

No usar como sustituto de etiquetas, errores o instrucciones esenciales.

## Formularios

- Etiqueta persistente y ejemplo solo cuando aporta.
- Validación al perder foco o enviar, evitando mensajes agresivos mientras el usuario escribe.
- Errores del servidor mapeados al campo cuando sea posible.
- Campos relacionados agrupados.
- Guardar deshabilitado mientras el formulario sea inválido o se esté enviando.
- Mantener lo escrito tras errores.

## Tablas

- Columnas necesarias, sin mostrar IDs técnicos.
- Acciones consistentes.
- Filtros conservan su estado durante la navegación cuando sea útil.
- En móvil usar cards o columnas prioritarias.
- Estado vacío distinto de “sin resultados por filtros”.
- Paginación server-side para listados grandes.

## Mensajes

- Éxito: concreto, sin exageración.
- Error: explicar qué ocurrió y cómo continuar cuando se sepa.
- Confirmaciones: describir consecuencia real.
- No mostrar excepciones técnicas al usuario.

## Permisos

La UI puede ocultar o deshabilitar acciones no permitidas para reducir confusión. Esto nunca reemplaza autorización backend. Cuando se deshabilite una acción, explicar el motivo si no es evidente.

## Responsive

Probar como mínimo:

- móvil estrecho;
- tablet;
- escritorio común.

Evitar dialogs que excedan el viewport, tablas horizontales inutilizables y botones demasiado pequeños.
