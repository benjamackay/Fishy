using System.Collections.Generic;
using UnityEngine;

namespace Fishy.World
{
    /// <summary>
    /// Un sitio donde Otto puede aparecer, y a qué zona pertenece.
    ///
    /// Hoy <see cref="OttoController"/> tiene un único <c>spawnPoint</c> para todo el
    /// mapa, así que al restaurar una partida solo hay dos salidas: dejar a Otto en
    /// la posición guardada, o mandarlo al principio del juego. Ninguna sirve cuando
    /// la posición guardada ya no es válida — porque quedó dentro de una zona que
    /// esta partida todavía no tiene abierta, o porque el mapa cambió y esas
    /// coordenadas ahora son agua.
    ///
    /// Con estos puntos hay una tercera: devolverlo a un lugar seguro <b>de su
    /// zona</b>, que es donde estaba jugando.
    ///
    /// Montaje: un GameObject vacío en el sitio, este componente, y el id de la zona.
    /// Marca uno por zona como <see cref="porDefecto"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class PuntoDeAparicion : MonoBehaviour
    {
        [Tooltip("Zona a la que pertenece este punto. Si se deja vacío se deduce " +
                 "preguntando en qué zona cae su propia posición.")]
        public string zonaId = "";

        [Tooltip("El punto de referencia de la zona. Si hay varios marcados, gana el " +
                 "primero que se registre.")]
        public bool porDefecto = false;

        [Tooltip("Dibujar una marca en la vista de escena para verlo sin seleccionarlo.")]
        public bool mostrarGizmo = true;

        private static readonly List<PuntoDeAparicion> _vivos = new List<PuntoDeAparicion>();
        public static IReadOnlyList<PuntoDeAparicion> Vivos => _vivos;

        /// <summary>
        /// Zona efectiva. Si no se puso a mano, se deduce de dónde está el punto: es
        /// lo que casi siempre se quiere, y evita que el id y la posición se
        /// contradigan si alguien mueve el marcador y olvida cambiar el texto.
        /// </summary>
        public string Zona
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(zonaId)) return zonaId.Trim();
                var rastreador = ZonaActual.Instance;
                return rastreador != null ? rastreador.ZonaEn(transform.position) : "";
            }
        }

        public Vector2 Posicion => transform.position;

        private void OnEnable()  { if (!_vivos.Contains(this)) _vivos.Add(this); }
        private void OnDisable() { _vivos.Remove(this); }

        // ── Consultas ─────────────────────────────────────────────────────────

        /// <summary>
        /// Dónde dejar a Otto en esa zona. Prefiere el marcado por defecto; si no hay
        /// ninguno, el primero de la zona; si la zona no tiene puntos, null.
        /// </summary>
        public static PuntoDeAparicion De(string zona)
        {
            if (string.IsNullOrWhiteSpace(zona)) return null;
            zona = zona.Trim();

            PuntoDeAparicion primero = null;
            foreach (PuntoDeAparicion p in _vivos)
            {
                if (p == null || p.Zona != zona) continue;
                if (p.porDefecto) return p;
                if (primero == null) primero = p;
            }
            return primero;
        }

        /// <summary>
        /// El punto más cercano a una posición, dentro de una zona. Sirve para
        /// restaurar: si la posición guardada ya no vale, se deja a Otto en el sitio
        /// bueno más parecido a donde estaba, en vez de al principio de la zona.
        /// </summary>
        public static PuntoDeAparicion MasCercano(Vector2 posicion, string zona = null)
        {
            PuntoDeAparicion mejor = null;
            float mejorDistancia = float.MaxValue;

            foreach (PuntoDeAparicion p in _vivos)
            {
                if (p == null) continue;
                if (!string.IsNullOrWhiteSpace(zona) && p.Zona != zona.Trim()) continue;

                float d = ((Vector2)p.transform.position - posicion).sqrMagnitude;
                if (d >= mejorDistancia) continue;

                mejorDistancia = d;
                mejor = p;
            }
            return mejor;
        }

        /// <summary>Las zonas que tienen al menos un punto declarado.</summary>
        public static HashSet<string> ZonasConPunto()
        {
            var zonas = new HashSet<string>();
            foreach (PuntoDeAparicion p in _vivos)
            {
                if (p == null) continue;
                string z = p.Zona;
                if (!string.IsNullOrWhiteSpace(z)) zonas.Add(z);
            }
            return zonas;
        }

        private void OnDrawGizmos()
        {
            if (!mostrarGizmo) return;

            Gizmos.color = porDefecto ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.8f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.9f);
        }
    }
}
