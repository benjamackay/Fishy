using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fishy.Album
{
    /// <summary>
    /// El álbum de evidencias de esta partida (HDU-12): qué señales de riesgo marcó
    /// bien el niño/a en los casos del Modo Detective, con su explicación y el
    /// contexto del caso.
    ///
    /// <b>Por qué la verdad está acá y no en el servidor.</b> El backend sí guarda
    /// el progreso de cada caso, pero <c>POST /casos-detective/{caso}/progreso/</c>
    /// <b>pisa</b> <c>mensajes_marcados</c> con el último intento
    /// (<c>api/views.py</c>, <c>registrar_progreso_detective</c>: en la rama
    /// <c>if not created</c> hace <c>progreso.mensajes_marcados = marcados</c>). Un
    /// álbum que leyera de ahí se comportaría así: el niño/a clava las 4 señales del
    /// caso 01 y gana 4 evidencias; más tarde vuelve a jugar el caso —porque le
    /// gustó, o para mostrárselo a alguien— y marca solo 1; el servidor se queda con
    /// esa única marca, y al reinstalar el juego el álbum le muestra 1 evidencia en
    /// vez de 4. Le habríamos borrado algo que se ganó.
    ///
    /// Así que el álbum <b>crece por unión y nunca borra</b>: cada intento suma lo
    /// que acertó y no resta nada. Vive en PlayerPrefs, separado por partida, y lo
    /// que llega del servidor es una <i>semilla</i> para el dispositivo que todavía
    /// no tiene nada local — no una fuente de verdad. El servidor no se toca: con los
    /// campos que ya existen alcanza.
    ///
    /// <b>Separado por partida</b>, igual que <c>HistorialDeObjetivos</c> y
    /// <c>MissionManager</c>, y por el mismo motivo: sin separar, las evidencias de
    /// un hermano se le aparecerían al otro en el mismo computador.
    /// </summary>
    public static class AlbumDeEvidencias
    {
        public const string Prefijo = "Fishy.Album.";
        private const string NombreEvidencias = "Evidencias";

        /// <summary>Versión del formato guardado. Se escribe siempre y se lee de
        /// forma tolerante; ver <c>AlbumGuardado</c>.</summary>
        public const int VersionFormato = 1;

        /// <summary>Clave del grupo de las evidencias que no tienen etiqueta. No es
        /// una etiqueta del banco: empieza con "__" justamente para no poder chocar
        /// con una.</summary>
        public const string ClaveSinTematica = "__sin_tematica";

        /// <summary>Las evidencias en orden de registro. La lista es el orden; el
        /// diccionario es solo para buscar por clave sin recorrerla.</summary>
        private static readonly List<EvidenciaAlbum> _evidencias = new List<EvidenciaAlbum>();
        private static readonly Dictionary<string, EvidenciaAlbum> _porClave =
            new Dictionary<string, EvidenciaAlbum>();

        /// <summary>Partida de la que son estos datos. Null = todavía no se ató
        /// ninguna, y entonces nada se escribe a disco.</summary>
        private static int? _partida;

        /// <summary>Avisa de que el contenido cambió, para que la pantalla abierta se
        /// repinte sola.</summary>
        public static event Action AlCambiar;

        /// <summary>
        /// El editor conserva los estáticos entre una corrida de Play y la siguiente.
        /// Sin esto, el álbum de la sesión anterior seguiría en memoria —con sus
        /// suscriptores ya destruidos— y lo que se vería sería una mezcla de dos
        /// partidas. Lo de disco no se toca: <see cref="ConfigurarParaPartida"/> lo
        /// vuelve a leer.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void LimpiarEstadoEstatico()
        {
            _evidencias.Clear();
            _porClave.Clear();
            _partida = null;
            AlCambiar = null;
        }

        // ── Partida ───────────────────────────────────────────────────────────

        /// <summary>
        /// Ata el álbum a una partida y carga lo suyo. Llamarla dos veces con la
        /// misma no hace nada; con otra, descarta lo anterior.
        ///
        /// La llama <c>MisionBackendSync.AtarProgresoALaPartida</c>, junto al resto de
        /// los sistemas que separan el avance por perfil.
        ///
        /// <b>Lo anotado antes de tener partida es de ESTA.</b> Entre que arranca la
        /// escena y que llega el <c>PartidaId</c> hay segundos en los que el niño/a ya
        /// puede haber terminado un caso —los <c>DetectiveLauncher</c> consultan el
        /// progreso en su <c>Start()</c>—, y tirar eso sería el mismo agujero que este
        /// álbum viene a tapar. Pasar de una partida a OTRA sí descarta: es lo que
        /// evita que las evidencias de un hermano le aparezcan al siguiente.
        /// </summary>
        public static void ConfigurarParaPartida(int partidaId)
        {
            if (partidaId <= 0 || _partida == partidaId) return;

            bool veniaSinPartida = _partida == null;

            _partida = partidaId;
            Cargar(conservarLoDeMemoria: veniaSinPartida);

            // Lo que venía en memoria todavía no estaba en disco: ahora que hay
            // partida a la que atarlo, se guarda junto con lo que ya había.
            if (veniaSinPartida) Guardar();

            if (_evidencias.Count > 0)
                Debug.Log($"[Álbum] Partida {partidaId}: {_evidencias.Count} evidencia(s), " +
                          $"{CantidadIncompletas} sin completar.");

            AlCambiar?.Invoke();
        }

        // ── Escribir ──────────────────────────────────────────────────────────
        //
        // Son cuatro verbos y no un solo "guardar esto" porque la política de
        // creación es distinta en cada caso, y mezclarlas es justo el error que deja
        // el álbum premiando lo que no debe:
        //
        //   Registrar / RegistrarVarias  crean y completan  (lo que ocurrió acá)
        //   SembrarIds                   crea esqueletos    (lo que dice el servidor)
        //   ConciliarConElCaso           completa y purga, NUNCA crea
        //
        // Con un único Upsert, una respuesta del servidor podría INVENTAR una
        // evidencia que la regla local nunca habría dado. Ver ConciliarConElCaso.

        /// <summary>
        /// Registra una evidencia, o rellena los huecos de la que ya estaba.
        /// Idempotente por (casoId, mensajeId): repetir el caso no duplica nada.
        /// Devuelve true si algo cambió.
        /// </summary>
        public static bool Registrar(EvidenciaAlbum evidencia)
        {
            bool cambio = Fusionar(evidencia, crear: true);
            if (cambio) GuardarYAvisar();
            return cambio;
        }

        /// <summary>Registra varias de una vez (un guardado, un aviso). Devuelve
        /// cuántas cambiaron.</summary>
        public static int RegistrarVarias(IEnumerable<EvidenciaAlbum> evidencias)
        {
            if (evidencias == null) return 0;

            int cambios = 0;
            foreach (EvidenciaAlbum e in evidencias)
                if (Fusionar(e, crear: true)) cambios++;

            if (cambios > 0) GuardarYAvisar();
            return cambios;
        }

        /// <summary>
        /// Siembra desde el servidor, que solo guarda ids: crea los esqueletos que
        /// falten y <b>no toca ni borra nada</b> de lo que ya hubiera. Devuelve
        /// cuántos esqueletos nuevos quedaron.
        ///
        /// Sembrar el mismo caso dos veces no hace nada: la respuesta del servidor
        /// la piden todos los <c>DetectiveLauncher</c> de la escena, y acá solo se
        /// añaden los pares que faltan.
        /// </summary>
        public static int SembrarIds(string casoId, IEnumerable<string> mensajeIds)
        {
            if (string.IsNullOrWhiteSpace(casoId) || mensajeIds == null) return 0;

            long ahora = DateTime.UtcNow.Ticks;
            int nuevas = 0;
            foreach (string id in mensajeIds)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;

                var esqueleto = new EvidenciaAlbum
                {
                    casoId = casoId,
                    mensajeId = id,
                    registradaEn = ahora,
                };
                if (Fusionar(esqueleto, crear: true)) nuevas++;
            }

            if (nuevas > 0) GuardarYAvisar();
            return nuevas;
        }

        /// <summary>
        /// Con los datos reales del caso en la mano: rellena los esqueletos de ese
        /// caso y descarta los que no correspondan a una señal válida. <b>No crea
        /// evidencias nuevas.</b> Devuelve cuántas cambiaron o se descartaron.
        ///
        /// <b>Por qué hace falta purgar.</b> El servidor guarda
        /// <c>mensajes_marcados</c> tal cual llega, y ahí puede venir un mensaje
        /// <i>ambiguo</i>: el niño/a lo marcó, y el servidor no lo filtra porque solo
        /// lo usa para corregir. El álbum, en cambio, no premia el ambiguo (ver
        /// <c>AlbumDetectivePuente.EsSenalValida</c>). Si la semilla creara la
        /// evidencia y nadie la revisara, en un dispositivo nuevo el álbum enseñaría
        /// lo contrario de lo que enseña el puntaje.
        ///
        /// <b>Pero solo se descarta lo que sigue incompleto.</b> Una evidencia con
        /// texto se ganó en este dispositivo, jugando: esa no se borra nunca, por
        /// nada. Perder evidencias es el único resultado inaceptable acá.
        /// </summary>
        public static int ConciliarConElCaso(string casoId, IEnumerable<EvidenciaAlbum> senalesDelCaso)
        {
            if (string.IsNullOrWhiteSpace(casoId)) return 0;
            casoId = casoId.Trim();

            var validas = new Dictionary<string, EvidenciaAlbum>();
            if (senalesDelCaso != null)
            {
                foreach (EvidenciaAlbum s in senalesDelCaso)
                {
                    if (s == null || string.IsNullOrWhiteSpace(s.mensajeId)) continue;
                    validas[s.mensajeId.Trim()] = s;
                }
            }

            int cambios = 0;

            // Al revés, que es como se puede quitar de una lista mientras se recorre.
            for (int i = _evidencias.Count - 1; i >= 0; i--)
            {
                EvidenciaAlbum e = _evidencias[i];
                if (!string.Equals(e.casoId, casoId, StringComparison.Ordinal)) continue;
                if (validas.ContainsKey(e.mensajeId)) continue;
                if (e.EstaCompleta) continue;

                _porClave.Remove(e.Clave);
                _evidencias.RemoveAt(i);
                cambios++;
            }

            foreach (KeyValuePair<string, EvidenciaAlbum> par in validas)
            {
                string clave = EvidenciaAlbum.Clavear(casoId, par.Key);
                if (!_porClave.TryGetValue(clave, out EvidenciaAlbum guardada)) continue;
                if (RellenarHuecos(guardada, par.Value)) cambios++;
            }

            if (cambios > 0) GuardarYAvisar();
            return cambios;
        }

        // ── Leer ──────────────────────────────────────────────────────────────

        public static int Cantidad => _evidencias.Count;

        /// <summary>Cuántas son todavía esqueletos (ids sin texto), para saber si
        /// vale la pena intentar completarlas.</summary>
        public static int CantidadIncompletas => _evidencias.Count(e => !e.EstaCompleta);

        public static bool Contiene(string casoId, string mensajeId)
            => _porClave.ContainsKey(EvidenciaAlbum.Clavear(casoId, mensajeId));

        /// <summary>Todas, en orden de registro.</summary>
        public static IReadOnlyList<EvidenciaAlbum> Todas() => _evidencias;

        /// <summary>Los casos que tienen al menos un esqueleto. Es la lista de los
        /// que conviene intentar completar con los datos del caso.</summary>
        public static List<string> CasosConEsqueletos()
        {
            return _evidencias
                .Where(e => !e.EstaCompleta)
                .Select(e => e.casoId)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Agrupadas por caso (CA2). Los grupos salen en el orden en que se empezó
        /// cada caso —la evidencia más vieja de cada uno—, y dentro de cada grupo las
        /// evidencias van en orden de registro.
        /// </summary>
        public static List<GrupoAlbum> PorCaso()
        {
            var grupos = new List<GrupoAlbum>();
            var porClave = new Dictionary<string, GrupoAlbum>();

            foreach (EvidenciaAlbum e in _evidencias)
            {
                if (!porClave.TryGetValue(e.casoId, out GrupoAlbum grupo))
                {
                    grupo = new GrupoAlbum { clave = e.casoId, titulo = e.TituloVisible };
                    porClave[e.casoId] = grupo;
                    grupos.Add(grupo);
                }

                // El título y la zona los pone la primera evidencia que los traiga:
                // un esqueleto no los tiene, y el grupo no tiene por qué quedarse sin
                // nombre solo porque la primera que llegó viniera del servidor.
                if (string.IsNullOrWhiteSpace(grupo.subtitulo) && !string.IsNullOrWhiteSpace(e.casoZona))
                    grupo.subtitulo = e.casoZona;
                if (!string.IsNullOrWhiteSpace(e.casoTitulo)) grupo.titulo = e.casoTitulo;

                grupo.evidencias.Add(e);
            }

            foreach (GrupoAlbum g in grupos) g.subtitulo = Subtitulo(g.subtitulo, g.Cantidad);
            return grupos;
        }

        /// <summary>
        /// Agrupadas por temática (CA2): el "tipo de señal", que son las
        /// <c>etiquetas_ml</c> del caso.
        ///
        /// Una evidencia con dos etiquetas <b>sale en los dos grupos</b>: el caso del
        /// Puma enseña a la vez sobre grooming y sobre privacidad, y esconder una de
        /// las dos lecturas para no repetir la ficha sería peor. La que no tenga
        /// ninguna etiqueta cae en <see cref="ClaveSinTematica"/> y nunca desaparece
        /// de la pantalla.
        /// </summary>
        public static List<GrupoAlbum> PorTematica()
        {
            var porClave = new Dictionary<string, GrupoAlbum>();

            foreach (EvidenciaAlbum e in _evidencias)
            {
                bool tiene = false;
                if (e.etiquetas != null)
                {
                    foreach (string etiqueta in e.etiquetas)
                    {
                        if (string.IsNullOrWhiteSpace(etiqueta)) continue;
                        tiene = true;
                        Meter(porClave, etiqueta.Trim(), e);
                    }
                }

                if (!tiene) Meter(porClave, ClaveSinTematica, e);
            }

            // Alfabético, y "sin clasificar" al final: es un cajón de sastre, no una
            // temática, y arriba daría la impresión de que el álbum está desordenado.
            List<GrupoAlbum> grupos = porClave.Values
                .Where(g => g.clave != ClaveSinTematica)
                .OrderBy(g => g.clave, StringComparer.Ordinal)
                .ToList();

            if (porClave.TryGetValue(ClaveSinTematica, out GrupoAlbum sueltas))
                grupos.Add(sueltas);

            foreach (GrupoAlbum g in grupos) g.subtitulo = Subtitulo(null, g.Cantidad);
            return grupos;
        }

        /// <summary>Se olvida todo, incluido lo guardado de esta partida. Para las
        /// pruebas y los arneses.</summary>
        public static void Limpiar()
        {
            if (_partida != null)
            {
                PlayerPrefs.DeleteKey(Clave());
                PlayerPrefs.Save();
            }

            _evidencias.Clear();
            _porClave.Clear();
            _partida = null;

            AlCambiar?.Invoke();
        }

        // ── Dentro ────────────────────────────────────────────────────────────

        private static void Meter(Dictionary<string, GrupoAlbum> donde, string clave, EvidenciaAlbum e)
        {
            if (!donde.TryGetValue(clave, out GrupoAlbum grupo))
            {
                grupo = new GrupoAlbum { clave = clave, titulo = clave };
                donde[clave] = grupo;
            }
            grupo.evidencias.Add(e);
        }

        private static string Subtitulo(string zona, int cuantas)
        {
            string cuenta = cuantas == 1 ? "1 evidencia" : $"{cuantas} evidencias";
            return string.IsNullOrWhiteSpace(zona) ? cuenta : $"{zona} · {cuenta}";
        }

        /// <summary>Mete la evidencia, o completa la que ya estaba. Con
        /// <paramref name="crear"/> en false solo completa. No guarda ni avisa: eso lo
        /// hace quien llama, una sola vez por lote.</summary>
        private static bool Fusionar(EvidenciaAlbum entrante, bool crear)
        {
            if (entrante == null) return false;

            string casoId = (entrante.casoId ?? "").Trim();
            string mensajeId = (entrante.mensajeId ?? "").Trim();
            if (casoId.Length == 0 || mensajeId.Length == 0) return false;

            string clave = EvidenciaAlbum.Clavear(casoId, mensajeId);
            if (_porClave.TryGetValue(clave, out EvidenciaAlbum guardada))
                return RellenarHuecos(guardada, entrante);

            if (!crear) return false;

            EvidenciaAlbum nueva = entrante.Copiar();
            nueva.casoId = casoId;
            nueva.mensajeId = mensajeId;
            if (nueva.etiquetas == null) nueva.etiquetas = new List<string>();
            if (nueva.registradaEn <= 0) nueva.registradaEn = DateTime.UtcNow.Ticks;

            _evidencias.Add(nueva);
            _porClave[clave] = nueva;
            return true;
        }

        /// <summary>
        /// Completa los campos vacíos de <paramref name="guardada"/> con los de
        /// <paramref name="entrante"/>. <b>Nunca sobrescribe lo que ya había</b>: una
        /// semilla del servidor no puede empeorar una evidencia que se ganó jugando
        /// acá, y un segundo intento del mismo caso tampoco tiene nada mejor que
        /// contar. Devuelve true si rellenó algo.
        /// </summary>
        private static bool RellenarHuecos(EvidenciaAlbum guardada, EvidenciaAlbum entrante)
        {
            if (guardada == null || entrante == null) return false;

            bool cambio = false;
            cambio |= Rellenar(ref guardada.casoTitulo, entrante.casoTitulo);
            cambio |= Rellenar(ref guardada.casoZona, entrante.casoZona);
            cambio |= Rellenar(ref guardada.autor, entrante.autor);
            cambio |= Rellenar(ref guardada.texto, entrante.texto);
            cambio |= Rellenar(ref guardada.explicacion, entrante.explicacion);

            bool sinEtiquetas = guardada.etiquetas == null || guardada.etiquetas.Count == 0;
            if (sinEtiquetas && entrante.etiquetas != null && entrante.etiquetas.Count > 0)
            {
                guardada.etiquetas = new List<string>(entrante.etiquetas);
                cambio = true;
            }

            if (guardada.registradaEn <= 0 && entrante.registradaEn > 0)
            {
                guardada.registradaEn = entrante.registradaEn;
                cambio = true;
            }

            return cambio;
        }

        private static bool Rellenar(ref string destino, string origen)
        {
            if (!string.IsNullOrWhiteSpace(destino)) return false;
            if (string.IsNullOrWhiteSpace(origen)) return false;

            destino = origen;
            return true;
        }

        private static string Clave() => $"{Prefijo}{NombreEvidencias}.Partida.{_partida}";

        /// <summary>
        /// Mete lo guardado de esta partida. Con <paramref name="conservarLoDeMemoria"/>
        /// se suma a lo que ya hubiera; sin él, lo reemplaza.
        /// </summary>
        private static void Cargar(bool conservarLoDeMemoria)
        {
            if (!conservarLoDeMemoria)
            {
                _evidencias.Clear();
                _porClave.Clear();
            }

            string guardado = PlayerPrefs.GetString(Clave(), "");
            if (string.IsNullOrEmpty(guardado)) return;

            AlbumGuardado archivo;
            try
            {
                archivo = JsonUtility.FromJson<AlbumGuardado>(guardado);
            }
            catch (Exception ex)
            {
                // Se avisa y se sigue con lo que haya en memoria. Lo que NO se hace es
                // borrar la clave: un formato que no entendemos puede ser legible por
                // una versión siguiente, y lo que hay ahí son evidencias de un niño/a.
                Debug.LogWarning($"[Álbum] No se pudo leer lo guardado de la partida " +
                                 $"{_partida}: {ex.Message}");
                return;
            }

            if (archivo == null) return;

            if (archivo.version != VersionFormato)
                Debug.LogWarning($"[Álbum] Lo guardado dice versión {archivo.version} y esta " +
                                 $"versión del juego escribe {VersionFormato}. Se carga igual.");

            if (archivo.evidencias == null) return;

            foreach (EvidenciaAlbum e in archivo.evidencias)
            {
                // JsonUtility deja las listas ausentes en null, no en lista vacía.
                if (e != null && e.etiquetas == null) e.etiquetas = new List<string>();
                Fusionar(e, crear: true);
            }

            Ordenar();
        }

        /// <summary>Por fecha de registro. OrderBy es estable, así que dos evidencias
        /// del mismo instante conservan el orden en que se registraron.</summary>
        private static void Ordenar()
        {
            List<EvidenciaAlbum> ordenadas = _evidencias.OrderBy(e => e.registradaEn).ToList();
            _evidencias.Clear();
            _evidencias.AddRange(ordenadas);
        }

        private static void Guardar()
        {
            // Sin partida no se escribe nada: una clave compartida le mezclaría el
            // álbum a la siguiente cuenta que entre en este computador. Lo anotado
            // mientras no había partida sigue en memoria y se guarda en cuanto
            // ConfigurarParaPartida diga de quién es.
            if (_partida == null) return;

            var archivo = new AlbumGuardado
            {
                version = VersionFormato,
                evidencias = new List<EvidenciaAlbum>(_evidencias),
            };

            PlayerPrefs.SetString(Clave(), JsonUtility.ToJson(archivo));
            PlayerPrefs.Save();
        }

        private static void GuardarYAvisar()
        {
            Guardar();
            AlCambiar?.Invoke();
        }
    }
}
