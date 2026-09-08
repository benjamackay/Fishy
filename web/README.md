# Fishy! — Web

Frontend web del proyecto **Fishy!**, construido con **Vite + React + TypeScript**.

Consume la misma API del backend Django que usa Unity. El contrato está en
[`DOCS_JSON_API.md`](../DOCS_JSON_API.md) y el flujo de control parental en
[`FLUJO_CONTROL_PARENTAL.md`](../FLUJO_CONTROL_PARENTAL.md).

## Los dos paneles

| Panel | Ruta | Quién entra | Estado |
|---|---|---|---|
| **Sesiones** (adulto responsable) | `/` y `/partidas/:id` | cualquier cuenta | contra la API real |
| **Grupos** (administración) | `/admin/grupos` | cuentas con `is_admin` | contra un **mock local** |

### Panel de sesiones

Lista los perfiles de menores de la cuenta y las partidas de cada uno. Al entrar
a una partida se ve el detalle: avance, decisiones por zona, oportunidades de
mejora, misiones, zonas y mochila.

Se muestran agrupadas por perfil y no como lista plana porque el progreso cuelga
del perfil: dos hermanos no comparten avance.

### Panel de grupos

Junta perfiles de menores (típicamente un curso) y muestra el avance de cada uno
y el agregado del grupo.

> ⚠️ **El backend todavía no tiene grupos.** No existe modelo `Grupo`, ni
> membresías, ni endpoints. Este panel corre contra
> [`src/mocks/gruposMock.ts`](src/mocks/gruposMock.ts), que guarda en
> `localStorage`. La especificación que Django debe implementar está en
> [`src/types/grupos.ts`](src/types/grupos.ts).

Cuando el backend exista, se pone `VITE_GRUPOS_MOCK=false` en el `.env` y **no
hay que tocar ninguna pantalla**: la fachada
[`src/api/grupos.ts`](src/api/grupos.ts) ya trae escrita la implementación HTTP.

Faltan además dos cosas en el backend para que el panel funcione de verdad:

1. **Exponer `is_admin`** en `AdultoResponsableSerializer.fields`. Hoy el campo
   existe en el modelo pero no viaja en `/auth/perfil/`, así que el frontend no
   puede saber quién es administrador. Mientras tanto está el flag de desarrollo
   `VITE_FORZAR_ADMIN=true`.
2. **Un endpoint de avance agregado** (`GET /grupos/{id}/avance/`). No basta con
   componerlo desde el frontend: todas las vistas de partidas filtran por
   `usuario_jugador__adulto=request.user`, así que un admin solo vería a sus
   propios hijos.

## Puesta en marcha

```
npm install
cp .env.example .env
npm run dev
```

Queda en http://localhost:5173. El backend tiene que estar corriendo aparte en
`http://127.0.0.1:8000` (ver [`Backend/README.md`](../Backend/README.md)).

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
    ├── main.tsx        ← punto de entrada
    ├── routes.tsx      ← rutas y guardias
    ├── index.css       ← tokens de color y estilos base
    ├── api/            ← una función por endpoint
    │   └── grupos.ts   ← fachada: elige entre mock y HTTP real
    ├── auth/           ← sesión del adulto y guardias de ruta
    ├── components/     ← piezas compartidas (KPI, medidor, barra de riesgo)
    ├── hooks/          ← useAsync
    ├── layouts/        ← header y <Outlet />
    ├── lib/            ← cliente HTTP, token, formato, flags
    ├── mocks/          ← implementación falsa de grupos (se borra después)
    ├── pages/          ← una pantalla por ruta
    └── types/          ← DTOs del backend y contrato de grupos
```

Los imports usan el alias `@`, que apunta a `src/`:

```ts
import { api } from '@/lib/api'
```

## Cómo se habla con la API

[`src/lib/api.ts`](src/lib/api.ts) expone un cliente con los verbos HTTP. Adjunta
el header `Authorization: Token <token>` cuando hay sesión y lanza `ApiError`
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

En el detalle de una partida, los endpoints secundarios se piden con un envoltorio
que traga el error: si falla el de inventario, el resto de la página se dibuja
igual en vez de quedar en blanco.

### Sobre el proxy y CORS

En desarrollo `VITE_API_URL` vale `/api`, así que los requests salen al mismo
origen (`localhost:5173`) y Vite los reenvía a Django. **No hay CORS de por
medio y no hace falta tocar el backend.**

En producción hay que definir `VITE_API_URL` con la URL real del backend; ahí sí
Django necesitará permitir el origen del sitio.

## Autenticación

El login es el del **adulto responsable** (`POST /auth/login/`), y autentica por
`nombre`, no por email. Los perfiles de los menores no tienen credenciales. El
token se guarda con `setToken()` de [`src/lib/token.ts`](src/lib/token.ts) y
`api.ts` lo adjunta solo.

## Notas de visualización

Los colores de datos salen de una paleta validada para contraste y daltonismo:

- El **color de estado** (bien / mejorable / riesgo) nunca viaja solo: siempre
  lleva su etiqueta de texto al lado.
- Las **barras de avance** usan un solo tono, con la pista en otro paso de la
  misma rampa azul.
- El **riesgo por zona** es divergente (azul = seguro, rojo = riesgo) con gris
  neutro al medio, porque tiene polaridad y un cero con significado. Ojo con el
  signo del backend: **más alto = más seguro**.
