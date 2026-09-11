using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fishy.Net
{
    /// <summary>
    /// HDU-15 — deja a Otto donde estaba, y va guardando dónde está.
    ///
    /// Sin esto, `OttoController.Start()` lo pone en el <c>spawnPoint</c> de la
    /// escena y el niño/a reaparece en la entrada del mapa cada vez que vuelve.
    ///
    /// <b>Dos cuidados que no tienen las misiones ni el inventario:</b>
    ///
    ///   1. <b>La carrera del arranque.</b> Otto se coloca en su `Start()`, mucho
    ///      antes de que conteste el servidor. Así que no se puede "poner la
    ///      posición al cargar": hay que esperar la respuesta y recién ahí moverlo.
    ///      Y si para entonces el niño/a ya empezó a caminar, <b>no se le mueve</b>:
    ///      devolverlo de un salto a donde estaba ayer sería peor que no restaurar.
    ///      Ver <see cref="RadioParaConsiderarQueNoSeHaMovido"/>.
    ///
    ///   2. <b>La posición cambia todos los frames.</b> Guardar en cada uno sería un
    ///      PATCH por frame, así que no se guarda por su cuenta: lo pide
    ///      <see cref="SaveManager"/> en los dos momentos que quedan —cambiar de zona
    ///      y cerrar el juego—, más las señales de cierre de aquí abajo.
    ///
    ///      Hubo un bucle propio que subía la posición cada 5 s si Otto se había
    ///      movido medio metro. Se quitó junto con el resto de guardados por tiempo.
    ///      <b>Cambia lo que se pierde en un cierre sucio</b> (batería, crash, el
    ///      sistema matando la app): antes eran metros, ahora es todo lo caminado
    ///      desde el último cambio de zona. Es una decisión tomada, no un descuido.
    /// </summary>
    public class PersonajeBackendSync : MonoBehaviour
    {
        public static PersonajeBackendSync Instance { get; private set; }

        /// <summary>Segundos entre reintentos mientras se espera a que haya partida.</summary>
        private const float EsperaEntreIntentos = 0.5f;

        /// <summary>
        /// Si al llegar la respuesta Otto sigue a menos de esto del punto donde
        /// apareció, se asume que el niño/a todavía no toma el control y se le puede
        /// mover. Más lejos significa que ya está jugando: ahí se respeta.
        /// </summary>
        private const float RadioParaConsiderarQueNoSeHaMovido = 1.5f;

        /// <summary>Segundos sin PartidaId antes de avisar que no se va a guardar nada.</summary>
        private const float SegundosAntesDeAvisarQueNoHayPartida = 8f;

        private int? partidaAtendida;

        /// <summary>
        /// Escena en la que se restauró. Va junto a la partida porque restaurar no
        /// depende solo de que haya partida: depende de que exista Otto, y Otto solo
        /// existe en la escena de juego. Ver <see cref="EsperarPartidaYOtto"/>.
        /// </summary>
        private string escenaAtendida;

        /// <summary>
        /// La posición que trajo el servidor, pedida ya en el menú. Tenerla ANTES de
        /// que cargue la escena es lo que evita el salto: si hubiera que esperar la
        /// respuesta con Otto ya en pantalla, se le ve aparecer en el spawnPoint y
        /// teletransportarse. Pidiéndola antes, se coloca en su primer frame.
        /// </summary>
        private PersonajeDto enCache;
        private int? partidaPrecargada;

        /// <summary>
        /// La pone <see cref="AlCargarEscena"/> y la consume <c>LateUpdate</c> del mismo
        /// frame. No se puede colocar a Otto en el propio evento de carga porque
        /// <c>OttoController.Start()</c> corre DESPUÉS y lo pisaría con el spawnPoint.
        /// LateUpdate va después de Start y antes de que se dibuje el frame, así que
        /// ese primer dibujo ya lo muestra en su sitio.
        /// </summary>
        private bool colocarEnLateUpdate;
        private int framesEsperandoCache;

        /// <summary>
        /// Cuántos frames se acepta esperar la respuesta con Otto ya en pantalla antes
        /// de rendirse. A 60 fps son unos 0,5 s: más que eso y conviene dejar que lo
        /// haga el bucle, porque el niño/a ya empezó a jugar.
        /// </summary>
        private const int MaxFramesEsperandoCache = 30;

        private bool avisoDeSinPartidaDado;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCrear()
        {
            if (Instance != null) return;
            var go = new GameObject("PersonajeBackendSync");
            go.AddComponent<PersonajeBackendSync>();
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
            SceneManager.sceneLoaded += AlCargarEscena;
            StartCoroutine(EsperarPartidaYOtto());
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= AlCargarEscena;
        }

        private void AlCargarEscena(Scene escena, LoadSceneMode modo)
        {
            // Solo se marca. Colocar aquí no serviría: Start() de Otto corre después.
            colocarEnLateUpdate = true;
        }

        private void LateUpdate()
        {
            if (!colocarEnLateUpdate) return;

            var otto = BuscarOtto();
            if (otto == null) { colocarEnLateUpdate = false; return; }   // escena de menú

            var api = ApiManager.Instance;
            if (api == null || api.PartidaId == null) { colocarEnLateUpdate = false; return; }

            // La respuesta todavía no llega. NO se consume la bandera: se reintenta en
            // cada frame durante un rato corto, porque cada frame que se espera es un
            // frame con Otto dibujado en el spawnPoint. Si se agota, el bucle de medio
            // segundo lo resuelve igual, con salto visible.
            if (enCache == null)
            {
                framesEsperandoCache++;
                if (framesEsperandoCache > MaxFramesEsperandoCache)
                {
                    colocarEnLateUpdate = false;
                    Debug.LogWarning("[PersonajeBackendSync] La posición no llegó a tiempo para el " +
                                     "primer frame: si se restaura ahora se va a ver el salto.");
                }
                return;
            }

            colocarEnLateUpdate = false;
            framesEsperandoCache = 0;

            string escena = SceneManager.GetActiveScene().name;
            if (partidaAtendida == api.PartidaId && escenaAtendida == escena) return;

            partidaAtendida = api.PartidaId;
            escenaAtendida = escena;

            // `dondeAparecio` es la posición actual: acaba de correr Start(), así que
            // Otto está en el spawnPoint y todavía nadie lo movió. La comprobación de
            // "ya se movió" pasa trivialmente, que es lo correcto en este camino.
            Aplicar(enCache, otto.transform.position);
        }

        /// <summary>
        /// Sube dónde está Otto ahora mismo. Es la única vía: este componente ya no
        /// guarda por su cuenta, así que si nadie llama aquí, no se guarda.
        ///
        /// La usa <see cref="SaveManager"/> en el cambio de zona y en el cierre.
        /// Manda la posición siempre, se haya movido Otto o no: los momentos que
        /// quedan son de los que puede no haber otro después.
        /// </summary>
        public void GuardarAhora() => GuardarPosicion();

        private void OnApplicationQuit()
        {
            // Último guardado antes de cerrar, best-effort: si el proceso muere antes
            // de que salga la petición, se pierde. El menú de pausa ("Guardar y salir")
            // es el camino bueno, porque ahí sí se le da un respiro a la petición.
            GuardarPosicion();
        }

        private void OnApplicationPause(bool pausado)
        {
            // En móvil, `OnApplicationQuit` muchas veces no llega: el sistema mata la
            // app pausada sin avisar. Esta es la única señal fiable de "se va".
            if (pausado) GuardarPosicion();
        }

        // ── 1. Restaurar ─────────────────────────────────────────────────────

        /// <summary>
        /// Espera a que haya partida <b>y además Otto en la escena</b>, y recién ahí
        /// restaura.
        ///
        /// Esperar solo la partida no alcanza, y es el error que costó una tarde: la
        /// <c>PartidaId</c> se fija en la escena <c>Ingresar</c>, donde Otto todavía
        /// no existe —vive en la escena de juego, que carga después—. Si se marcara
        /// la partida como atendida ahí, se restauraría contra un Otto inexistente y
        /// nunca se volvería a intentar, porque la partida ya no cambia. Por eso la
        /// condición incluye la escena: mientras no haya a quién mover, se sigue
        /// esperando, y volver a entrar al juego vuelve a restaurar.
        /// </summary>
        private IEnumerator EsperarPartidaYOtto()
        {
            var espera = new WaitForSeconds(EsperaEntreIntentos);
            float sinPartidaDesde = Time.realtimeSinceStartup;

            while (true)
            {
                var api = ApiManager.Instance;

                if (api == null || api.PartidaId == null)
                {
                    // El silencio es lo que más ha costado en este proyecto: sin partida
                    // no se guarda nada y antes no se decía. Se avisa una vez.
                    if (!avisoDeSinPartidaDado &&
                        Time.realtimeSinceStartup - sinPartidaDesde > SegundosAntesDeAvisarQueNoHayPartida)
                    {
                        avisoDeSinPartidaDado = true;
                        Debug.LogWarning(
                            "[PersonajeBackendSync] Llevo varios segundos sin PartidaId: la posición de " +
                            "Otto NO se va a guardar. Si estás probando, entra por MenuUno para pasar " +
                            "por el login; darle Play directo a la escena de juego no crea partida.");
                    }
                }
                else
                {
                    sinPartidaDesde = Time.realtimeSinceStartup;
                    avisoDeSinPartidaDado = false;

                    // Se pide apenas hay partida, sin esperar a Otto: normalmente eso
                    // ocurre en la pantalla de ingreso, varios segundos antes de que
                    // cargue el juego, así que la respuesta llega a tiempo para
                    // colocarlo en su primer frame y no se ve ningún salto.
                    if (partidaPrecargada != api.PartidaId)
                    {
                        partidaPrecargada = api.PartidaId;
                        enCache = null;
                        Precargar();
                    }

                    string escena = SceneManager.GetActiveScene().name;
                    bool yaHecho = partidaAtendida == api.PartidaId && escenaAtendida == escena;

                    if (!yaHecho && enCache != null && BuscarOtto() != null)
                    {
                        partidaAtendida = api.PartidaId;
                        escenaAtendida = escena;
                        Restaurar();
                    }
                }

                yield return espera;
            }
        }

        /// <summary>Pide la posición y la deja en <see cref="enCache"/>, sin aplicarla.</summary>
        private void Precargar()
        {
            var api = ApiManager.Instance;
            if (api == null) return;

            api.ObtenerPersonaje(
                onSuccess: dto => enCache = dto ?? new PersonajeDto { tiene_posicion = false },
                onError: e =>
                {
                    Debug.LogWarning($"[PersonajeBackendSync] No se pudo saber dónde quedó Otto: {e}");
                    // Se deja un DTO vacío en vez de null para no reintentar en bucle:
                    // sin posición, Otto se queda en el spawnPoint, que es correcto.
                    enCache = new PersonajeDto { tiene_posicion = false };
                });
        }

        /// <summary>
        /// Camino tardío: la escena ya cargó y la respuesta todavía no había llegado.
        /// Aquí sí se puede ver el salto, pero es lo mejor disponible — la alternativa
        /// sería no restaurar. El caso normal lo resuelve <c>LateUpdate</c>.
        /// </summary>
        private void Restaurar()
        {
            var otto = BuscarOtto();
            if (otto == null || enCache == null) return;
            Aplicar(enCache, otto.transform.position);
        }

        private void Aplicar(PersonajeDto dto, Vector2 dondeAparecio)
        {
            var otto = BuscarOtto();

            if (otto == null)
            {
                // El bucle solo llama aquí cuando Otto existe, así que llegar sin él
                // significa que lo destruyeron entre la petición y la respuesta (un
                // cambio de escena a mitad de camino). Se deshace lo marcado para que
                // el bucle vuelva a intentarlo cuando Otto reaparezca.
                Debug.LogWarning("[PersonajeBackendSync] Otto desapareció mientras se " +
                                 "pedía su posición. Se reintentará.");
                escenaAtendida = null;
                return;
            }

            string escenaActual = SceneManager.GetActiveScene().name;

            if (dto == null || !dto.tiene_posicion)
            {
                Debug.Log("[PersonajeBackendSync] Sin posición guardada: Otto se queda en el spawnPoint.");
            }
            else if (!string.IsNullOrEmpty(dto.escena) && dto.escena != escenaActual)
            {
                // Restaurar unas coordenadas de otra escena dejaría a Otto dentro de
                // un cerro o fuera del mapa.
                Debug.LogWarning(
                    $"[PersonajeBackendSync] La posición guardada es de '{dto.escena}' y esta escena " +
                    $"es '{escenaActual}'. Se ignora y Otto se queda en el spawnPoint.");
            }
            else if (Vector2.Distance(otto.transform.position, dondeAparecio) > RadioParaConsiderarQueNoSeHaMovido)
            {
                // La respuesta llegó tarde y el niño/a ya está jugando. Moverlo ahora
                // sería un tirón hacia atrás en medio del juego.
                Debug.LogWarning(
                    "[PersonajeBackendSync] Otto ya se movió antes de que llegara la respuesta: " +
                    "no se restaura la posición para no dar un salto en medio del juego.");
            }
            else
            {
                var destino = new Vector3(dto.pos_x.Value, dto.pos_y.Value, otto.transform.position.z);
                otto.TeleportTo(destino);
                Debug.Log($"[PersonajeBackendSync] Otto restaurado en ({destino.x:F1}, {destino.y:F1}).");
            }
        }

        // ── 2. Guardar ───────────────────────────────────────────────────────

        private void GuardarPosicion()
        {
            var api = ApiManager.Instance;
            if (api == null || api.PartidaId == null) return;

            var otto = BuscarOtto();
            if (otto == null) return;   // escena de menú: no hay nada que guardar

            Vector2 ahora = otto.transform.position;
            string escena = SceneManager.GetActiveScene().name;

            // La zona viaja en la MISMA peticion que la posicion, no en otra: asi no
            // pueden contradecirse ni queda una a medias si la segunda no sale.
            api.GuardarPersonaje(escena, ahora.x, ahora.y, ZonaDeOtto(),
                onError: e => Debug.LogWarning($"[PersonajeBackendSync] No se pudo guardar dónde está Otto: {e}"));
        }

        /// <summary>
        /// En que zona esta Otto, para mandarla junto a la posicion.
        ///
        /// Por el camino normal la pone <see cref="SaveManager"/>: su Guardar() llama
        /// a GuardarZona() —que recalcula con Revisar()— justo antes de pedirnos que
        /// subamos, asi que <c>ZonaGuardada</c> es lo mas fresco que hay.
        ///
        /// Pero <see cref="GuardarPosicion"/> tambien se llama directo desde
        /// OnApplicationQuit y OnApplicationPause, sin pasar por SaveManager, y por
        /// ahi ZonaGuardada seria la del guardado anterior. De ahi el respaldo.
        ///
        /// El respaldo lee <c>Actual</c> y NO llama a <c>Revisar()</c> a proposito:
        /// Revisar dispara OnZonaCambiada, y eso engancharia otro guardado entero
        /// justo mientras se esta cerrando el juego. Actual se refresca solo cada
        /// 0,25 s, que para esto sobra.
        /// </summary>
        private static string ZonaDeOtto()
        {
            var save = Fishy.World.SaveManager.Instance;
            if (save != null && !string.IsNullOrEmpty(save.ZonaGuardada)) return save.ZonaGuardada;

            var zonas = Fishy.World.ZonaActual.Instance;
            return zonas != null ? zonas.Actual : null;
        }

        // ── Auxiliares ───────────────────────────────────────────────────────

        /// <summary>
        /// El Otto de la escena, o null si no hay (los menús no tienen). Se busca
        /// cada vez en vez de guardarlo: este componente es DontDestroyOnLoad y el
        /// Otto de la escena anterior queda destruido al cambiar de escena.
        /// </summary>
        private static Fishy.World.OttoController BuscarOtto()
        {
            return FindAnyObjectByType<Fishy.World.OttoController>();
        }
    }
}
