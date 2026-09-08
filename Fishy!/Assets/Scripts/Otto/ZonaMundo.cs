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
