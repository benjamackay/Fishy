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
