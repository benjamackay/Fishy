/**
 * Interruptores de configuracion. Todos se leen del `.env` (ver .env.example).
 */

/** `false` explicito para pegarle a los endpoints reales de grupos. */
export const USAR_MOCK_GRUPOS =
  import.meta.env.VITE_GRUPOS_MOCK !== 'false'

/**
 * El backend todavia no expone `is_admin` en `AdultoResponsableSerializer`, asi
 * que ninguna cuenta se ve como admin desde el frontend. Este flag permite
 * trabajar el panel mientras tanto. Quitar del .env cuando Django exponga el
 * campo.
 */
export const FORZAR_ADMIN = import.meta.env.VITE_FORZAR_ADMIN === 'true'
