# Entorno local con Docker

Para instalar todas las dependencias y preparar un computador nuevo desde un
clon limpio, consultar
[`LOCAL_SETUP_FROM_SCRATCH.md`](LOCAL_SETUP_FROM_SCRATCH.md).

Este entorno levanta exclusivamente los servicios de Legaria:

- PostgreSQL 16 en `localhost:5434`, con volumen persistente.
- Una tarea `migrate` que aplica hasta `OrganizationProvisioningAndDivipola` y termina.
- La API .NET 8 en `https://localhost:7007`, ejecutada como usuario no root.

El frontend continúa ejecutándose fuera de Docker.

## Primer arranque

1. Copiar `.env.example` como `.env`.
2. Completar `POSTGRES_PASSWORD`, `BOOTSTRAP_OWNER_EMAIL` y
   `BOOTSTRAP_OWNER_PASSWORD`. El `.env` está ignorado por Git.
3. Ejecutar desde la raíz del repositorio:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/setup-local.ps1
```

El script genera una clave JWT aleatoria y una contraseña aleatoria para el
certificado cuando encuentra `__GENERATE__`, las guarda únicamente en `.env`,
genera el PFX ignorado en `.local/https`, confía el certificado de desarrollo y
levanta los contenedores.

Para preparar los secretos y el certificado sin iniciar Docker:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/setup-local.ps1 -SkipStart
```

El frontend debe usar `VITE_API_BASE_URL=https://localhost:7007`.

## Operación cotidiana

```powershell
# Iniciar o reconstruir
docker compose up --detach --build

# Estado y logs
docker compose ps
docker compose logs --follow api
docker compose logs migrate
docker compose logs --follow postgres

# Reiniciar sin eliminar datos
docker compose restart api postgres

# Detener sin eliminar datos
docker compose down
```

La API depende de que PostgreSQL esté saludable y de que `migrate` termine con
código cero. La API no aplica migraciones por sí misma.

## Acceso a PostgreSQL

Con un cliente instalado en el host:

```powershell
psql --host localhost --port 5434 --dbname legaria --username legaria_local
```

O mediante el cliente incluido en el contenedor:

```powershell
docker compose exec postgres psql --username legaria_local --dbname legaria
```

La contraseña se consulta en el `.env` local; no debe copiarse a archivos
versionados.

## Recrear completamente la base local

Este comando elimina el volumen y todos los datos locales de Legaria:

```powershell
docker compose down --volumes
powershell -ExecutionPolicy Bypass -File scripts/setup-local.ps1
```

Antes de ejecutarlo, confirmar con `docker volume inspect legaria-postgres-data`
que el volumen pertenece a este repositorio.

## Rotar credenciales locales

- Para una base vacía: detener el entorno, eliminar el volumen de Legaria,
  cambiar usuario/contraseña en `.env` y volver a iniciar.
- Para una base con datos: cambiar primero el rol en PostgreSQL y después
  actualizar `.env`.
- Para rotar JWT o certificado: detener la API, cambiar el valor en `.env` y,
  para el certificado, eliminar únicamente `.local/https/legaria-local.pfx`;
  después ejecutar nuevamente `setup-local.ps1`.
- Al rotar la contraseña del propietario, cambiarla mediante el flujo de la
  aplicación o directamente con una operación administrativa aprobada. Cambiar
  `BOOTSTRAP_OWNER_PASSWORD` no modifica un usuario que ya existe.

Ninguna de estas credenciales es válida para producción.
