# Instalar Legaria desde cero en otro computador

Esta guía prepara un entorno local de Legaria desde un clon limpio del
repositorio. PostgreSQL y la API .NET se ejecutan en Docker; el frontend Vue se
ejecuta en el computador anfitrión.

Los comandos están escritos para Windows PowerShell.

## 1. Requisitos

Instalar antes de clonar:

- Git.
- Docker Desktop configurado para contenedores Linux, con Docker Compose.
- .NET SDK 8, necesario para generar y confiar el certificado HTTPS local.
- Node.js `20.19` o superior; también es válido `22.12` o superior.
- npm, incluido con Node.js.

Abrir Docker Desktop y esperar a que el motor esté listo. Verificar las
herramientas desde PowerShell:

```powershell
git --version
docker --version
docker compose version
dotnet --version
node --version
npm.cmd --version
```

Los puertos locales esperados son:

| Servicio | Puerto |
| --- | ---: |
| Frontend | `5173` |
| API HTTPS | `7007` |
| PostgreSQL | `5434` |

## 2. Clonar el repositorio

Elegir una carpeta de trabajo y ejecutar:

```powershell
git clone https://github.com/savelasquezs/legaria.git
cd legaria
git checkout main
```

Todos los comandos siguientes se ejecutan desde la raíz `legaria`, salvo que se
indique lo contrario.

## 3. Crear la configuración local

Crear el archivo ignorado `.env` a partir del ejemplo:

```powershell
Copy-Item .env.example .env
notepad .env
```

Como mínimo, definir valores propios para estas variables:

```dotenv
POSTGRES_PASSWORD=<contraseña-local-de-postgresql>
BOOTSTRAP_OWNER_EMAIL=owner.pruebas@legaria.local
BOOTSTRAP_OWNER_PASSWORD=<contraseña-local-del-owner>
BOOTSTRAP_OWNER_FIRST_NAME=Owner
BOOTSTRAP_OWNER_LAST_NAME=Pruebas
```

Conservar estos valores para que el script genere secretos aleatorios:

```dotenv
JWT_SIGNING_KEY=__GENERATE__
CERT_PASSWORD=__GENERATE__
```

No copiar `.env` entre computadores, enviarlo por mensajería ni agregarlo a
Git. Las contraseñas del ejemplo anterior deben compartirse por un canal seguro
si varios desarrolladores necesitan usar las mismas.

La configuración de Resend incluida es deliberadamente local y no contiene una
API key real. El remitente es `noreply@senorarroz.com`, pero no se deben probar
flujos que envíen correo hasta configurar credenciales válidas de un ambiente
autorizado.

## 4. Generar HTTPS y levantar backend/base de datos

Ejecutar:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup-local.ps1
```

En el primer arranque Windows puede pedir confirmación para confiar el
certificado HTTPS. Se debe aceptar únicamente si el comando se está ejecutando
desde este repositorio clonado.

El script realiza estas operaciones:

1. Valida las variables obligatorias de `.env`.
2. Genera una clave JWT y una contraseña de certificado aleatorias.
3. Guarda ambos secretos únicamente en `.env`.
4. Genera y confía `.local/https/legaria-local.pfx`.
5. Crea `frontend/.env.local` con la URL HTTPS de la API.
6. Construye la imagen .NET 8.
7. Inicia PostgreSQL.
8. Ejecuta `InitialIdentityAndAuthentication` mediante el contenedor `migrate`.
9. Inicia la API y crea el primer `OWNER` si la base estaba vacía.

No es necesario instalar PostgreSQL ni ejecutar la API .NET directamente en el
computador.

## 5. Comprobar los contenedores

```powershell
docker compose ps --all
```

El resultado correcto debe mostrar:

- `legaria-postgres`: `Up` y `healthy`.
- `legaria-api`: `Up` y `healthy`.
- `legaria-local-migrate-1`: `Exited (0)`.

`Exited (0)` en `migrate` es normal: la migración es una tarea de una sola
ejecución, no un servicio permanente.

La API y Swagger quedan disponibles en:

```text
https://localhost:7007
https://localhost:7007/swagger
```

Para confirmar el bootstrap directamente en PostgreSQL:

```powershell
docker compose exec postgres psql --username legaria_local --dbname legaria `
  --command "SELECT email, role, email_verified_at IS NOT NULL AS verified FROM platform_users;"
```

