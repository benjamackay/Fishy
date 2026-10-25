#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Fishy.Mision;
using UnityEditor;
using UnityEngine;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Pruebas headless del seguimiento de objetivos al retomar una partida.
    ///
    /// Existen por una regresión que se vio jugando y no en ningún log: al arreglar la
    /// restauración del panel, las misiones volvían con su título y su descripción pero
    /// <b>sin la cuenta de objetivos</b> ("2/4"). El motivo es que los objetivos no viven
    /// en la ficha: los lleva <see cref="MissionTracker"/>, y a ese solo lo arrancaban los
    /// sitios que ENTREGAN una misión, ninguno de los cuales corre al retomar.
    ///
    /// Lo que se prueba aquí es el mecanismo del que depende el arreglo: que los objetivos
    /// se puedan sacar del catálogo, que al seguirlos el progreso deje de ser null, y que
    /// volver a seguir la misma misión no pise lo que ya había — que es lo que permite
    /// llamarlo al restaurar sin romper a los NPCs que traen objetivos propios.
    ///
    /// Se corren desde  Fishy ▸ Probar objetivos de misión,  o sin abrir el editor:
    ///
    ///   Unity.exe -batchmode -nographics -quit -projectPath "&lt;ruta&gt;" `
    ///             -executeMethod Fishy.EditorTools.FishyPruebasObjetivos.Ejecutar `
    ///             -logFile -
    /// </summary>
    public static class FishyPruebasObjetivos
    {
        private static int _ok;
        private static readonly List<string> _fallas = new List<string>();

        private const string IdMision = "M_CON_OBJETIVOS";

        private const string JsonCatalogo = @"{
            ""version"": ""test"",
            ""misiones"": [
                {
                    ""mision_id"": ""M_CON_OBJETIVOS"",
                    ""titulo"": ""Una con objetivos"",
                    ""tipo"": ""secundaria"",
                    ""zona"": ""desconocidos"",
                    ""zona_objetivo"": ""zona_1"",
                    ""orden"": 10,
                    ""objetivos"": [
                        { ""orden"": 2, ""tipo"": ""llegar_zona"", ""zona_id"": ""zona_2"" },
                        { ""orden"": 1, ""tipo"": ""recoger_objeto"", ""item_id"": ""ITEM_FLOR_01"", ""cantidad"": 2 }
                    ]
                }
            ]
        }";

        [MenuItem("Fishy/Probar objetivos de misión")]
        public static void Ejecutar()
        {
            _ok = 0;
            _fallas.Clear();

            var log = new StringBuilder();
            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine("PRUEBAS DE OBJETIVOS DE MISIÓN (headless)");
            log.AppendLine(new string('=', 70));

            GameObject go = null;
            try
            {
                CatalogoMisiones.LeerTexto(JsonCatalogo);

                go = new GameObject("MissionTrackerDePrueba") { hideFlags = HideFlags.HideAndDontSave };
                var tracker = go.AddComponent<MissionTracker>();
                tracker.verboseLogs = false;
                FijarInstancia(tracker);

                ProbarObjetivosDelCatalogo(log);
                ProbarQueSeguirEnciendeElProgreso(log, tracker);
                ProbarQueSeguirNoPisaLoQueYaHabia(log, tracker);
                ProbarNombreDeUnObjetivoYaCumplido(log);
                ProbarHistorialCumpleObjetivosDeAntes(log);
                ProbarHistorialYElCambioDePartida(log);
                ProbarObjetivosDelInspectorSeNumeran(log, tracker);
                ProbarLaFlechaDeZonaNaceEscondida(log);
            }
            finally
            {
                if (go != null) Object.DestroyImmediate(go);
                SoltarInstancia();
                CatalogoDesafios.Olvidar(IdMision);
                CatalogoMisiones.Recargar();
            }

            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine($"RESULTADO: {_ok} OK, {_fallas.Count} fallas");
            log.AppendLine(new string('=', 70));

            if (_fallas.Count > 0) Debug.LogError(log.ToString());
            else                   Debug.Log(log.ToString());

            if (Application.isBatchMode)
                EditorApplication.Exit(_fallas.Count > 0 ? 1 : 0);
        }

        // ── Las pruebas ───────────────────────────────────────────────────────

        /// <summary>
        /// Los objetivos armados a mano en el Inspector reciben número, para que su
        /// avance se pueda guardar.
        ///
        /// Es el caso de «Las llaves del cofre»: sus cuatro objetivos vivían en el prefab
        /// de la Foca con `ordenCatalogo = 0`, y ese cero los dejaba fuera del guardado
        /// por dos puertas —ObjetivosBackendSync no los sube y MarcarGuardados no los
        /// restaura—. Ninguno llegó nunca a la base.
        /// </summary>
        private static void ProbarObjetivosDelInspectorSeNumeran(
            StringBuilder log, MissionTracker tracker)
        {
            tracker.Reiniciar();

            var ficha = ScriptableObject.CreateInstance<DesafioData>();
            ficha.desafioId = "MISION_DE_PRUEBA_INSPECTOR";
            ficha.titulo = "Armada a mano";

            // Como los deja el Inspector: sin ordenCatalogo.
            var objetivos = new List<ObjetivoMision>
            {
                new ObjetivoMision { tipo = TipoObjetivo.ChatearPorTelefono, escenarioIds = "A" },
                new ObjetivoMision { tipo = TipoObjetivo.ChatearPorTelefono, escenarioIds = "B" },
                new ObjetivoMision { tipo = TipoObjetivo.ChatearPorTelefono, escenarioIds = "C" },
            };

            Comprobar(log, "Los objetivos del Inspector llegan sin número de catálogo",
                objetivos.TrueForAll(o => o.ordenCatalogo == 0), "");

            tracker.Seguir(ficha, objetivos);

            Comprobar(log, "Al seguirlos se numeran por posición, para poder guardarlos",
                objetivos[0].ordenCatalogo == 1 &&
                objetivos[1].ordenCatalogo == 2 &&
                objetivos[2].ordenCatalogo == 3,
                $"quedaron en {objetivos[0].ordenCatalogo}, {objetivos[1].ordenCatalogo}, " +
                $"{objetivos[2].ordenCatalogo}");

            // Y una lista que SÍ viene del catálogo no se toca: inventarle números encima
            // pisaría los suyos y el avance guardado dejaría de corresponder.
            tracker.Reiniciar();

            var delCatalogo = ScriptableObject.CreateInstance<DesafioData>();
            delCatalogo.desafioId = "MISION_DE_PRUEBA_CATALOGO";
            delCatalogo.titulo = "Del catálogo";

            var conNumeros = new List<ObjetivoMision>
            {
                new ObjetivoMision { tipo = TipoObjetivo.ChatearPorTelefono, escenarioIds = "A", ordenCatalogo = 7 },
                new ObjetivoMision { tipo = TipoObjetivo.ChatearPorTelefono, escenarioIds = "B", ordenCatalogo = 9 },
            };

            tracker.Seguir(delCatalogo, conNumeros);

            Comprobar(log, "Una lista que ya trae números del catálogo no se renumera",
                conNumeros[0].ordenCatalogo == 7 && conNumeros[1].ordenCatalogo == 9,
                $"quedaron en {conNumeros[0].ordenCatalogo}, {conNumeros[1].ordenCatalogo}");

            tracker.Reiniciar();
            Object.DestroyImmediate(ficha);
            Object.DestroyImmediate(delCatalogo);
        }

        /// <summary>
        /// Lo que se hizo ANTES de recibir la misión cuenta.
        ///
        /// El caso real: el niño/a explora por su cuenta, habla con el Huemul, y recién
        /// después le encargan la misión de hablar con el Huemul. Antes ese objetivo
        /// nacía pendiente y había que repetir la conversación. Con los chats del celular
        /// era peor: un PhoneChatLauncher sin 'repetible' no se vuelve a abrir, así que
        /// el objetivo quedaba imposible de cumplir.
        /// </summary>
        private static void ProbarHistorialCumpleObjetivosDeAntes(StringBuilder log)
        {
            HistorialDeObjetivos.Limpiar();

            // Primero se comprueba que SIN historial el objetivo está pendiente. Sin
            // esto la prueba podría pasar por estar mirando un true de otra cosa.
            var hablar = new ObjetivoMision
            {
                tipo = TipoObjetivo.HablarConNpc,
                dialogoNpcId = "HDU1_NPC_HUEMUL",
            };
            Comprobar(log, "Sin historial, «hablar con» nace pendiente",
                !hablar.Evaluar(), $"cumplido = {hablar.cumplido}");

            // Ahora el jugador habla con él, y RECIÉN DESPUÉS llega la misión.
            HistorialDeObjetivos.AnotarDialogo("HDU1_NPC_HUEMUL");

            var recibidaDespues = new ObjetivoMision
            {
                tipo = TipoObjetivo.HablarConNpc,
                dialogoNpcId = "HDU1_NPC_HUEMUL",
            };
            Comprobar(log, "Haber hablado antes de recibir la misión cuenta",
                recibidaDespues.Evaluar() && recibidaDespues.cumplido,
                $"cumplido = {recibidaDespues.cumplido}");

            // Y no se cumple cualquier cosa: otro NPC sigue pendiente.
            var otro = new ObjetivoMision
            {
                tipo = TipoObjetivo.HablarConNpc,
                dialogoNpcId = "HDU1_NPC_COIPO",
            };
            Comprobar(log, "El historial no da por cumplido a un NPC distinto",
                !otro.Evaluar(), $"cumplido = {otro.cumplido}");

            // El chat del celular, que es el caso que quedaba imposible.
            HistorialDeObjetivos.AnotarChats(new[] { "M1_CHAT01" });

            var chat = new ObjetivoMision
            {
                tipo = TipoObjetivo.ChatearPorTelefono,
                escenarioIds = "M1_CHAT01",
            };
            Comprobar(log, "Un chat ya atendido cumple el objetivo aunque el lanzador no se repita",
                chat.Evaluar() && chat.cumplido, $"cumplido = {chat.cumplido}");

            // Un objetivo que nombra varias fases se cumple con una: es el mismo criterio
            // con el que ObjetivoMision busca el lanzador.
            var variasFases = new ObjetivoMision
            {
                tipo = TipoObjetivo.ChatearPorTelefono,
                escenarioIds = "M4_FASE01, M1_CHAT01",
            };
            Comprobar(log, "Con varias fases basta haber hecho una",
                variasFases.Evaluar(), $"cumplido = {variasFases.cumplido}");

            // Y el caso de detective.
            HistorialDeObjetivos.AnotarCasoDetective("DC_CASO_01");
            var caso = new ObjetivoMision
            {
                tipo = TipoObjetivo.CompletarCasoDetective,
                casoDetectiveId = "DC_CASO_01",
            };
            Comprobar(log, "Un caso de detective ya jugado cumple su objetivo",
                caso.Evaluar() && caso.cumplido, $"cumplido = {caso.cumplido}");

            HistorialDeObjetivos.Limpiar();
        }

        /// <summary>
        /// Lo anotado antes de que llegara el PartidaId no se pierde, y lo de otra
        /// partida no se hereda.
        ///
        /// Entre que arranca la escena y que el login deja el PartidaId puesto pasan
        /// segundos, y en esa ventana el niño/a ya puede haber hablado con alguien. La
        /// primera versión de esto lo perdía: `Cargar` hacía `Clear()` antes de leer lo
        /// guardado, así que ataba la partida y de paso borraba lo de esa ventana.
        /// </summary>
        private static void ProbarHistorialYElCambioDePartida(StringBuilder log)
        {
            const int PartidaA = 990101;
            const int PartidaB = 990102;

            HistorialDeObjetivos.Limpiar();

            // Se habla con alguien ANTES de que el login deje la partida puesta.
            HistorialDeObjetivos.AnotarDialogo("HDU1_NPC_HUEMUL");
            HistorialDeObjetivos.ConfigurarParaPartida(PartidaA);

            Comprobar(log, "Lo anotado antes de que llegara la partida no se pierde",
                HistorialDeObjetivos.HabloCon("HDU1_NPC_HUEMUL"), "");

            // Otro perfil en el mismo equipo no hereda nada.
            HistorialDeObjetivos.ConfigurarParaPartida(PartidaB);
            Comprobar(log, "Al cambiar de partida no se hereda lo del perfil anterior",
                !HistorialDeObjetivos.HabloCon("HDU1_NPC_HUEMUL"), "");

            // Y al volver a la primera, lo suyo sigue ahí.
            HistorialDeObjetivos.ConfigurarParaPartida(PartidaA);
            Comprobar(log, "Al volver a la partida de antes, su historial sigue guardado",
                HistorialDeObjetivos.HabloCon("HDU1_NPC_HUEMUL"), "");

            // Limpieza: Limpiar() solo borra las claves de la partida atada, así que hay
            // que pasar por las dos para no dejarle nada puesto al juego de verdad.
            HistorialDeObjetivos.Limpiar();
            HistorialDeObjetivos.ConfigurarParaPartida(PartidaB);
            HistorialDeObjetivos.Limpiar();
        }

        /// <summary>Los objetivos salen del catálogo y en el orden que dice `orden`,
        /// no en el que vinieron escritos.</summary>
        private static void ProbarObjetivosDelCatalogo(StringBuilder log)
        {
            List<ObjetivoMision> objetivos = ObjetivoMision.DesdeCatalogo(IdMision);

            Comprobar(log, "Los objetivos se sacan del catálogo, ordenados por 'orden'",
                objetivos.Count == 2 && objetivos[0].ordenCatalogo == 1 && objetivos[1].ordenCatalogo == 2,
                $"{objetivos.Count} objetivo(s)");

            Comprobar(log, "Una misión que no está en el catálogo da lista vacía, no null",
                ObjetivoMision.DesdeCatalogo("M_QUE_NO_EXISTE") != null &&
                ObjetivoMision.DesdeCatalogo("M_QUE_NO_EXISTE").Count == 0, "");
        }

        /// <summary>
        /// El corazón de la regresión: sin `Seguir`, el panel no tiene nada que contar.
        /// </summary>
        private static void ProbarQueSeguirEnciendeElProgreso(StringBuilder log, MissionTracker tracker)
        {
            DesafioData ficha = MissionManager.BuscarFicha(IdMision);

            Comprobar(log, "BuscarFicha encuentra la ficha de una misión del catálogo",
                ficha != null, ficha == null ? "devolvió null" : ficha.titulo);

            Comprobar(log, "Antes de seguirla, el progreso es null — el panel no puede contar",
                tracker.Progreso(IdMision) == null,
                $"progreso = {tracker.Progreso(IdMision) ?? "null"}");

            tracker.Seguir(ficha, ObjetivoMision.DesdeCatalogo(IdMision));

            Comprobar(log, "Después de seguirla, el panel ya sabe cuántos objetivos faltan",
                tracker.Progreso(IdMision) == "0/2",
                $"progreso = {tracker.Progreso(IdMision) ?? "null"}");
        }

        /// <summary>
        /// Es lo que hace seguro llamarlo al restaurar: si un NPC ya la estaba siguiendo
        /// con objetivos puestos a mano en el Inspector, esto no se los quita.
        /// </summary>
        private static void ProbarQueSeguirNoPisaLoQueYaHabia(StringBuilder log, MissionTracker tracker)
        {
            DesafioData ficha = MissionManager.BuscarFicha(IdMision);

            // Un segundo Seguir con UN solo objetivo: si pisara, el progreso pasaría a "0/1".
            tracker.Seguir(ficha, new List<ObjetivoMision>
            {
                ObjetivoMision.DesdeRegistro(new ObjetivoRegistro { orden = 1, tipo = "llegar_zona", zona_id = "zona_3" }),
            });

            Comprobar(log, "Volver a seguir la misma misión no pisa los objetivos ya seguidos",
                tracker.Progreso(IdMision) == "0/2",
                $"progreso = {tracker.Progreso(IdMision) ?? "null"}");
        }

        /// <summary>
        /// Un objetivo que vuelve del servidor ya cumplido tiene que enseñar el NOMBRE
        /// del personaje, no el id del banco.
        ///
        /// Es lo que se vio en pantalla: "Atender el chat de M1_CHAT01 (1/1) ¡listo!"
        /// junto a "Atender el chat de Pumy (1/1) ¡listo!". El de abajo se había resuelto
        /// porque seguía pendiente al empezar a seguirlo; el de arriba llegó marcado como
        /// cumplido, y MissionTracker salta esos al resolver.
        /// </summary>
        private static void ProbarNombreDeUnObjetivoYaCumplido(StringBuilder log)
        {
            // Sin HideAndDontSave a propósito: FindObjectsByType —que es como
            // ObjetivoMision busca el lanzador— no ve los objetos ocultos, y la prueba
            // pasaría por el motivo equivocado. Se destruye igual en el finally.
            var go = new GameObject("Puma");
            try
            {
                // PhoneChatLauncher pide un Collider2D, y esa clase es abstracta: sin
                // uno concreto delante, AddComponent devuelve null en silencio.
                go.AddComponent<BoxCollider2D>();
                var lanzador = go.AddComponent<Fishy.Phone.PhoneChatLauncher>();
                lanzador.escenarioIds = "M1_CHAT01";

                var objetivo = ObjetivoMision.DesdeRegistro(new ObjetivoRegistro
                {
                    orden = 1, tipo = "chatear_telefono", escenario_ids = "M1_CHAT01",
                });

                // Como vuelve del servidor: cumplido y sin que nadie lo haya resuelto.
                objetivo.cumplido = true;

                string linea = objetivo.Describir();

                Comprobar(log, "Un objetivo ya cumplido enseña el nombre del personaje, no el id",
                    linea == "Atender el chat de Puma",
                    $"decía «{linea}»");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ── Andamiaje ─────────────────────────────────────────────────────────

        /// <summary>
        /// Publica el tracker como su propio <c>Instance</c>: en modo edición Unity no
        /// llama a <c>Awake</c>, y su <c>GetOrCreate</c> crearía otro objeto suelto.
        /// Ver la nota larga en FishyPruebasCola.FijarInstancia.
        /// </summary>
        private static void FijarInstancia(MissionTracker tracker)
            => typeof(MissionTracker)
                .GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                ?.GetSetMethod(nonPublic: true)?.Invoke(null, new object[] { tracker });

        private static void SoltarInstancia()
            => typeof(MissionTracker)
                .GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                ?.GetSetMethod(nonPublic: true)?.Invoke(null, new object[] { null });

        /// <summary>
        /// La flecha de zona se construía visible y su "esconder" inicial no hacía nada
        /// (la zona ya era null). Al volver a entrar a una partida cuya misión activa no
        /// señala ninguna zona, se quedaba clavada encima de Otto apuntando a la derecha.
        /// </summary>
        private static void ProbarLaFlechaDeZonaNaceEscondida(StringBuilder log)
        {
            var go = new GameObject("ZoneMarkerDePrueba") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var marcador = go.AddComponent<ZoneMarker>();
                marcador.verboseLogs = false;
                // En modo edición Unity no llama a Awake.
                typeof(ZoneMarker).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(marcador, null);

                bool alNacer = marcador.Visible;
                marcador.Apuntar(null);
                bool sinZona = marcador.Visible;
                marcador.Apuntar("zona_1");
                bool conZona = marcador.Visible;
                marcador.Apuntar(null);
                bool otraVezSinZona = marcador.Visible;

                Comprobar(log, "La flecha de zona nace escondida y solo sale con una zona",
                    !alNacer && !sinZona && conZona && !otraVezSinZona,
                    $"al nacer={alNacer}, sin zona={sinZona}, con zona={conZona}, " +
                    $"otra vez sin zona={otraVezSinZona}");
            }
            finally
            {
                Object.DestroyImmediate(go);
                typeof(ZoneMarker).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                    ?.GetSetMethod(nonPublic: true)?.Invoke(null, new object[] { null });
            }
        }

        private static void Comprobar(StringBuilder log, string que, bool paso, string detalle)
        {
            if (paso)
            {
                _ok++;
                log.AppendLine($"  OK    {que}" + (string.IsNullOrEmpty(detalle) ? "" : $"  [{detalle}]"));
            }
            else
            {
                _fallas.Add(que);
                log.AppendLine($"  FALLA {que}" + (string.IsNullOrEmpty(detalle) ? "" : $"  [{detalle}]"));
            }
        }
    }
}
#endif
