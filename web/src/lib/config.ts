/** Un inicio de sesión real nunca activa datos ficticios. */
export const DEMO_DISPONIBLE = import.meta.env.VITE_DEMO === 'true' ||
  (import.meta.env.DEV && import.meta.env.VITE_DEMO !== 'false')

// Los permisos provienen exclusivamente del perfil autenticado.
// VITE_FORZAR_ADMIN y VITE_GRUPOS_MOCK ya no se utilizan.