Debe existir un solo usuario con rol `OWNER` y `verified` igual a `true`.

## 6. Instalar e iniciar el frontend

En otra terminal PowerShell:

```powershell
cd frontend
npm.cmd ci
npm.cmd run dev
```

Abrir:

```text
http://localhost:5173
```

Iniciar sesión con `BOOTSTRAP_OWNER_EMAIL` y `BOOTSTRAP_OWNER_PASSWORD` del
`.env` local.

Al cargar la página sin una sesión anterior, `/api/auth/refresh` puede responder
401 con `auth.invalid_refresh_token`. Es esperado y no bloquea el login: la
aplicación está comprobando si existe una cookie de sesión previa.

## 7. Arranques posteriores

Backend y PostgreSQL conservando datos:

```powershell
docker compose up --detach
```

Frontend:

```powershell
cd frontend
npm.cmd run dev
```

Estado y logs:

```powershell
docker compose ps --all
docker compose logs --follow api
docker compose logs migrate
docker compose logs --follow postgres
```

Detener servicios sin eliminar la base:

```powershell
docker compose down
```

## 8. Reiniciar completamente desde una base vacía

Advertencia: los siguientes comandos eliminan todos los datos locales de
Legaria.

Primero verificar que el volumen pertenece a este Compose:

```powershell
docker volume inspect legaria-postgres-data
```

Después recrear el entorno:

```powershell
docker compose down --volumes
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup-local.ps1
```

Cambiar `BOOTSTRAP_OWNER_PASSWORD` sin eliminar la base no cambia la contraseña
de un usuario ya creado. Esa variable solo se usa durante el primer bootstrap.

## 9. Problemas frecuentes

### Docker no responde

Abrir Docker Desktop y esperar a que el motor esté listo. Confirmar con:

```powershell
docker info
```

### Un puerto está ocupado

Consultar los puertos:

```powershell
Get-NetTCPConnection -State Listen -LocalPort 5173,7007,5434 `
  -ErrorAction SilentlyContinue
```

- Para PostgreSQL, cambiar `POSTGRES_PORT` en `.env`.
- Para la API, cambiar `API_HTTPS_PORT` en `.env` y ejecutar nuevamente
  `setup-local.ps1` para actualizar `frontend/.env.local`.
- El frontend debe conservar el mismo origen configurado en
  `FRONTEND_BASE_URL`; de lo contrario CORS rechazará las solicitudes.

No se debe detener ni modificar un contenedor de otro repositorio para liberar
un puerto de Legaria.

### El certificado no coincide con `.env`

Detener la API, eliminar únicamente el PFX local y volver a ejecutar el script:

```powershell
docker compose down
Remove-Item -LiteralPath .local\https\legaria-local.pfx
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup-local.ps1
```

No eliminar certificados o carpetas globales del sistema.

### La migración falla

Revisar:

```powershell
docker compose logs migrate
docker compose logs postgres
```

La API no ejecuta migraciones automáticamente. Solo debe iniciar después de que
`migrate` termine con código cero.

## 10. Verificación opcional del repositorio

Backend:

```powershell
dotnet format backend/Legaria.sln --verify-no-changes
dotnet build backend/Legaria.sln --configuration Release
dotnet test backend/Legaria.sln --configuration Release
```

Frontend:

```powershell
cd frontend
npm.cmd run lint
npm.cmd run test
npm.cmd run build
```

Las pruebas de integración backend usan Docker para crear una base PostgreSQL
efímera mediante Testcontainers.
