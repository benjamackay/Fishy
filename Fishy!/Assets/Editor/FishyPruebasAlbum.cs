#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Fishy.Album;
using Fishy.Detective;
using Fishy.Net;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Pruebas headless del álbum de evidencias (HDU-12): lo que se registra al
    /// terminar un caso, cómo queda agrupado y que la pantalla se arme sin reventar.
    ///
    /// Lo que se puede probar con NUnit en PlayMode está en Fishy.Album.Tests, al lado
    /// del núcleo. Acá está lo que esas pruebas no alcanzan a ver: la UI y el camino
    /// real desde <see cref="DetectiveCaseManager"/>, que viven en Assembly-CSharp —y
    /// un assembly de pruebas con .asmdef no puede referenciarlo—.
    ///
    /// Se corren desde el menú  Fishy ▸ Probar álbum de evidencias,
    /// o sin abrir el editor:
    ///
    ///   Unity.exe -batchmode -nographics -quit -projectPath "&lt;ruta&gt;" `
    ///             -executeMethod Fishy.EditorTools.FishyPruebasAlbum.Ejecutar `
    ///             -logFile -
    ///
    /// Termina con código 0 si todo pasa y 1 si algo falla.
    /// </summary>
    public static class FishyPruebasAlbum
    {
        private static int _ok;
        private static readonly List<string> _fallas = new List<string>();

        private const BindingFlags Privado = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags PrivadoEstatico = BindingFlags.Static | BindingFlags.NonPublic;

        [MenuItem("Fishy/Probar álbum de evidencias")]
        public static void Ejecutar()
        {
            _ok = 0;
            _fallas.Clear();

            var log = new StringBuilder();
            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine("PRUEBAS DEL ÁLBUM DE EVIDENCIAS (headless)");
            log.AppendLine(new string('=', 70));

            AsegurarApiLocal();

            // ── La pantalla ──
            ProbarElAlbumVacioSeAbreSinExcepciones(log);
            ProbarElAlbumVacioExplicaComoSeLlena(log);
            ProbarConPinesYSinEvidenciasElMensajeEsElOtro(log);
            ProbarConEvidenciasYSinPinesElMosaicoAvisa(log);
            ProbarElContenidoVaDentroDeUnScroll(log);
            ProbarElContenidoNoSeSaleDelPanel(log);
            ProbarAgrupadoPorCasoHayUnaTarjetaPorCaso(log);
            ProbarPorTematicaUnaEvidenciaDeDosEtiquetasSaleDosVeces(log);
            ProbarElDetalleTraeLaExplicacionYElContexto(log);
            ProbarLaAspaEsDibujadaYNoUnaEquisDeTexto(log);
            ProbarReabrirNoDuplicaFilas(log);
            ProbarUnPinSinIconoSigueDejandoLeerCualEs(log);

            // ── El camino real, desde el Modo Detective ──
            ProbarElManagerDejaUnaEvidenciaPorSenalBienMarcada(log);
            ProbarUnaSenalAmbiguaMarcadaNoEntraAlAlbum(log);
            ProbarRejugarMarcandoMenosNoBorraLoGanado(log);
            ProbarLosTresCasosLocalesTraenSusMetadatos(log);
            ProbarConciliarDesdeElRespaldoLocalCompletaLosEsqueletos(log);

            AlbumDeEvidencias.Limpiar();

            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine($"RESULTADO: {_ok} OK, {_fallas.Count} fallas");
            log.AppendLine(new string('=', 70));

            if (_fallas.Count > 0) Debug.LogError(log.ToString());
            else                   Debug.Log(log.ToString());

            if (Application.isBatchMode)
                EditorApplication.Exit(_fallas.Count > 0 ? 1 : 0);
        }

        // ── La pantalla ──────────────────────────────────────────────────────────

        /// <summary>
        /// CA5 — El álbum vacío se construye y se abre sin una sola excepción.
        ///
        /// Es la prueba que más veces se habría ganado el sueldo en esta pantalla: el
        /// álbum se arma entero en código, y un álbum vacío recorre caminos que el
        /// lleno no toca (ninguna tarjeta, ningún pin, el texto de "todavía no hay
        /// nada" dentro del layout). Se escucha la consola además de capturar la
        /// excepción, porque Unity se come las que pasan dentro de un callback de
        /// layout y solo quedan ahí.
        /// </summary>
        private static void ProbarElAlbumVacioSeAbreSinExcepciones(StringBuilder log)
        {
            Reiniciar();

            var ruidos = new List<string>();
            Application.LogCallback escucha = (condicion, pila, tipo) =>
            {
                if (tipo == LogType.Error || tipo == LogType.Exception || tipo == LogType.Assert)
                    ruidos.Add($"{tipo}: {condicion}");
            };

            bool exploto = false;
            AlbumEvidenciasUI ui = null;
            Application.logMessageReceived += escucha;
            try
            {
                ui = NuevoAlbum();
                Abrir(ui);
            }
            catch (Exception ex)
            {
                exploto = true;
                ruidos.Add(ex.ToString());
            }
            finally
            {
                Application.logMessageReceived -= escucha;
            }

            Comprobar(log, "el álbum vacío se construye y se abre sin una sola excepción",
                !exploto && ruidos.Count == 0,
                ruidos.Count == 0 ? "" : ruidos[0]);

            Soltar(ui);
        }

        private static void ProbarElAlbumVacioExplicaComoSeLlena(StringBuilder log)
        {
            Reiniciar();

            AlbumEvidenciasUI ui = NuevoAlbum();
            Abrir(ui);

            Comprobar(log, "el álbum vacío dice que las evidencias llegan al completar casos",
                Dice(ui, AlbumUITheme.Textos.VacioTotal), "");

            Soltar(ui);
        }

        /// <summary>CA4 — Quien cerró casos antes de que existiera el registro de
        /// señales tiene los pines y no tiene evidencias. Decirle lo mismo que a quien
        /// no ha jugado ("resuelve tu primer caso") sería mentirle.</summary>
        private static void ProbarConPinesYSinEvidenciasElMensajeEsElOtro(StringBuilder log)
        {
            Reiniciar();
            DarPin("PIN_VIGIA_SILENCIOSO");

            AlbumEvidenciasUI ui = NuevoAlbum();
            Abrir(ui);

            Comprobar(log, "con pines pero sin evidencias, el mensaje es el otro",
                Dice(ui, AlbumUITheme.Textos.VacioSoloPines) &&
                !Dice(ui, AlbumUITheme.Textos.VacioTotal), "");

            Soltar(ui);
        }

        /// <summary>Estado real: acertar una señal de cuatro deja evidencia y no
        /// alcanza para el pin.</summary>
        private static void ProbarConEvidenciasYSinPinesElMosaicoAvisa(StringBuilder log)
        {
            Reiniciar();
            AlbumDeEvidencias.Registrar(Evidencia("DC_CASO_01", "DC01_MSG03"));

            AlbumEvidenciasUI ui = NuevoAlbum();
            Abrir(ui);

            Comprobar(log, "con evidencias pero sin pines, el mosaico avisa y la lista se pinta igual",
                Dice(ui, AlbumUITheme.Textos.SinPines) && Tarjetas(ui).Count == 1,
                $"{Tarjetas(ui).Count} tarjeta(s)");

            Soltar(ui);
        }

        private static void ProbarElContenidoVaDentroDeUnScroll(StringBuilder log)
        {
            Reiniciar();
            AlbumEvidenciasUI ui = NuevoAlbum();
            Abrir(ui);

            var scroll = (ScrollRect)Campo(ui, "_scroll");
            var contenido = (RectTransform)Campo(ui, "_contenido");

            Comprobar(log, "el contenido del álbum va dentro de un ScrollRect",
                scroll != null && contenido != null && scroll.content == contenido &&
                scroll.GetComponent<RectMask2D>() != null && scroll.vertical,
                scroll == null ? "no hay ScrollRect" : "");

            Soltar(ui);
        }

        /// <summary>
        /// Con una evidencia por señal bien marcada —doce en los tres casos del
        /// banco— la lista no cabe en el panel. Antes no había scroll y lo de abajo
        /// quedaba fuera, sin ninguna forma de llegar.
        /// </summary>
        private static void ProbarElContenidoNoSeSaleDelPanel(StringBuilder log)
        {
            Reiniciar();
            foreach (string caso in new[] { "DC_CASO_01", "DC_CASO_02", "DC_CASO_03" })
                for (int i = 1; i <= 4; i++)
                    AlbumDeEvidencias.Registrar(Evidencia(caso, $"{caso}_MSG{i}"));

            AlbumEvidenciasUI ui = NuevoAlbum();
            Abrir(ui);
            Asentar(ui);

            var scroll = (ScrollRect)Campo(ui, "_scroll");
            var contenido = (RectTransform)Campo(ui, "_contenido");
            var panel = (RectTransform)scroll.transform.parent;

            float altoPanel = panel.rect.height;
            float altoVisible = ((RectTransform)scroll.transform).rect.height;
            float altoTodo = LayoutUtility.GetPreferredHeight(contenido);

            Comprobar(log, "el contenido no se sale del panel y el scroll aprovecha lo libre",
                altoPanel > 0f && altoVisible > 0f &&
                altoVisible <= altoPanel && altoVisible > altoPanel * 0.5f &&
                altoTodo > altoVisible,
                $"panel {altoPanel:F0}, visible {altoVisible:F0}, contenido {altoTodo:F0}");

            Soltar(ui);
        }

        private static void ProbarAgrupadoPorCasoHayUnaTarjetaPorCaso(StringBuilder log)
        {
            Reiniciar();
            AlbumDeEvidencias.Registrar(Evidencia("DC_CASO_01", "DC01_MSG03"));
            AlbumDeEvidencias.Registrar(Evidencia("DC_CASO_01", "DC01_MSG05"));
            AlbumDeEvidencias.Registrar(Evidencia("DC_CASO_02", "DC02_MSG03"));

            AlbumEvidenciasUI ui = NuevoAlbum();
            Abrir(ui);

            List<Transform> tarjetas = Tarjetas(ui);
            int filas = Filas(ui).Count;

            Comprobar(log, "agrupado por caso hay una tarjeta por caso, con su título y su zona",
                tarjetas.Count == 2 && filas == 3 &&
                Dice(ui, "Caso de prueba DC_CASO_01") && Dice(ui, "desconocidos · 2 evidencias"),
                $"{tarjetas.Count} tarjeta(s), {filas} fila(s)");

            Soltar(ui);
        }

        /// <summary>CA2 — Una evidencia con dos etiquetas aparece bajo las dos
        /// temáticas, con el nombre legible y no con la etiqueta cruda del banco.</summary>
        private static void ProbarPorTematicaUnaEvidenciaDeDosEtiquetasSaleDosVeces(StringBuilder log)
        {
            Reiniciar();
            AlbumDeEvidencias.Registrar(Evidencia("DC_CASO_01", "DC01_MSG03"));

            AlbumEvidenciasUI ui = NuevoAlbum();
            Abrir(ui);

            bool exploto = false;
            try { Invocar(ui, "CambiarAgrupacion", true); }
            catch { exploto = true; }

            Comprobar(log, "por temática la evidencia de dos etiquetas sale dos veces, con su nombre",
                !exploto && Tarjetas(ui).Count == 2 && Filas(ui).Count == 2 &&
                Dice(ui, "Contacto de desconocidos") && Dice(ui, "Datos privados") &&
                !Dice(ui, "grooming"),
                $"{Tarjetas(ui).Count} tarjeta(s), {Filas(ui).Count} fila(s)");

            Soltar(ui);
        }

        /// <summary>CA3 — Al elegir una evidencia se ve su explicación y el contexto
        /// del caso.</summary>
        private static void ProbarElDetalleTraeLaExplicacionYElContexto(StringBuilder log)
        {
            Reiniciar();
            EvidenciaAlbum evidencia = Evidencia("DC_CASO_01", "DC01_MSG03");
            AlbumDeEvidencias.Registrar(evidencia);

            AlbumEvidenciasUI ui = NuevoAlbum();
            Abrir(ui);

            var detalle = (GameObject)Campo(ui, "_detalle");
            bool apagadoAlPrincipio = !detalle.activeSelf;

            Invocar(ui, "MostrarEvidencia", evidencia);

            var chips = (RectTransform)Campo(ui, "_detalleChips");

            Comprobar(log, "al elegir una evidencia, el detalle trae la explicación y el contexto",
                apagadoAlPrincipio && detalle.activeSelf &&
                Dice(ui, evidencia.explicacion) &&          // la explicación
                Dice(ui, evidencia.texto) &&                // el mensaje citado
                Dice(ui, evidencia.casoTitulo) &&           // de qué caso es
                Dice(ui, evidencia.casoZona) &&             // dónde pasó
                Dice(ui, evidencia.autor) &&                // quién lo escribió
                chips.childCount == 2,                      // el tipo de señal
                $"detalle activo={detalle.activeSelf}, chips={chips.childCount}");

            Soltar(ui);
        }

        /// <summary>El aspa va dibujada con dos barras: ✕ no tiene glifo en Mango ni en
        /// la fuente de cuerpo, y salía un cuadrito. Ver FishyUIKit.Aspa.</summary>
        private static void ProbarLaAspaEsDibujadaYNoUnaEquisDeTexto(StringBuilder log)
        {
            Reiniciar();
            AlbumEvidenciasUI ui = NuevoAlbum();
            Abrir(ui);

            int barras = ui.GetComponentsInChildren<Transform>(true)
                .Count(t => t.name == "Aspa0" || t.name == "Aspa1");
            bool hayEquisDeTexto = ui.GetComponentsInChildren<TMP_Text>(true)
                .Any(t => t.text != null && t.text.Trim() == "X");

            Comprobar(log, "la aspa de cerrar es dibujada y no un glifo de texto",
                barras == 2 && !hayEquisDeTexto, $"{barras} barra(s)");

            Soltar(ui);
        }

        private static void ProbarReabrirNoDuplicaFilas(StringBuilder log)
        {
            Reiniciar();
            AlbumDeEvidencias.Registrar(Evidencia("DC_CASO_01", "DC01_MSG03"));

            AlbumEvidenciasUI ui = NuevoAlbum();
            Abrir(ui);
            Invocar(ui, "Cerrar");
            Abrir(ui);
            Asentar(ui);

            Comprobar(log, "reabrir el álbum no duplica filas",
                Tarjetas(ui).Count == 1 && Filas(ui).Count == 1,
                $"{Tarjetas(ui).Count} tarjeta(s), {Filas(ui).Count} fila(s)");

            Soltar(ui);
        }

        /// <summary>Un pin cuyo ItemData todavía no tiene ícono asignado se dibuja como
        /// un cuadro tenue, y su nombre se sigue pudiendo leer: así se ve que el premio
        /// está, aunque falte el dibujo.</summary>
        private static void ProbarUnPinSinIconoSigueDejandoLeerCualEs(StringBuilder log)
        {
            Reiniciar();

            var ficha = ScriptableObject.CreateInstance<ItemData>();
            ficha.itemId = "PIN_SIN_ICONO_DE_PRUEBA";
            ficha.itemName = "Pin sin dibujo";
            ficha.itemDescription = "Un pin al que todavía no le pusieron el ícono.";
            InventoryManager.Instance.AddItem(ficha, 1);

            AlbumEvidenciasUI ui = NuevoAlbum();
            Abrir(ui);

            string pista = TextoDelHover(ui);
            Image icono = ui.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(i => i.transform.name == "Icono");

            Comprobar(log, "un pin sin ícono sigue dejando leer cuál es",
                pista != null && pista.Contains("Pin sin dibujo") &&
                icono != null && icono.sprite == null && icono.color.a < 0.5f,
                pista ?? "no hay casilla de pin");

            Soltar(ui);
            UnityEngine.Object.DestroyImmediate(ficha);
        }

        // ── El camino real, desde el Modo Detective ──────────────────────────────

        /// <summary>CA1 de punta a punta: confirmar el caso deja una evidencia por
        /// señal bien marcada, con los metadatos del caso.</summary>
        private static void ProbarElManagerDejaUnaEvidenciaPorSenalBienMarcada(StringBuilder log)
        {
            Reiniciar();

            DetectiveCase caso = CasoLocal("DC_CASO_01");
            DetectiveCaseManager manager = NuevoManager();
            manager.CargarCaso(caso);
            manager.ToggleMarca("DC01_MSG03");
            manager.ToggleMarca("DC01_MSG05");
            manager.CalcularResultado();

            EvidenciaAlbum primera = AlbumDeEvidencias.Todas()
                .FirstOrDefault(e => e.mensajeId == "DC01_MSG03");

            Comprobar(log, "confirmar el caso deja una evidencia por señal bien marcada",
                AlbumDeEvidencias.Cantidad == 2 && primera != null &&
                primera.casoTitulo == "Los Mensajes del Puma al Pudú" &&
                primera.casoZona == "desconocidos" &&
                primera.autor == "Puma" &&
                !string.IsNullOrWhiteSpace(primera.texto) &&
                !string.IsNullOrWhiteSpace(primera.explicacion) &&
                primera.etiquetas.Contains("grooming"),
                $"{AlbumDeEvidencias.Cantidad} evidencia(s)");

            // Y el ítem del álbum llega aunque esto no haya alcanzado para el pin.
            var album = CatalogoItems.Buscar(AlbumEvidenciasUI.ItemIdAlbum);
            Comprobar(log, "el ítem del álbum llega con la primera evidencia",
                InventoryManager.Instance.GetQuantity(album) == 1,
                $"cantidad={InventoryManager.Instance.GetQuantity(album)}");

            UnityEngine.Object.DestroyImmediate(manager.gameObject);
        }

        private static void ProbarUnaSenalAmbiguaMarcadaNoEntraAlAlbum(StringBuilder log)
        {
            Reiniciar();

            DetectiveCase caso = CasoLocal("DC_CASO_01");
            DetectiveCaseManager manager = NuevoManager();
            manager.CargarCaso(caso);
            manager.ToggleMarca("DC01_MSG06");   // el ambiguo del caso 01
            manager.CalcularResultado();

            Comprobar(log, "una señal ambigua marcada no entra al álbum",
                AlbumDeEvidencias.Cantidad == 0, $"{AlbumDeEvidencias.Cantidad} evidencia(s)");

            UnityEngine.Object.DestroyImmediate(manager.gameObject);
        }

        /// <summary>El servidor pisa mensajes_marcados con el último intento. El álbum
        /// no: acumula la unión.</summary>
        private static void ProbarRejugarMarcandoMenosNoBorraLoGanado(StringBuilder log)
        {
            Reiniciar();

            DetectiveCase caso = CasoLocal("DC_CASO_01");
            DetectiveCaseManager manager = NuevoManager();

            manager.CargarCaso(caso);
            foreach (string id in new[] { "DC01_MSG03", "DC01_MSG05", "DC01_MSG07", "DC01_MSG09" })
                manager.ToggleMarca(id);
            manager.CalcularResultado();
            int despuesDelPrimerIntento = AlbumDeEvidencias.Cantidad;

            // Repetir el caso limpia las marcas; ahora marca una sola.
            manager.CargarCaso(caso);
            manager.ToggleMarca("DC01_MSG03");
            manager.CalcularResultado();

            Comprobar(log, "rejugar marcando menos señales no borra las evidencias ya ganadas",
                despuesDelPrimerIntento == 4 && AlbumDeEvidencias.Cantidad == 4,
                $"{despuesDelPrimerIntento} → {AlbumDeEvidencias.Cantidad}");

            UnityEngine.Object.DestroyImmediate(manager.gameObject);
        }

        /// <summary>
        /// Los tres casos del respaldo local traen título, zona y etiquetas, y el 03
        /// trae su explicación guiada.
        ///
        /// Guarda dos cosas a la vez. La primera, que la clave de las etiquetas en esos
        /// JSON sea <c>etiquetasMl</c>: los lee JsonUtility, que empareja por nombre
        /// exacto de campo, y con <c>etiquetas_ml</c> la lista quedaría vacía sin que
        /// nadie se entere. La segunda, que el caso 03 no revienta al confirmar: no
        /// tenía la clave <c>explicacionGuiada</c> y eso dejaba la lista en null.
        /// </summary>
        private static void ProbarLosTresCasosLocalesTraenSusMetadatos(StringBuilder log)
        {
            Reiniciar();

            foreach (string casoId in new[] { "DC_CASO_01", "DC_CASO_02", "DC_CASO_03" })
            {
                DetectiveCase caso = CasoLocal(casoId);
                Comprobar(log, $"el respaldo local de {casoId} trae título, zona, etiquetas y explicaciones",
                    caso != null &&
                    !string.IsNullOrWhiteSpace(caso.titulo) &&
                    !string.IsNullOrWhiteSpace(caso.zona) &&
                    caso.etiquetasMl != null && caso.etiquetasMl.Count == 2 &&
                    caso.explicacionGuiada != null && caso.explicacionGuiada.Count == 4,
                    caso == null ? "no se pudo cargar" :
                        $"{caso.titulo} | {caso.zona} | {caso.etiquetasMl.Count} etiqueta(s) | " +
                        $"{(caso.explicacionGuiada == null ? 0 : caso.explicacionGuiada.Count)} explicación(es)");
            }

            DetectiveCase caso03 = CasoLocal("DC_CASO_03");
            DetectiveCaseManager manager = NuevoManager();
            manager.CargarCaso(caso03);
            manager.ToggleMarca("DC03_MSG03");

            bool exploto = false;
            try { manager.CalcularResultado(); }
            catch { exploto = true; }

            Comprobar(log, "confirmar el caso 03 por el respaldo local no revienta", !exploto, "");

            UnityEngine.Object.DestroyImmediate(manager.gameObject);
        }

        /// <summary>Una evidencia sembrada desde el servidor llega solo con ids; el
        /// respaldo local que viaja en el juego le pone el texto, sin pedirle nada a la
        /// red.</summary>
        private static void ProbarConciliarDesdeElRespaldoLocalCompletaLosEsqueletos(StringBuilder log)
        {
            Reiniciar();

            AlbumDeEvidencias.SembrarIds("DC_CASO_01", new[] { "DC01_MSG03" });
            bool eraEsqueleto = AlbumDeEvidencias.CantidadIncompletas == 1;

            AlbumDetectivePuente.ConciliarLoQueFalte();

            EvidenciaAlbum completada = AlbumDeEvidencias.Todas().FirstOrDefault();

            Comprobar(log, "conciliar desde el respaldo local completa una evidencia sembrada con ids",
                eraEsqueleto && completada != null && completada.EstaCompleta &&
                completada.casoTitulo == "Los Mensajes del Puma al Pudú" &&
                completada.etiquetas.Contains("privacidad") &&
                !string.IsNullOrWhiteSpace(completada.explicacion) &&
                AlbumDeEvidencias.CantidadIncompletas == 0,
                completada == null ? "no quedó ninguna" : completada.texto);

            Comprobar(log, "el nombre del recurso local se deduce del id del caso",
                AlbumDetectivePuente.RecursoLocalDe("DC_CASO_02") == "detective_caso_02",
                AlbumDetectivePuente.RecursoLocalDe("DC_CASO_02"));
        }

        // ── Andamios ─────────────────────────────────────────────────────────────

        /// <summary>Cada prueba arranca con el álbum y la mochila vacíos: son estáticos
        /// y, si no, una prueba vería lo que dejó la anterior.</summary>
        private static void Reiniciar()
        {
            AlbumDeEvidencias.Limpiar();
            InventoryManager.Instance.Vaciar();
        }

        private static AlbumEvidenciasUI NuevoAlbum()
        {
            var go = new GameObject("AlbumDePrueba") { hideFlags = HideFlags.HideAndDontSave };
            var ui = go.AddComponent<AlbumEvidenciasUI>();
            // En edición Awake no corre solo.
            typeof(AlbumEvidenciasUI).GetMethod("Awake", Privado).Invoke(ui, null);
            return ui;
        }

        private static void Abrir(AlbumEvidenciasUI ui)
        {
            Invocar(ui, "Abrir");
            Asentar(ui);
        }

        /// <summary>Deja el canvas medido. En el juego lleva muchos frames vivo; en
        /// edición el escalador todavía no actuó y los rects medirían el tamaño crudo.
        /// Mismo truco que FishyPruebasDetectiveRecompensa.</summary>
        private static void Asentar(AlbumEvidenciasUI ui)
        {
            if (ui == null) return;

            foreach (CanvasScaler escalador in ui.GetComponentsInChildren<CanvasScaler>(true))
                typeof(CanvasScaler).GetMethod("Handle", Privado)?.Invoke(escalador, null);

            Canvas.ForceUpdateCanvases();

            var contenido = (RectTransform)Campo(ui, "_contenido");
            if (contenido != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contenido);
        }

        private static void Soltar(AlbumEvidenciasUI ui)
        {
            if (ui == null) return;

            // OnDestroy es el que suelta la suscripción a AlbumDeEvidencias.AlCambiar.
            typeof(AlbumEvidenciasUI).GetMethod("OnDestroy", Privado)?.Invoke(ui, null);
            UnityEngine.Object.DestroyImmediate(ui.gameObject);
            typeof(AlbumEvidenciasUI).GetField("_instance", PrivadoEstatico)?.SetValue(null, null);
        }

        private static object Campo(AlbumEvidenciasUI ui, string nombre)
            => typeof(AlbumEvidenciasUI).GetField(nombre, Privado)?.GetValue(ui);

        private static void Invocar(AlbumEvidenciasUI ui, string metodo, params object[] args)
            => typeof(AlbumEvidenciasUI).GetMethod(metodo, Privado).Invoke(ui, args);

        /// <summary>Si en la pantalla se lee ese texto, en cualquier TMP.</summary>
        private static bool Dice(AlbumEvidenciasUI ui, string fragmento)
        {
            if (string.IsNullOrEmpty(fragmento)) return false;

            return ui.GetComponentsInChildren<TMP_Text>(true)
                .Any(t => t.text != null && t.text.Contains(fragmento));
        }

        private static List<Transform> Tarjetas(AlbumEvidenciasUI ui)
            => Hijos(ui, "_contenido", "Grupo_");

        private static List<Transform> Filas(AlbumEvidenciasUI ui)
            => ui.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("Evidencia_"))
                .ToList();

        private static List<Transform> Hijos(AlbumEvidenciasUI ui, string campo, string prefijo)
        {
            var padre = (RectTransform)Campo(ui, campo);
            var hijos = new List<Transform>();
            if (padre == null) return hijos;

            for (int i = 0; i < padre.childCount; i++)
            {
                Transform hijo = padre.GetChild(i);
                if (hijo.name.StartsWith(prefijo)) hijos.Add(hijo);
            }
            return hijos;
        }

        /// <summary>El texto que la casilla de pin enseña al pasarle el mouse. PinHover
        /// es una clase anidada privada de la UI, así que se la busca por nombre.</summary>
        private static string TextoDelHover(AlbumEvidenciasUI ui)
        {
            foreach (MonoBehaviour comp in ui.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (comp == null || comp.GetType().Name != "PinHover") continue;

                return (string)comp.GetType()
                    .GetField("texto", BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(comp);
            }
            return null;
        }

        private static void DarPin(string itemId)
        {
            var pin = CatalogoItems.Buscar(itemId);
            if (pin != null) InventoryManager.Instance.AddItem(pin, 1);
        }

        /// <summary>Una evidencia con los metadatos del caso 01, sirva de ejemplo para
        /// cualquier caso.</summary>
        private static EvidenciaAlbum Evidencia(string casoId, string mensajeId)
        {
            return new EvidenciaAlbum
            {
                casoId = casoId,
                mensajeId = mensajeId,
                casoTitulo = $"Caso de prueba {casoId}",
                casoZona = "desconocidos",
                etiquetas = new List<string> { "grooming", "privacidad" },
                autor = "Puma",
                texto = $"mensaje {mensajeId}: oye, ¿cuántos años tienes? no le cuentes a nadie",
                explicacion = $"Explicación de {mensajeId}: preguntar la edad es una alerta.",
            };
        }

        private static DetectiveCase CasoLocal(string casoId)
            => DetectiveCaseLoader.LoadLocal(AlbumDetectivePuente.RecursoLocalDe(casoId));

        private static DetectiveCaseManager NuevoManager()
        {
            var go = new GameObject("DetectiveCaseManagerDePrueba")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            return go.AddComponent<DetectiveCaseManager>();
        }

        /// <summary>Reusa el ApiManager singleton si ya existe, o crea uno en modo
        /// local. Así ReportarProgreso corta antes de pegarle a la red, y el álbum no
        /// se ata a ninguna partida (no escribe en PlayerPrefs).</summary>
        private static void AsegurarApiLocal()
        {
            var api = ApiManager.Instance;
            if (api == null)
            {
                var go = new GameObject("ApiManagerDePrueba") { hideFlags = HideFlags.HideAndDontSave };
                api = go.AddComponent<ApiManager>();
            }

            var so = new SerializedObject(api);
            so.FindProperty("useLocalMode").boolValue = true;
            so.FindProperty("verboseLogs").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
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
