using System.Collections.Generic;
using UnityEngine;

namespace Fishy.World
{
    /// <summary>
    /// Área de una zona del mapa: dice si un punto está dentro de ella.
    ///
    /// Hace falta porque hasta ahora el juego sabía <b>abrir</b> zonas pero no
    /// <b>contenerlas</b>: <see cref="BlockedZone"/> usa colliders sólidos como
    /// barrera, y al desbloquear los desactiva. Sin esto no se puede decir en qué
    /// zona está Otto, que es lo que HDU-15 necesita para restaurar la partida y lo
    /// que el backend rechazó guardar por ese mismo motivo.
    ///
    /// <b>Trabaja con la geometría, no con el collider.</b> Lee los caminos del
    /// PolygonCollider2D y hace la prueba punto-en-polígono a mano, en vez de usar
    /// <c>OverlapPoint</c>. Es a propósito: <c>OverlapPoint</c> devuelve false sobre
    /// un collider desactivado, y BlockedZone desactiva los suyos justo al
    /// desbloquear la zona — o sea, exactamente cuando Otto puede entrar en ella.
    ///
    /// Montaje: ponlo en el mismo GameObject que la BlockedZone y deja
    /// <see cref="areas"/> vacío; toma sus polígonos y no hay que redibujar nada.
    /// Para una zona que no se bloquea, ponlo en un objeto con su propio polígono.
    /// </summary>
    [DisallowMultipleComponent]
    public class ZonaMundo : MonoBehaviour
    {
        [Header("Identidad")]
        [Tooltip("Id de la zona. Si se deja vacío y hay una BlockedZone al lado, " +
                 "se usa el suyo.")]
        public string zonaId = "";

        [Tooltip("Cómo se llama esta zona para el niño/a: 'El Bosque', 'El Arrecife'. " +
                 "Si se deja vacío se muestra el id, que no está escrito para leerse.")]
        public string nombreVisible = "";

        [Header("Área")]
        [Tooltip("Polígonos que forman la zona. Si se deja vacío se toman los " +
                 "PolygonCollider2D de este mismo objeto.")]
        public PolygonCollider2D[] areas;

        /// <summary>Todas las zonas vivas. Estático para que el rastreador no tenga
        /// que buscarlas en cada comprobación.</summary>
        private static readonly List<ZonaMundo> _vivas = new List<ZonaMundo>();
        public static IReadOnlyList<ZonaMundo> Vivas => _vivas;

