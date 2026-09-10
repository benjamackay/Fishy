/**
 * Las pantallas consumen FuentePanel a través de usePanel.
 * El contrato de grupos es src/types/grupos.ts y la integración se completa en panelReal.ts.
 * Se retiró el adaptador que proponía altas por jugador_id: el flujo ahora usa correo.
 */
export { panelReal } from './panelReal'
