import { LOGO_TEXTO, TAMANO_LOGO_TEXTO } from '@/lib/marca'

export function Marca() {
  return <img
    className="brand-image"
    src={LOGO_TEXTO}
    alt="Fishy!"
    width={TAMANO_LOGO_TEXTO.ancho}
    height={TAMANO_LOGO_TEXTO.alto}
    decoding="async"
  />
}
