# Frontend de Legaria

Aplicación Vue 3 + Quasar con TypeScript estricto, Pinia, Axios, Vue Router, Vitest y Vue Test Utils.

## Alcance

- Login.
- Recuperación y restablecimiento de contraseña.
- Verificación y reenvío de correo.
- Restauración de sesión mediante `/api/auth/refresh`.
- Rutas mínimas separadas para plataforma (`/platform`) y tenant (`/app`).

El access token vive únicamente en memoria. Axios envía la cookie con credenciales, coordina un solo refresh para solicitudes concurrentes y reintenta cada request como máximo una vez.

En desarrollo, Vite sirve `https://localhost:5173` con el certificado generado
en `.local/https`. El archivo `.env` de la raíz debe contener
`CERT_PASSWORD`; frontend y API deben usar HTTPS para que la cookie
`SameSite=Lax` restaure la sesión después de una recarga.

## Configuración y comandos

Definir `VITE_API_BASE_URL` con el origen de la API:

```dotenv
VITE_API_BASE_URL=https://localhost:7007
```

```powershell
npm.cmd install
npm.cmd run dev
npm.cmd run lint
npm.cmd run test
npm.cmd run build
```

No almacenar access ni refresh tokens en `localStorage` o `sessionStorage`.