        /// <summary>Id efectivo: el propio, o el de la BlockedZone de al lado.</summary>
        public string Id
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(zonaId)) return zonaId.Trim();
                var bloqueada = GetComponent<BlockedZone>();
                return bloqueada != null ? bloqueada.zoneId : name;
            }
        }

        /// <summary>
        /// Nombre para mostrar. Cae al <see cref="Id"/> si nadie escribió uno, porque
        /// un hueco en pantalla es peor que un id feo.
        /// </summary>
        public string Nombre =>
            string.IsNullOrWhiteSpace(nombreVisible) ? Id : nombreVisible.Trim();

        /// <summary>
        /// Punto de referencia de la zona: la media de los vértices de sus polígonos,
        /// en coordenadas de mundo. Es hacia donde apunta la flecha de HDU-16 cuando la
        /// zona no tiene ningún <see cref="PuntoDeAparicion"/> que sea mejor destino.
        ///
        /// Media y no centro del bounding box: con una zona en forma de L el bounding
        /// box cae fuera del terreno, y la flecha señalaría agua.
        ///
        /// Se calcula una vez y se guarda. Los polígonos del mapa no se mueven en
        /// juego; si alguna vez se editan en caliente, <see cref="OlvidarCentro"/>.
        /// </summary>
        public Vector2 Centro
        {
            get
            {
                if (_centro.HasValue) return _centro.Value;

                Vector2 suma = Vector2.zero;
                int puntos = 0;

                if (areas != null)
                {
                    foreach (PolygonCollider2D area in areas)
                    {
                        if (area == null) continue;
                        for (int i = 0; i < area.pathCount; i++)
                        {
                            foreach (Vector2 punto in area.GetPath(i))
                            {
                                suma += (Vector2)area.transform.TransformPoint(punto + area.offset);
                                puntos++;
                            }
                        }
                    }
                }

                // Sin polígonos queda la posición del objeto, que al menos está en la
                // parte del mapa donde alguien colocó la zona.
                _centro = puntos > 0 ? suma / puntos : (Vector2)transform.position;
                return _centro.Value;
            }
        }

        private Vector2? _centro;

        /// <summary>Hace que <see cref="Centro"/> se vuelva a calcular.</summary>
        public void OlvidarCentro() => _centro = null;

        /// <summary>La zona con ese id, o null si ninguna viva lo tiene.</summary>
        public static ZonaMundo De(string zonaId)
        {
            if (string.IsNullOrWhiteSpace(zonaId)) return null;
            zonaId = zonaId.Trim();

            foreach (ZonaMundo zona in _vivas)
                if (zona != null && zona.Id == zonaId) return zona;

            return null;
        }

        /// <summary>
        /// Nombre legible de una zona por su id. Si esa zona no está viva —todavía no
        /// se cargó, o nadie le puso el componente— devuelve el id: la frase de la
        /// interfaz sigue teniendo sentido aunque quede fea.
        /// </summary>
        public static string NombreDe(string zonaId)
        {
            ZonaMundo zona = De(zonaId);
            return zona != null ? zona.Nombre : (zonaId ?? "").Trim();
        }

        private void Awake()
        {
            if (areas == null || areas.Length == 0)
                areas = GetComponents<PolygonCollider2D>();

            if (areas == null || areas.Length == 0)
            {
                Debug.LogWarning($"[ZonaMundo] '{name}' no tiene ningún PolygonCollider2D, " +
                                 "así que nunca va a contener a nadie.", this);
            }
        }

        private void OnEnable()  { if (!_vivas.Contains(this)) _vivas.Add(this); }
        private void OnDisable() { _vivas.Remove(this); }

        /// <summary>
        /// ¿Este punto del mundo cae dentro de la zona?
        ///
        /// Con varios polígonos se aplica XOR y no OR: así un polígono metido dentro
        /// de otro se comporta como un agujero, que es como Unity los interpreta.
        /// </summary>
        public bool Contiene(Vector2 puntoMundo)
        {
            if (areas == null) return false;

            bool dentro = false;
            foreach (PolygonCollider2D area in areas)
            {
                if (area == null) continue;

                for (int i = 0; i < area.pathCount; i++)
                {
                    if (PuntoEnCamino(area, area.GetPath(i), puntoMundo))
                        dentro = !dentro;
                }
            }
            return dentro;
        }

        /// <summary>
        /// Regla par-impar: se lanza un rayo hacia la derecha y se cuentan los cruces.
        /// Los puntos del camino vienen en el espacio local del collider, así que hay
        /// que pasarlos a mundo con su offset antes de comparar.
        /// </summary>
        private static bool PuntoEnCamino(PolygonCollider2D area, Vector2[] camino, Vector2 punto)
        {
            if (camino == null || camino.Length < 3) return false;

            Transform t = area.transform;
            bool dentro = false;

            Vector2 anterior = t.TransformPoint(camino[camino.Length - 1] + area.offset);
            for (int i = 0; i < camino.Length; i++)
            {
                Vector2 actual = t.TransformPoint(camino[i] + area.offset);

                bool cruzaEnY = (actual.y > punto.y) != (anterior.y > punto.y);
                if (cruzaEnY)
                {
                    float x = (anterior.x - actual.x) * (punto.y - actual.y)
                              / (anterior.y - actual.y) + actual.x;
                    if (punto.x < x) dentro = !dentro;
                }
                anterior = actual;
            }
            return dentro;
        }
    }
}
