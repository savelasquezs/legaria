# Entornos y configuración

## Ambientes

- Development: máquina local, logs útiles y Swagger habilitado.
- Test: configuración aislada y base efímera.
- Production: secretos externos, HTTPS, logs seguros y CORS restringido.

## Variables de autenticación

El archivo `.env.example` contiene únicamente nombres y valores no secretos. En
el entorno Docker local, Compose traduce estas variables a la configuración .NET:

- `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_PORT`
- `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_SIGNING_KEY`
- `RESEND_API_KEY`, `RESEND_FROM_EMAIL`, `RESEND_FROM_NAME`, `RESEND_REPLY_TO_EMAIL`
- `FRONTEND_BASE_URL`
- `BOOTSTRAP_OWNER_EMAIL`, `BOOTSTRAP_OWNER_FIRST_NAME`,
  `BOOTSTRAP_OWNER_LAST_NAME`, `BOOTSTRAP_OWNER_PASSWORD`
- `CERT_PASSWORD`, `API_HTTPS_PORT`
- `VITE_API_BASE_URL`

La API falla al iniciar si la conexión, JWT, Resend o URL del frontend son inválidos. Las variables de bootstrap solo son exigidas si no existe ningún `PlatformUser`.

## Reglas

- Incluir `.env.example` o ejemplos sin secretos.
- Nunca commitear `.env`, claves privadas o credenciales.
- Validar opciones al iniciar.
- Mantener configuraciones de producción fuera del repositorio.
- No usar una clave JWT de desarrollo en producción.
- Retirar `BootstrapOwner__Password` después del primer bootstrap exitoso.
- Ejecutar la migración de base de datos antes de iniciar la API en producción.
