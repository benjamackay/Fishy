using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fishy.Net
{
    /// <summary>
    /// HDU-15 — puente entre la mochila de Otto (<see cref="InventoryManager"/>) y
    /// el backend. Mismo papel que <see cref="MisionBackendSync"/> para las misiones.
    ///
    /// Hace dos cosas:
    ///
    ///   1. <b>Al empezar la partida, baja lo que Otto llevaba</b> y lo pone de vuelta
    ///      en la mochila. Es lo que hacía falta para que cerrar el juego dejara de
    ///      vaciar el inventario.
    ///
    ///   2. <b>Después marca la mochila como sucia</b> en <see cref="ColaDeCambios"/>,
    ///      que la sube cuando toca. Recoger tres flores seguidas deja una sola entrada
    ///      en la cola, no tres peticiones: el endpoint manda la mochila entera, así
    ///      que solo importa la última.
    ///
    /// <b>Por qué es un componente aparte y no código dentro de InventoryManager:</b>
    /// por lo mismo que MisionBackendSync — dejar la mochila funcionando igual sin
    /// backend, y que el que sabe de HTTP sea uno solo. Aquí además no hay barrera
    /// de assemblies (InventoryManager vive en Assembly-CSharp), así que es una
    /// decisión de diseño y no una obligación de Unity.
    ///
    /// Se crea solo al cargar la escena; no hay que arrastrar nada al inspector.
    /// </summary>
    public class InventarioBackendSync : MonoBehaviour
    {
        public static InventarioBackendSync Instance { get; private set; }

        /// <summary>Segundos entre reintentos mientras se espera a que haya partida.</summary>
        private const float EsperaEntreIntentos = 0.5f;

        /// <summary>
        /// Lo último que el servidor confirmó. Sirve para no mandar un PUT idéntico al
        /// estado que allá ya existe, que es lo que pasaría al bajar el inventario:
        /// aplicarlo dispara OnInventoryChanged y sin esto se subiría de vuelta lo que
        /// se acaba de recibir.
        /// </summary>
        private string ultimoSubido;

        /// <summary>Tras fallar la bajada se espera esto antes de reintentar.</summary>
        private const float EsperaTrasFallo = 5f;
        private float _noAntesDe;

        private int? partidaAtendida;
        private bool suscrito;
        private bool avisoDeSinPartidaDado;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCrear()
        {
            if (Instance != null) return;
            var go = new GameObject("InventarioBackendSync");
            go.AddComponent<InventarioBackendSync>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            StartCoroutine(EsperarPartida());
        }

        private void OnDisable()
        {
            Desuscribir();
        }

        // ── 1. Bajar lo que Otto llevaba ─────────────────────────────────────

        private IEnumerator EsperarPartida()
        {
            var espera = new WaitForSeconds(EsperaEntreIntentos);

            float sinPartidaDesde = Time.realtimeSinceStartup;

            while (true)
            {
                var api = ApiManager.Instance;

                if (api == null || api.PartidaId == null)
                {
                    // Sin partida no se guarda nada, y antes eso no se decia: el juego
                    // parecia andar bien y la mochila se perdia igual.
                    if (!avisoDeSinPartidaDado &&
                        Time.realtimeSinceStartup - sinPartidaDesde > 8f)
                    {
                        avisoDeSinPartidaDado = true;
                        Debug.LogWarning(
                            "[InventarioBackendSync] Llevo varios segundos sin PartidaId: la mochila " +
                            "NO se va a guardar. Si estas probando, entra por MenuUno para pasar por " +
                            "el login; darle Play directo a la escena de juego no crea partida.");
                    }
                }
                else
                {
                    sinPartidaDesde = Time.realtimeSinceStartup;
                    avisoDeSinPartidaDado = false;

                    if (partidaAtendida != api.PartidaId && Time.realtimeSinceStartup >= _noAntesDe)
                    {
                        partidaAtendida = api.PartidaId;
                        ultimoSubido = null;
                        BajarInventario();
                    }
                }

                yield return espera;
            }
        }

        /// <summary>Pide la mochila de la partida activa y la deja puesta.</summary>
        public void BajarInventario()
        {
            var api = ApiManager.Instance;
            if (api == null) return;

            api.ObtenerInventario(
                onSuccess: Aplicar,
                onError: e =>
                {
                    // Antes se suscribía igual "para que lo que recoja suba cuando se pueda",
                    // pero el PUT del servidor REEMPLAZA la mochila: el primer objeto
                    // recogido subía la mochila local —sin lo guardado— y borraba lo del
                    // servidor. Sin haberlo leído no se escribe; se reintenta la bajada.
                    Debug.LogWarning($"[InventarioBackendSync] No se pudo bajar la mochila: {e}. " +
                                     "No se guardará hasta lograr leerla.");
                    partidaAtendida = null;
                    _noAntesDe = Time.realtimeSinceStartup + EsperaTrasFallo;
                });
        }

        private void Aplicar(List<ItemInventarioDto> guardado)
        {
            var inv = InventoryManager.Instance;

            // Mientras se aplica lo que vino del servidor no se escucha: si no, cada
            // AddItem dispararía una subida de algo que allá ya está.
            Desuscribir();

            inv.Vaciar();

            int puestos = 0, desconocidos = 0;
            if (guardado != null)
            {
                foreach (var fila in guardado)
                {
                    if (fila == null || string.IsNullOrEmpty(fila.item_id)) continue;

                    var data = CatalogoItems.Buscar(fila.item_id);
                    if (data == null)
                    {
                        // El id está en la base pero no hay ItemData con ese itemId. No
                        // se puede dibujar algo que no existe, así que se avisa fuerte:
                        // significa que Unity y la base dejaron de hablar el mismo
                        // idioma, y el objeto le desaparece al niño/a sin explicación.
                        Debug.LogWarning(
                            $"[InventarioBackendSync] '{fila.item_id}' está guardado pero " +
                            "ningún ItemData tiene ese itemId. Revisa Fishy → Revisar ids de objetos.");
                        desconocidos++;
                        continue;
                    }

                    inv.AddItem(data, Mathf.Max(1, fila.cantidad));
                    puestos++;
                }
            }

            ultimoSubido = Firma(LeerMochila());
            Suscribir();

            Debug.Log($"[InventarioBackendSync] Mochila restaurada: {puestos} objeto(s)" +
                      (desconocidos > 0 ? $", {desconocidos} sin ItemData." : "."));
        }

        // ── 2. Subir los cambios, agrupados ──────────────────────────────────

        private void Suscribir()
        {
            if (suscrito) return;
            InventoryManager.Instance.OnInventoryChanged += AlCambiarLaMochila;
            suscrito = true;
        }

        private void Desuscribir()
        {
            if (!suscrito) return;
            if (InventoryManager.instance != null)
                InventoryManager.instance.OnInventoryChanged -= AlCambiarLaMochila;
            suscrito = false;
        }

        private void AlCambiarLaMochila() => MarcarSucio();

        /// <summary>
        /// Pone la mochila en la cola. No sube nada todavía.
        ///
        /// Ya no hace falta la espera de medio segundo que agrupaba las ráfagas: la cola
        /// coalesce por clave hasta el vaciado, que es estrictamente mejor. Recoger tres
        /// flores seguidas deja una sola entrada, no tres peticiones ni una espera.
        ///
        /// Es un snapshot: lo que se encola es la orden de leer la mochila, no la
        /// mochila. Así siempre sube el estado final y no uno intermedio.
        /// </summary>
        public void MarcarSucio()
            => ColaDeCambios.EncolarSnapshot("inventario", Subir, "mochila");

        /// <summary>Nombre viejo de <see cref="MarcarSucio"/>. Lo llaman las pruebas
        /// de editor y algún camino antiguo.</summary>
        public void GuardarAhora() => MarcarSucio();

        /// <summary>
        /// Lo que la cola ejecuta al vaciar. Ver la nota de
        /// <c>PersonajeBackendSync.GuardarPosicion</c> sobre por qué "no hay nada que
        /// hacer" llama a <paramref name="ok"/> y no a <paramref name="error"/>.
        /// </summary>
        private void Subir(Action ok, Action<string> error)
        {
            var api = ApiManager.Instance;
            if (api == null || api.PartidaId == null) { ok(); return; }

            // Sin haber leído la mochila del servidor, la local no es la verdad.
            if (!suscrito) { ok(); return; }

            var items = LeerMochila();
            string firma = Firma(items);

            // Nada que decir: el servidor ya tiene exactamente esto.
            if (firma == ultimoSubido) { ok(); return; }

            api.GuardarInventario(items,
                onSuccess: _ =>
                {
                    ultimoSubido = firma;
                    Debug.Log($"[InventarioBackendSync] Mochila guardada ({items.Count} objeto(s)).");
                    ok();
                },
                onError: e =>
                {
                    Debug.LogWarning($"[InventarioBackendSync] No se pudo guardar la mochila: {e}");
                    error(e);
                });
        }

        // ── Auxiliares ───────────────────────────────────────────────────────

        /// <summary>La mochila actual en el formato que espera el endpoint.</summary>
        private static List<ItemInventarioEnvio> LeerMochila()
        {
            var lista = new List<ItemInventarioEnvio>();
            var inv = InventoryManager.instance;
            if (inv == null) return lista;

            foreach (var item in inv.inventory)
            {
                if (item == null || item.itemData == null) continue;

                string id = item.itemData.itemId;
                if (string.IsNullOrWhiteSpace(id))
                {
                    // Sin id no se puede guardar: se avisa en vez de perderlo callado.
                    Debug.LogWarning(
                        $"[InventarioBackendSync] '{item.itemData.name}' no tiene itemId, " +
                        "así que no se puede guardar. Ponle uno en el asset.");
                    continue;
                }

                lista.Add(new ItemInventarioEnvio(id.Trim(), item.itemQuantity));
            }
            return lista;
        }

        /// <summary>
        /// Texto que representa el contenido de la mochila, para comparar dos estados
        /// sin mirar objeto por objeto. Ordenado, porque el orden de la lista no es
        /// parte del estado: mover una casilla no es un cambio que valga un PUT.
        /// </summary>
        private static string Firma(IEnumerable<ItemInventarioEnvio> items)
        {
            if (items == null) return "";
            return string.Join("|", items
                .Where(i => i != null)
                .Select(i => $"{i.item_id}:{i.cantidad}")
                .OrderBy(s => s));
        }
    }
}
