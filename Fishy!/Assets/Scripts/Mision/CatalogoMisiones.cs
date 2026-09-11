using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fishy.Mision
{
    /// <summary>Un objetivo tal como viaja en los datos, antes de tocar la escena.</summary>
    [Serializable]
    public class ObjetivoRegistro
    {
        [Tooltip("Posición dentro de la misión. El cartel muestra los primeros.")]
        public int orden;

        /// <summary>
        /// Una de las cuatro categorías, en texto:
        /// <c>recoger_objeto</c> · <c>hablar_npc</c> · <c>chatear_telefono</c> · <c>llegar_zona</c>.
        ///
        /// Va en texto y no como enum a propósito. El enum <c>TipoObjetivo</c> vive en
        /// Assembly-CSharp —lo necesita para hablar de NPC e ItemData— y desde aquí no
        /// se ve; además el texto sobrevive a que alguien reordene el enum, que es el
        /// accidente clásico con los enums serializados por índice.
        /// </summary>
        public string tipo;

        // ── Si es recoger_objeto ──
        public string item_id;
        public int cantidad = 1;

        // ── Si es hablar_npc ──
        /// <summary>Id de diálogo del bloque `dialogos_npc_neutros` del banco,
        /// p. ej. `HDU1_SEC_COIPO_MASCOTA`. Es el único identificador que los NPCs
        /// del mapa llevan encima.</summary>
        public string dialogo_id;

        // ── Si es chatear_telefono ──
        /// <summary>Uno o varios `escenario_id` del bloque `preguntas`, separados por
        /// coma si la historia va por fases. Es el criterio preciso.</summary>
        public string escenario_ids;

        // ── Si es llegar_zona ──
        /// <summary>Id ESPACIAL de la zona (`zona_1`, `zona_2`, `zona_3`), no el
        /// temático del banco.</summary>
        public string zona_id;

        // ── Si es completar_caso_detective ──
        /// <summary>caso_id del Modo Detective (`DC_CASO_01`). Cuenta con cualquier
        /// resultado del caso, no hace falta superar el umbral de aciertos.</summary>
        public string caso_id;
    }

    /// <summary>Una misión del catálogo, tal como viaja en los datos.</summary>
    [Serializable]
    public class MisionRegistro
    {
        public string mision_id;

        /// <summary>Título para el cartel. Ver <see cref="Titulo"/>.</summary>
        public string titulo;

        /// <summary>
        /// Alias de <see cref="titulo"/>. La tabla `Mision` que ya existe en la base
        /// llama `nombre` a este campo, así que se aceptan los dos nombres: si el
        /// backend manda uno u otro, la misión igual sale con su título en vez de
        /// quedarse en blanco sin que nadie se entere. JsonUtility deja en null el
        /// campo que no venga, y eso es justo lo que se comprueba.
        /// </summary>
        public string nombre;

        /// <summary>El título que se muestra, de donde haya salido. Último recurso: el
        /// propio id, que es feo pero deja ver qué misión es.</summary>
        public string Titulo
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(titulo)) return titulo.Trim();
                if (!string.IsNullOrWhiteSpace(nombre)) return nombre.Trim();
                return (mision_id ?? "").Trim();
            }
        }

        /// <summary>principal · secundaria · exploracion</summary>
        public string tipo;

        /// <summary>Zona TEMÁTICA (`desconocidos`, `ciberacoso`, `reto_viral`): de qué
        /// trata el contenido. No sirve para ubicar a nadie.</summary>
        public string zona;

        /// <summary>Zona ESPACIAL de destino (`zona_1`…): hacia dónde apunta el
        /// indicador. Vacío = esta misión no señala ninguna zona.</summary>
        public string zona_objetivo;

        /// <summary>Lugar en la historia. Menor va antes.</summary>
        public int orden = 100;

        public List<ObjetivoRegistro> objetivos = new List<ObjetivoRegistro>();

        /// <summary>Los objetivos por su <c>orden</c>, que es el que se muestra.</summary>
        public List<ObjetivoRegistro> ObjetivosEnOrden() =>
            (objetivos ?? new List<ObjetivoRegistro>())
                .Where(o => o != null)
                .OrderBy(o => o.orden)
                .ToList();
    }

    /// <summary>Raíz del archivo de respaldo. JsonUtility no sabe leer un array suelto.</summary>
    [Serializable]
    public class MisionesArchivo
    {
        public string version;
        public string nota;
        public List<MisionRegistro> misiones = new List<MisionRegistro>();
    }

    /// <summary>
    /// El catálogo de misiones del juego: qué misiones existen y qué pide cada una.
    ///
    /// <b>Dos fuentes, y el archivo es la red de seguridad.</b> El contenido lo manda la
    /// base de datos, pero el juego no puede quedarse sin misiones porque no haya red,
    /// no haya sesión o se esté jugando en modo local. Así que:
    ///
    ///   1. Al arrancar se lee <c>Resources/misiones.json</c>, que va empaquetado con
    ///      el juego. Es síncrono y no falla nunca: desde el primer frame hay catálogo.
    ///   2. Si más tarde llega el de la base, lo reemplaza (<see cref="AplicarDesdeBase"/>)
    ///      y se avisa por <see cref="OnCatalogoCambiado"/>.
    ///
    /// Es el mismo reparto que ya hacen el banco de preguntas
    /// (<c>BancoPreguntasLoader</c> + <c>BancoBackendSync</c>) y los casos del Modo
    /// Detective, y por la misma razón: esperar a la red para poder empezar a jugar
    /// convierte un problema de conexión en un juego roto.
    ///
    /// <b>Esto es catálogo, no progreso.</b> Qué misiones lleva hechas un niño/a
    /// concreto sigue siendo cosa de <see cref="MissionManager"/> y de
    /// <c>MisionBackendSync</c>. Aquí sólo está el contenido, que es igual para todos.
    ///
    /// Vive en el assembly <c>Fishy.Mision</c> porque no toca nada de escena: sólo
    /// texto, números y <see cref="DesafioData"/>. Convertir un objetivo en referencias
    /// del mundo —el NPC, el ItemData— es cosa de <c>ObjetivoMision</c>, del otro lado.
    /// </summary>
    public static class CatalogoMisiones
    {
        /// <summary>De dónde salió lo que hay ahora en memoria.</summary>
        public enum Origen
        {
            /// <summary>Todavía no se cargó nada.</summary>
            Ninguno,
            /// <summary>Del archivo empaquetado. Es el respaldo.</summary>
            Archivo,
            /// <summary>De la base de datos.</summary>
            Base,
        }

        /// <summary>Archivo de respaldo, dentro de Resources y sin extensión.</summary>
        public const string RutaResources = "misiones";

        public static Origen DeDonde { get; private set; } = Origen.Ninguno;

        /// <summary>Se dispara cuando el catálogo cambia, que en la práctica es cuando
        /// llega el de la base y reemplaza al del archivo.</summary>
        public static event Action OnCatalogoCambiado;

        private static Dictionary<string, MisionRegistro> _porId;
        private static readonly Dictionary<string, DesafioData> _fichasEnMemoria =
            new Dictionary<string, DesafioData>();

        // ── Consultas ─────────────────────────────────────────────────────────

        /// <summary>Todas las misiones del catálogo, por id.</summary>
        public static IReadOnlyDictionary<string, MisionRegistro> Todas
        {
            get { Asegurar(); return _porId; }
        }

        /// <summary>La misión con ese id, o null si el catálogo no la tiene.</summary>
        public static MisionRegistro Buscar(string misionId)
        {
            if (string.IsNullOrWhiteSpace(misionId)) return null;
            Asegurar();
            return _porId.TryGetValue(misionId.Trim(), out var m) ? m : null;
        }

        /// <summary>El catálogo en orden de historia.</summary>
        public static List<MisionRegistro> EnOrden()
        {
            Asegurar();
            return _porId.Values
                .OrderBy(m => m.orden)
                .ThenBy(m => m.titulo)
                .ToList();
        }

        // ── Carga ─────────────────────────────────────────────────────────────

        private static void Asegurar()
        {
            if (_porId != null) return;
            CargarDeArchivo();
        }

        /// <summary>
        /// Lee el archivo de respaldo. Se llama solo la primera vez que alguien
        /// pregunta algo, así que normalmente no hay que invocarlo a mano.
        /// </summary>
        public static void CargarDeArchivo()
        {
            var asset = Resources.Load<TextAsset>(RutaResources);
            if (asset == null)
            {
                // No es un error mortal: el juego puede funcionar con las fichas que
                // haya arrastradas en la escena. Pero sí es algo que hay que ver.
                Debug.LogWarning(
                    $"[CatalogoMisiones] No encontré Resources/{RutaResources}.json. " +
                    "Sin él, si la base no responde el juego se queda sin catálogo de " +
                    "misiones y sólo valen las fichas puestas a mano en la escena.");
                Poner(new List<MisionRegistro>(), Origen.Ninguno);
                return;
            }

            LeerTexto(asset.text, quejarseSiVacio: true);
        }

        /// <summary>
        /// Carga el catálogo desde un JSON en texto. Separado de
        /// <see cref="CargarDeArchivo"/> para poder probarlo sin tocar Resources.
        /// </summary>
        public static void LeerTexto(string json, bool quejarseSiVacio = false)
        {
            MisionesArchivo archivo = null;
            try
            {
                archivo = JsonUtility.FromJson<MisionesArchivo>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CatalogoMisiones] El archivo de misiones no se pudo leer: {e.Message}");
            }

            var misiones = archivo?.misiones ?? new List<MisionRegistro>();
            if (misiones.Count == 0 && quejarseSiVacio)
            {
                Debug.LogWarning("[CatalogoMisiones] El archivo de misiones no trae ninguna " +
                                 "misión. ¿Está vacío o mal formado?");
            }

            Poner(misiones, misiones.Count > 0 ? Origen.Archivo : Origen.Ninguno);
            Debug.Log($"[CatalogoMisiones] {_porId.Count} misión(es) desde el archivo de respaldo.");
        }

        /// <summary>
        /// Reemplaza el catálogo por el de la base de datos.
        ///
        /// Manda la base: es lo que el equipo puede corregir sin recompilar ni publicar
        /// una versión nueva. Si llega vacío se conserva lo del archivo, porque una
        /// respuesta vacía es casi siempre un problema del servidor y no un juego que
        /// de verdad se quedó sin misiones.
        /// </summary>
        public static void AplicarDesdeBase(IEnumerable<MisionRegistro> misiones)
        {
            var lista = (misiones ?? Enumerable.Empty<MisionRegistro>())
                .Where(m => m != null && !string.IsNullOrWhiteSpace(m.mision_id))
                .ToList();

            if (lista.Count == 0)
            {
                Debug.LogWarning("[CatalogoMisiones] La base no devolvió ninguna misión; " +
                                 "se mantiene el archivo de respaldo.");
                return;
            }

            // Si NINGUNA trae título, lo más probable es que el backend llame a ese
            // campo de otra manera y JsonUtility lo haya dejado vacío sin quejarse. Es
            // el fallo más fácil de no ver: el catálogo "funciona" y el cartel sale en
            // blanco. Mejor decirlo aquí, con el nombre de los campos que se esperan.
            int conTitulo = lista.Count(m => !string.IsNullOrWhiteSpace(m.Titulo) &&
                                             m.Titulo != (m.mision_id ?? "").Trim());
            if (conTitulo == 0)
            {
                Debug.LogError(
                    "[CatalogoMisiones] La base devolvió misiones pero ninguna trae título. " +
                    "Seguramente el campo se llama de otra forma: aquí se esperan 'titulo' " +
                    "o 'nombre'. Revisar también 'mision_id', 'zona_objetivo', 'orden' y " +
                    "'objetivos'.");
            }

            Poner(lista, Origen.Base);
            Debug.Log($"[CatalogoMisiones] {_porId.Count} misión(es) desde la base de datos.");
            OnCatalogoCambiado?.Invoke();
        }

        private static void Poner(List<MisionRegistro> misiones, Origen origen)
        {
            _porId = new Dictionary<string, MisionRegistro>();

            foreach (MisionRegistro m in misiones)
            {
                if (m == null || string.IsNullOrWhiteSpace(m.mision_id)) continue;

                string id = m.mision_id.Trim();
                if (_porId.ContainsKey(id))
                {
                    // Mismo motivo que en CatalogoDesafios: el id es la llave con la que
                    // se guarda el progreso, así que dos misiones con el mismo id se
                    // pisan entre sí y completar una marca la otra.
                    Debug.LogError($"[CatalogoMisiones] El id '{id}' viene repetido en el " +
                                   "catálogo. Se queda el primero.");
                    continue;
                }
                _porId[id] = m;
            }

            DeDonde = origen;
            RefrescarFichasEnMemoria();
        }

        /// <summary>Vuelve a leer el archivo. Para el editor y las pruebas.</summary>
        public static void Recargar()
        {
            _porId = null;
            DeDonde = Origen.Ninguno;
            Asegurar();   // Asegurar → CargarDeArchivo → Poner → refresca las fichas
        }

        // ── Fichas ────────────────────────────────────────────────────────────

        /// <summary>
        /// La <see cref="DesafioData"/> de esa misión, lista para dársela al
        /// <see cref="MissionManager"/>.
        ///
        /// <b>Si ya existe un asset con ese id, gana el asset.</b> No se le sobrescribe
        /// nada: un ScriptableObject modificado en juego se queda modificado en el
        /// disco cuando se corre desde el editor, y un catálogo que llega tarde
        /// reescribiría a mano lo que alguien puso en el Inspector. Si el asset y el
        /// catálogo no coinciden se avisa, para que la discrepancia se vea.
        ///
        /// Para las misiones que no tienen asset —que es el caso normal cuando el
        /// contenido vive en la base— se fabrica una ficha en memoria y se registra en
        /// <see cref="CatalogoDesafios"/>, para que la restauración de la partida
        /// pueda encontrarla por id.
        /// </summary>
        public static DesafioData Ficha(string misionId)
        {
            MisionRegistro registro = Buscar(misionId);
            if (registro == null) return null;

            string id = registro.mision_id.Trim();

            DesafioData asset = CatalogoDesafios.Buscar(id);
            if (asset != null)
            {
                AvisarSiNoCoinciden(asset, registro);
                return asset;
            }

            if (_fichasEnMemoria.TryGetValue(id, out DesafioData enMemoria))
            {
                if (enMemoria != null) return enMemoria;

                // Quedó destruida y el diccionario guarda el hueco. Pasa al volver a
                // darle Play en el editor con el dominio sin recargar: los estáticos
                // sobreviven pero los objetos de Unity no. Se suelta la referencia
                // muerta antes de fabricar otra, o el catálogo se quedaría con ella.
                CatalogoDesafios.Olvidar(id);
            }

            var ficha = ScriptableObject.CreateInstance<DesafioData>();
            ficha.name          = id;
            ficha.desafioId     = id;
            ficha.titulo        = registro.Titulo;
            ficha.zonaObjetivo  = registro.zona_objetivo;
            ficha.orden         = registro.orden;
            // `descripcion` no viaja en el catálogo a propósito: se decidió dejarla
            // fuera de la base. Sigue existiendo para las fichas hechas a mano.
            ficha.hideFlags     = HideFlags.HideAndDontSave;

            _fichasEnMemoria[id] = ficha;
            CatalogoDesafios.Registrar(ficha);
            return ficha;
        }

        private static void AvisarSiNoCoinciden(DesafioData asset, MisionRegistro registro)
        {
            var diferencias = new List<string>();

            string tituloCatalogo = registro.Titulo;
            if (!string.IsNullOrWhiteSpace(tituloCatalogo) && asset.titulo != tituloCatalogo)
                diferencias.Add($"título ('{asset.titulo}' vs '{tituloCatalogo}')");
            if (asset.orden != registro.orden)
                diferencias.Add($"orden ({asset.orden} vs {registro.orden})");

            string zonaAsset = asset.zonaObjetivo ?? "";
            string zonaCat   = registro.zona_objetivo ?? "";
            if (zonaAsset.Trim() != zonaCat.Trim())
                diferencias.Add($"zona objetivo ('{zonaAsset}' vs '{zonaCat}')");

            if (diferencias.Count == 0) return;

            Debug.LogWarning(
                $"[CatalogoMisiones] La ficha '{asset.name}' y el catálogo no dicen lo mismo en: " +
                string.Join(", ", diferencias) + ". Manda la ficha, porque tocarla desde " +
                "el juego la cambiaría en disco. Actualízala en el Inspector o bórrala " +
                "para que el catálogo la fabrique sola.");
        }

        /// <summary>
        /// Pone al día las fichas ya fabricadas con lo que diga el catálogo nuevo.
        ///
        /// <b>Se actualizan en el sitio; no se destruyen ni se reemplazan.</b> Cuando
        /// el catálogo de la base llega a mitad de partida puede haber misiones ya
        /// entregadas, y <see cref="MissionManager"/> guarda la ficha por referencia:
        /// soltarla dejaría al panel enseñando "(desafío desconocido)" para una misión
        /// que el niño/a está haciendo en ese momento. Tocarlas es seguro justamente
        /// porque son de memoria —<see cref="HideFlags.HideAndDontSave"/>—, al revés
        /// que los assets del proyecto, que por eso no se tocan nunca.
        ///
        /// Una ficha cuya misión ya no está en el catálogo se conserva tal cual: si
        /// alguien la está jugando, quitársela a media partida es peor que dejar
        /// contenido de más.
        /// </summary>
        private static void RefrescarFichasEnMemoria()
        {
            foreach (KeyValuePair<string, DesafioData> par in _fichasEnMemoria)
            {
                DesafioData ficha = par.Value;
                if (ficha == null) continue;

                if (!_porId.TryGetValue(par.Key, out MisionRegistro registro))
                {
                    Debug.LogWarning(
                        $"[CatalogoMisiones] El catálogo nuevo ya no trae '{par.Key}', pero " +
                        "la ficha se conserva porque puede haber una partida jugándola.");
                    continue;
                }

                ficha.titulo       = registro.Titulo;
                ficha.zonaObjetivo = registro.zona_objetivo;
                ficha.orden        = registro.orden;
            }
        }
    }
}
