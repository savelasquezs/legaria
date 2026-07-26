# Entornos y configuración

## Ambientes

- Development: máquina local, logs útiles y Swagger habilitado.
- Test: configuración aislada y base efímera.
- Production: secretos externos, HTTPS, logs seguros y CORS restringido.

## Variables esperadas

Los nombres finales se definirán al crear los proyectos, pero incluirán:

- conexión PostgreSQL;
- issuer, audience y clave JWT;
- configuración de cookie;
- orígenes CORS;
- almacenamiento de archivos;
- nivel de logs.

## Reglas

- Incluir `.env.example` o ejemplos sin secretos.
- Nunca commitear `.env`, claves privadas o credenciales.
- Validar opciones al iniciar.
- Mantener configuraciones de producción fuera del repositorio.
- No usar una clave JWT de desarrollo en producción.
