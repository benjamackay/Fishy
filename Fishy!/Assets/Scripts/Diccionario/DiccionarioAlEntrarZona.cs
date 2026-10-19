using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fishy.World;

namespace Fishy.Diccionario
{
    /// <summary>
    /// Saca el celular con el diccionario la primera vez que Otto entra a una zona.
    ///
    /// Se cuelga del evento <see cref="ZonaActual.OnZonaCambiada"/>, que es el único
    /// sitio del juego que sabe cuándo se cruza un borde. No hace falta ponerlo en
    /// cada zona: basta un objeto con este componente en la escena.
    ///
    /// <b>Por qué espera en vez de abrir al instante.</b> Las zonas se cruzan
    /// caminando, y caminando se puede entrar justo cuando arranca un diálogo, un
    /// chat o la cinemática de desbloqueo. <c>MenuController.PuedeAbrir</c> ya sabe
    /// distinguir eso, así que aquí se reintenta hasta que el camino esté libre en
    /// vez de pisarle la pantalla a otra cosa.
    /// </summary>
    [DisallowMultipleComponent]
    public class DiccionarioAlEntrarZona : MonoBehaviour
    {
        [Header("Referencias (si se dejan vacías se buscan en la escena)")]
        public MenuController menu;
        public PhoneMenuNavigation navegacion;
        public DiccionarioPageUI pagina;

        [Header("Comportamiento")]
        [Tooltip("Segundos de margen antes de sacar el celular, para que el cambio de " +
                 "zona se note antes de que aparezca el panel.")]
        [Min(0f)] public float esperaInicial = 0.6f;

        [Tooltip("Cada cuánto se reintenta mientras haya un diálogo o una cinemática " +
                 "en medio.")]
        [Min(0.1f)] public float reintento = 0.5f;

        [Tooltip("Escribir en consola cada vez que se muestra o se omite.")]
        public bool verboseLogs = true;

        [Header("Salidas que se tapan mientras es obligatorio")]
        [Tooltip("Botones del celular que llevan a otra pantalla y dejarían al niño/a " +
                 "encerrado: con el panel bloqueado no hay X ni Tab, así que saltar a " +
                 "Opciones sería un callejón sin salida. La flecha de volver se tapa " +
                 "sola; aquí va el engranaje (BotonOpciones) y cualquier otro que se " +
                 "agregue después.")]
        public GameObject[] salidasATapar;

        private readonly List<GameObject> _tapados = new List<GameObject>();
        private Coroutine _pendiente;
        private string    _zonaPendiente;
        private bool      _conectado;

        private void OnEnable()
        {
            ZonaActual.OnZonaCambiada  += AlCambiarZona;
            BlockedZone.OnZonaDesbloqueada += AlDesbloquear;
        }

        private void OnDisable()
        {
            ZonaActual.OnZonaCambiada  -= AlCambiarZona;
            BlockedZone.OnZonaDesbloqueada -= AlDesbloquear;
            if (_pendiente != null) { StopCoroutine(_pendiente); _pendiente = null; }

            // Si esto se apaga con el panel bloqueado, los botones tapados se
            // quedarían escondidos para siempre.
            DestaparSalidas();
        }

        private void Start()
        {
            Resolver();
            Conectar();

            // ZonaActual revisa en su primer Update y puede haber disparado el evento
            // antes de que llegáramos a suscribirnos. Sin esto, el diccionario de la
            // zona de partida no saldría nunca.
            var zona = ZonaActual.Instance != null ? ZonaActual.Instance.Actual : "";
            if (!string.IsNullOrEmpty(zona)) AlCambiarZona("", zona);
        }

        private void OnDestroy()
        {
            if (_conectado && pagina != null) pagina.AlEntender -= AlEntender;
        }

        // ── Montaje ──────────────────────────────────────────────────────────────

        private void Resolver()
        {
            if (menu == null) menu = FindAnyObjectByType<MenuController>();

            // Include: la página y su navegación viven dentro del panel, que nace
            // apagado y sólo se enciende al abrir el celular.
            if (navegacion == null)
                navegacion = FindAnyObjectByType<PhoneMenuNavigation>(FindObjectsInactive.Include);
            if (pagina == null)
                pagina = FindAnyObjectByType<DiccionarioPageUI>(FindObjectsInactive.Include);
        }

        private void Conectar()
        {
            if (_conectado || pagina == null) return;
            pagina.AlEntender += AlEntender;
            _conectado = true;
        }

        // ── Entrada a una zona ───────────────────────────────────────────────────

        private void AlCambiarZona(string anterior, string nueva)
        {
            if (string.IsNullOrWhiteSpace(nueva)) return;
            if (nueva == _zonaPendiente) return;
            if (DiccionarioVistos.YaVisto(nueva)) return;

            // La zona se decide con una prueba punto-en-polígono (ZonaActual.ZonaEn),
            // no con el collider de la barrera, así que rozar el borde de una zona
            // cerrada ya cuenta como estar dentro. Sin esto el diccionario se adelanta
            // a la zona: sale el de un sitio al que todavía no se puede entrar.
            if (!ZonaDesbloqueada(nueva))
            {
                if (verboseLogs)
                    Debug.Log($"[Diccionario] '{nueva}' todavía está cerrada; se espera al " +
                              "desbloqueo.", this);
                return;
            }

            if (DiccionarioCatalogo.De(nueva) == null)
            {
                if (verboseLogs)
                    Debug.Log($"[Diccionario] '{nueva}' no tiene entrada en diccionario.json; " +
                              "no se muestra nada.", this);
                return;
            }

            Resolver();
            if (menu == null || navegacion == null || pagina == null)
            {
                Debug.LogWarning("[Diccionario] Falta el panel del celular en la escena; " +
                                 "no se puede mostrar el diccionario de zona.", this);
                return;
            }
            Conectar();

            // Una zona nueva manda sobre la que estaba esperando: si el niño/a cruzó
            // dos bordes seguidos, lo que importa es dónde está ahora.
            if (_pendiente != null) StopCoroutine(_pendiente);
            _zonaPendiente = nueva;
            _pendiente = StartCoroutine(Mostrar(nueva));
        }

