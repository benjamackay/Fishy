// La demostración solo se habilita explícitamente en el servidor de desarrollo.
export const modoDemo = import.meta.env.DEV && import.meta.env.MODE !== 'test' && import.meta.env.VITE_FRONTEND_MOCK === 'true'
