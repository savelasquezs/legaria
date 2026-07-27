# Entornos y configuración

## Ambientes

- Development: máquina local, logs útiles y Swagger habilitado.
- Test: configuración aislada y base efímera.
- Production: secretos externos, HTTPS, logs seguros y CORS restringido.

## Variables de autenticación

El archivo `.env.example` contiene únicamente nombres y valores no secretos:

- `ConnectionStrings__DefaultConnection`
- `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey`
- `Resend__ApiKey`, `Resend__FromEmail`, `Resend__FromName`, `Resend__ReplyToEmail`
- `Frontend__BaseUrl`
- `BootstrapOwner__Email`, `BootstrapOwner__DisplayName`, `BootstrapOwner__Password`
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