        /// <summary>
        /// Una zona que se acaba de abrir puede ser la misma en la que Otto ya estaba
        /// "parado" según el polígono, y entonces no habrá ningún cambio de zona que
        /// dispare el diccionario. Se vuelve a mirar dónde está.
        /// </summary>
        private void AlDesbloquear(BlockedZone zona)
        {
            string donde = ZonaDeOtto();
            if (!string.IsNullOrEmpty(donde)) AlCambiarZona("", donde);
        }

        private IEnumerator Mostrar(string zona)
        {
            if (esperaInicial > 0f) yield return new WaitForSeconds(esperaInicial);

            // Reintenta hasta que no haya nada en medio. Sin tope a propósito: si el
            // niño/a entró a la zona y se quedó en un diálogo largo, el diccionario
            // tiene que salir al terminar, no perderse.
            while (!menu.AbrirModal())
            {
                if (DiccionarioVistos.YaVisto(zona)) { Limpiar(); yield break; }

                // Si mientras esperábamos se fue a otra parte, este diccionario ya no
                // es el de donde está: sacarlo ahora sería hablarle de un sitio que
                // dejó atrás.
                if (ZonaDeOtto() != zona) { Limpiar(); yield break; }

                yield return new WaitForSeconds(reintento);
            }

            // El panel se enciende al final del frame, dentro de MenuController.Open,
            // y al encenderse PhoneMenuNavigation.OnEnable lo manda al inicio. Por eso
            // la página se pide después, no antes. Mientras tanto el panel todavía
            // está fuera de pantalla, así que no se alcanza a ver el inicio.
            yield return new WaitUntil(() => menu.menuCanvas != null && menu.menuCanvas.activeSelf);

            int indice = IndiceDePagina();
            if (indice < 0)
            {
                Debug.LogWarning("[Diccionario] La página no está en la lista 'pages' de " +
                                 "PhoneMenuNavigation; se suelta el panel.", this);
                DestaparSalidas();
                menu.SoltarModal();
                Limpiar();
                yield break;
            }

            navegacion.ShowPage(indice);

            // Después de ShowPage, no antes, y las dos por el mismo motivo: ShowPage
            // es quien enciende la página y la flecha de volver. Si la página ya venía
            // encendida de una visita anterior, su OnEnable se adelantó y la pintó como
            // visita opcional; este Mostrar repinta encima, en el mismo frame.
            pagina.Mostrar(zona, true);
            TaparSalidas();

            if (verboseLogs) Debug.Log($"[Diccionario] Mostrando el diccionario de '{zona}'.", this);
            _pendiente = null;
        }

        // ── Salidas ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Esconde todo lo que saque de esta pantalla. Sin esto el bloqueo es de
        /// mentira: la flecha de volver lleva al inicio y el engranaje a Opciones, y
        /// desde cualquiera de las dos ya no hay botón de cerrar con el que salir.
        /// </summary>
        private void TaparSalidas()
        {
            DestaparSalidas();

            if (navegacion != null && navegacion.backButton != null)
                Tapar(navegacion.backButton.gameObject);

            if (salidasATapar == null) return;
            foreach (var go in salidasATapar) Tapar(go);
        }

        private void Tapar(GameObject go)
        {
            // Se anota sólo lo que estaba encendido, para devolverlo tal como estaba.
            if (go == null || !go.activeSelf) return;
            go.SetActive(false);
            _tapados.Add(go);
        }

        private void DestaparSalidas()
        {
            foreach (var go in _tapados) if (go != null) go.SetActive(true);
            _tapados.Clear();
        }

        /// <summary>
        /// Se busca en vez de guardarse en el inspector para que mover la página de
        /// sitio en la lista no deje esto apuntando a Opciones.
        /// </summary>
        private int IndiceDePagina()
        {
            if (navegacion == null || navegacion.pages == null) return -1;
            for (int i = 0; i < navegacion.pages.Length; i++)
                if (navegacion.pages[i] == pagina.gameObject) return i;
            return -1;
        }

        // ── Salida ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Pulsó "Entendido". Se anota aquí y no al abrir: si el juego se cierra con
        /// la pantalla puesta, el diccionario tiene que volver a salir.
        /// </summary>
        private void AlEntender(string zona)
        {
            if (!string.IsNullOrWhiteSpace(zona)) DiccionarioVistos.Marcar(zona);
            DestaparSalidas();
            if (menu != null) menu.SoltarModal();
            Limpiar();
        }

        private void Limpiar()
        {
            _pendiente = null;
            _zonaPendiente = null;
        }

        private static string ZonaDeOtto()
        {
            var zona = ZonaActual.Instance;
            if (zona == null) zona = FindAnyObjectByType<ZonaActual>();
            return zona != null ? zona.Actual : "";
        }

        /// <summary>
        /// Una zona sin <see cref="BlockedZone"/> cuenta como abierta: así es la zona
        /// de partida, que no tiene barrera porque nunca estuvo cerrada.
        /// </summary>
        private static bool ZonaDesbloqueada(string zonaId)
        {
            foreach (var barrera in FindObjectsByType<BlockedZone>())
            {
                if (barrera == null) continue;
                if (!string.Equals(barrera.zoneId, zonaId, StringComparison.OrdinalIgnoreCase)) continue;
                return !barrera.isLocked;
            }
            return true;
        }
    }
}
