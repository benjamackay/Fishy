# Fishy! — Web

Frontend web del proyecto **Fishy!**, construido con **Vite + React + TypeScript**.

Consume la misma API del backend Django que usa Unity. El contrato está en
[`DOCS_JSON_API.md`](../DOCS_JSON_API.md) y el flujo de control parental en
[`FLUJO_CONTROL_PARENTAL.md`](../FLUJO_CONTROL_PARENTAL.md).

## Puesta en marcha

```
npm install
cp .env.example .env    # opcional: los valores por defecto ya sirven en local
npm run dev
```

Queda en http://localhost:5173.

El backend tiene que estar corriendo aparte en `http://127.0.0.1:8000`
(ver [`Backend/README.md`](../Backend/README.md)).

## Scripts

| Comando | Qué hace |
|---|---|
| `npm run dev` | Servidor de desarrollo con HMR |
| `npm run build` | Chequea tipos (`tsc -b`) y compila a `dist/` |
| `npm run preview` | Sirve el build de producción |
| `npm run lint` | Linter (oxlint) |

## Estructura

```
web/
├── .env.example        ← plantilla de variables (el .env NO se commitea)
├── vite.config.ts      ← proxy /api → Django y alias @ → src
└── src/
    ├── main.tsx        ← punto de entrada, monta el router
    ├── routes.tsx      ← definición de rutas
    ├── index.css       ← estilos base y variables de color
    ├── layouts/        ← layouts compartidos (header, <Outlet />)
    ├── pages/          ← una pantalla por ruta
    └── lib/
        ├── api.ts      ← cliente HTTP (fetch + Authorization: Token)
        └── token.ts    ← guarda el token del adulto en localStorage
```

Los imports usan el alias `@`, que apunta a `src/`:

```ts
import { api } from '@/lib/api'
```

## Cómo se habla con la API

`src/lib/api.ts` expone un cliente con los verbos HTTP. Adjunta solo el header
`Authorization: Token <token>` cuando hay sesión guardada y lanza `ApiError`
(con `status` y `data`) si el backend responde con error:

```ts
import { api, ApiError } from '@/lib/api'

try {
  const jugadores = await api.get<Jugador[]>('/jugadores/')
} catch (e) {
  if (e instanceof ApiError && e.status === 401) {
    // sesión vencida
  }
}
```

Las rutas se escriben **sin** el prefijo `/api` (`/jugadores/`, no
`/api/jugadores/`): ya viene en la URL base.

### Sobre el proxy y CORS

En desarrollo `VITE_API_URL` vale `/api`, así que los requests salen al mismo
origen (`localhost:5173`) y Vite los reenvía a Django. **No hay CORS de por
medio y no hace falta tocar el backend.**

En producción hay que definir `VITE_API_URL` con la URL real del backend
(p. ej. `https://api.fishy.cl/api`); ahí sí Django necesitará permitir el origen
del sitio.

## Autenticación

El login es el del **adulto responsable** (`POST /auth/login/`). Los perfiles de
los menores no tienen credenciales propias. El token que devuelve el backend se
guarda con `setToken()` de `src/lib/token.ts` y `api.ts` lo adjunta solo.
