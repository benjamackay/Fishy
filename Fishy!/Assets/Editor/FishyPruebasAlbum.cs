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

            // ── Las dos pantallas del celular ──
            ProbarElCelularRecibeLasDosPaginasYUnBoton(log);
            ProbarLaPaginaVaciaSeAbreSinExcepciones(log);
            ProbarCadaPantallaVaciaDiceLoSuyo(log);
            ProbarLasPaginasCalcanElAspectoDelCelular(log);
            ProbarAgrupadoPorCasoHayUnaTarjetaPorCaso(log);
            ProbarPorTematicaUnaEvidenciaDeDosEtiquetasSaleDosVeces(log);
            ProbarElDetalleTraeLaExplicacionYElContexto(log);
            ProbarReabrirNoDuplicaFilas(log);
            ProbarLosPinesVanEnSuPropiaPantalla(log);
            ProbarUnPinSinIconoSigueDejandoLeerCualEs(log);
            ProbarCadaPuertaAbreSuPaginaYNoOtra(log);
            ProbarColocarDosVecesNoDuplica(log);
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

        // ── Las dos pantallas del celular ────────────────────────────────────────

        /// <summary>
        /// HDU-12 CA2 — El celular gana DOS páginas pero UN SOLO botón.
        ///
        /// Evidencias lleva botón en el inicio; recompensas no, porque se entra desde
        /// la mochila (ver <see cref="AlbumEvidenciasUI.Show"/>). Que su página exista
        /// igual, con su índice y su título, es justo lo que hace falta comprobar: una
        /// pantalla sin botón es fácil de dejar a medio montar y nadie se entera hasta
        /// que alguien pulsa el ítem.
        ///
        /// Y las dos listas de <see cref="PhoneMenuNavigation"/> tienen que seguir
        /// cuadrando: si se descuadran, un botón abre la pantalla del otro y el
        /// encabezado miente.
        /// </summary>
        private static void ProbarElCelularRecibeLasDosPaginasYUnBoton(StringBuilder log)
        {
            Reiniciar();
            PhoneMenuNavigation celular = NuevoCelularConAlbumes(log);
            if (celular == null) return;

            Transform bEvi = celular.home.transform.Find(AccesoAlbumEnElCelular.NombreBotonEvidencias);

            // Ningún botón del inicio puede llevar a la página de recompensas.
            int iEvi = AccesoAlbumEnElCelular.IndiceEvidencias;
            int iRec = AccesoAlbumEnElCelular.IndiceRecompensas;

            bool indices = iEvi >= 0 && iRec >= 0 && iEvi != iRec
                && iEvi < celular.pages.Length && iRec < celular.pages.Length
                && celular.pages.Length == celular.pageTitles.Length;

            bool emparejado = indices
                && celular.pages[iEvi].GetComponent<PaginaDeEvidencias>() != null
                && celular.pages[iRec].GetComponent<PaginaDeRecompensas>() != null
                && celular.pageTitles[iEvi] == AccesoAlbumEnElCelular.TituloEvidencias
                && celular.pageTitles[iRec] == AccesoAlbumEnElCelular.TituloRecompensas;

            Comprobar(log, "el celular gana dos páginas y solo el botón de evidencias",
                bEvi != null && emparejado,
                $"botón evidencias={bEvi != null}, evidencias={iEvi}, recompensas={iRec}, " +
                $"páginas={celular.pages.Length}, títulos={celular.pageTitles.Length}");

            SoltarCelular(celular);
        }

        /// <summary>
        /// CA4 — Las dos pantallas vacías se construyen y se abren sin una sola
        /// excepción.
        ///
        /// Es la prueba que más veces se habría ganado el sueldo acá: las pantallas se
        /// arman enteras en código, y una vacía recorre caminos que la llena no toca
        /// (ninguna tarjeta, ningún pin, el texto de "todavía no hay nada" dentro del
        /// layout). Se escucha la consola además de capturar la excepción, porque Unity
        /// se come las que pasan dentro de un callback de layout y solo quedan ahí.
        /// </summary>
        private static void ProbarLaPaginaVaciaSeAbreSinExcepciones(StringBuilder log)
        {
            Reiniciar();

            var ruidos = new List<string>();
            Application.LogCallback escucha = (condicion, pila, tipo) =>
            {
                if (tipo == LogType.Error || tipo == LogType.Exception || tipo == LogType.Assert)
                    ruidos.Add($"{tipo}: {condicion}");
            };

            bool exploto = false;
            PhoneMenuNavigation celular = null;
            Application.logMessageReceived += escucha;
            try
            {
                celular = NuevoCelularConAlbumes(null);
                if (celular != null)
                {
                    AbrirPagina(PaginaEvidencias(celular));
                    AbrirPagina(PaginaRecompensas(celular));
                }
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

            Comprobar(log, "las dos pantallas vacías se abren sin una sola excepción",
                !exploto && celular != null && ruidos.Count == 0,
                ruidos.Count == 0 ? "" : ruidos[0]);

            SoltarCelular(celular);
        }

        /// <summary>
        /// CA4 — Cada pantalla vacía dice lo suyo, y no lo del otro álbum.
        ///
        /// Es la razón de ser de la separación: antes, con las dos cosas en una
        /// pantalla, el mensaje de vacío tenía que hablar de pines y de evidencias a la
        /// vez y disculparse por la mitad del contenido. Ahora cada uno explica cómo se
        /// consigue lo que falta — y son reglas distintas: un pin pide más de la mitad
        /// de las señales de un caso; una evidencia la deja UNA bien marcada.
        /// </summary>
        private static void ProbarCadaPantallaVaciaDiceLoSuyo(StringBuilder log)
        {
            Reiniciar();
            PhoneMenuNavigation celular = NuevoCelularConAlbumes(log);
            if (celular == null) return;

            GameObject evi = PaginaEvidencias(celular);
            GameObject rec = PaginaRecompensas(celular);
            AbrirPagina(evi);
            AbrirPagina(rec);

            bool evidenciasBien = Dice(evi, "Modo Detective") && !Dice(evi, "pines");
            bool recompensasBien = Dice(rec, "pines") && !Dice(rec, "Modo Detective");

            Comprobar(log, "cada pantalla vacía explica cómo se llena ELLA, no la otra",
                evidenciasBien && recompensasBien,
                $"evidencias={evidenciasBien}, recompensas={recompensasBien}");

            SoltarCelular(celular);
        }

        /// <summary>
        /// Las páginas nuevas copian el fondo y el marco de una que ya exista en el
        /// prefab, en vez de traer su propio arte. Así el día que alguien cambie el
        /// aspecto del teléfono, estas dos lo siguen sin que nadie se acuerde de venir
        /// a tocarlas — que es justo lo que no pasaba cuando el álbum era una ventana
        /// aparte con su propia paleta.
        /// </summary>
        private static void ProbarLasPaginasCalcanElAspectoDelCelular(StringBuilder log)
        {
            Reiniciar();
            PhoneMenuNavigation celular = NuevoCelularConAlbumes(log);
            if (celular == null) return;

            GameObject modelo = celular.pages[0];
            GameObject evi = PaginaEvidencias(celular);
            AbrirPagina(evi);

            Sprite fondoModelo = modelo.GetComponent<Image>() != null
                ? modelo.GetComponent<Image>().sprite : null;
            Sprite fondoNuevo = evi.GetComponent<Image>() != null
                ? evi.GetComponent<Image>().sprite : null;

            Transform bordeModelo = modelo.transform.Find("Borde");
            Transform bordeNuevo = evi.transform.Find("Borde");
            Sprite spriteBordeModelo = bordeModelo != null
                ? bordeModelo.GetComponent<Image>().sprite : null;
            Sprite spriteBordeNuevo = bordeNuevo != null
                ? bordeNuevo.GetComponent<Image>().sprite : null;

            // El marco va el último para quedar por encima del contenido, igual que en
            // las páginas del prefab, y no debe comerse los clics.
            bool bordeArriba = bordeNuevo != null
                && bordeNuevo.GetSiblingIndex() == evi.transform.childCount - 1
                && !bordeNuevo.GetComponent<Image>().raycastTarget;

            Comprobar(log, "las páginas nuevas calcan el fondo y el marco del celular",
                fondoModelo != null && fondoNuevo == fondoModelo &&
                spriteBordeModelo != null && spriteBordeNuevo == spriteBordeModelo && bordeArriba,
                $"fondo={(fondoNuevo == fondoModelo)}, borde={(spriteBordeNuevo == spriteBordeModelo)}, " +
                $"borde arriba y sin raycast={bordeArriba}");

            SoltarCelular(celular);
        }

        private static void ProbarAgrupadoPorCasoHayUnaTarjetaPorCaso(StringBuilder log)
        {
            Reiniciar();

            AlbumDeEvidencias.Registrar(Evidencia("DC_CASO_01", "DC01_MSG03"));
            AlbumDeEvidencias.Registrar(Evidencia("DC_CASO_01", "DC01_MSG05"));
            AlbumDeEvidencias.Registrar(Evidencia("DC_CASO_02", "DC02_MSG02"));

            PhoneMenuNavigation celular = NuevoCelularConAlbumes(log);
            if (celular == null) return;

            GameObject evi = PaginaEvidencias(celular);
            AbrirPagina(evi);

            List<Transform> tarjetas = Tarjetas(evi);
            List<Transform> filas = Filas(evi);

            Comprobar(log, "agrupado por caso hay una tarjeta por caso, con su título y su zona",
                tarjetas.Count == 2 && filas.Count == 3 &&
                Dice(evi, "Caso de prueba DC_CASO_01") && Dice(evi, "desconocidos"),
                $"{tarjetas.Count} tarjeta(s), {filas.Count} fila(s)");

            SoltarCelular(celular);
        }

        private static void ProbarPorTematicaUnaEvidenciaDeDosEtiquetasSaleDosVeces(StringBuilder log)
        {
            Reiniciar();

            AlbumDeEvidencias.Registrar(Evidencia("DC_CASO_01", "DC01_MSG03"));

            PhoneMenuNavigation celular = NuevoCelularConAlbumes(log);
            if (celular == null) return;

            GameObject evi = PaginaEvidencias(celular);
            AbrirPagina(evi);

            // La misma evidencia lleva "grooming" y "privacidad": agrupando por
            // temática tiene que aparecer en los dos grupos, con su nombre bonito.
            Invocar(evi.GetComponent<PaginaDeEvidencias>(), "CambiarAgrupacion", true);
            Asentar(evi);

            List<Transform> tarjetas = Tarjetas(evi);

            Comprobar(log, "por temática la evidencia de dos etiquetas sale dos veces, con su nombre",
                tarjetas.Count == 2 && Filas(evi).Count == 2 &&
                Dice(evi, "Contacto de desconocidos") && Dice(evi, "Datos privados"),
                $"{tarjetas.Count} tarjeta(s), {Filas(evi).Count} fila(s)");

            SoltarCelular(celular);
        }

        private static void ProbarElDetalleTraeLaExplicacionYElContexto(StringBuilder log)
        {
            Reiniciar();

            AlbumDeEvidencias.Registrar(Evidencia("DC_CASO_01", "DC01_MSG03"));

            PhoneMenuNavigation celular = NuevoCelularConAlbumes(log);
            if (celular == null) return;

            GameObject evi = PaginaEvidencias(celular);
            AbrirPagina(evi);

            Transform fila = Filas(evi).FirstOrDefault();
            if (fila != null) fila.GetComponent<Button>().onClick.Invoke();
            Asentar(evi);

            GameObject detalle = Detalle(evi);

            Comprobar(log, "al elegir una evidencia, el detalle trae la explicación y el contexto",
                detalle != null && detalle.activeSelf &&
                Dice(evi, "Explicación de DC01_MSG03") &&   // por qué era una señal
                Dice(evi, "Caso de prueba DC_CASO_01") &&   // de qué caso
                Dice(evi, "Puma") &&                        // quién lo escribió
                Dice(evi, "Contacto de desconocidos"),      // el tipo de señal
                detalle == null ? "no hay tarjeta de detalle" : $"detalle activo={detalle.activeSelf}");

            SoltarCelular(celular);
        }

        private static void ProbarReabrirNoDuplicaFilas(StringBuilder log)
        {
            Reiniciar();

            AlbumDeEvidencias.Registrar(Evidencia("DC_CASO_01", "DC01_MSG03"));

            PhoneMenuNavigation celular = NuevoCelularConAlbumes(log);
            if (celular == null) return;

            GameObject evi = PaginaEvidencias(celular);

            // Entrar, salir y volver a entrar: el celular apaga y enciende las páginas
            // al navegar, y cada encendido repuebla. Si no se limpiara antes, cada
            // visita dejaría otra copia de la lista.
            AbrirPagina(evi);
            evi.SetActive(false);
            AbrirPagina(evi);
            AbrirPagina(evi);

            Comprobar(log, "entrar y salir de la pantalla no duplica filas",
                Tarjetas(evi).Count == 1 && Filas(evi).Count == 1,
                $"{Tarjetas(evi).Count} tarjeta(s), {Filas(evi).Count} fila(s)");

            SoltarCelular(celular);
        }

        private static void ProbarLosPinesVanEnSuPropiaPantalla(StringBuilder log)
        {
            Reiniciar();

            DarPin("PIN_VIGIA_SILENCIOSO");
            AlbumDeEvidencias.Registrar(Evidencia("DC_CASO_01", "DC01_MSG03"));

            PhoneMenuNavigation celular = NuevoCelularConAlbumes(log);
            if (celular == null) return;

            GameObject evi = PaginaEvidencias(celular);
            GameObject rec = PaginaRecompensas(celular);
            AbrirPagina(evi);
            AbrirPagina(rec);

            int pinesEnRecompensas = Pines(rec).Count;
            int pinesEnEvidencias = Pines(evi).Count;

            Comprobar(log, "los pines salen en recompensas y no se cuelan en evidencias",
                pinesEnRecompensas == 1 && pinesEnEvidencias == 0 && Filas(evi).Count == 1,
                $"pines en recompensas={pinesEnRecompensas}, en evidencias={pinesEnEvidencias}");

            SoltarCelular(celular);
        }

        private static void ProbarUnPinSinIconoSigueDejandoLeerCualEs(StringBuilder log)
        {
            Reiniciar();

            DarPin("PIN_VIGIA_SILENCIOSO");

            PhoneMenuNavigation celular = NuevoCelularConAlbumes(log);
            if (celular == null) return;

            GameObject rec = PaginaRecompensas(celular);
            AbrirPagina(rec);

            string hover = TextoDelHover(rec);
            Transform pin = Pines(rec).FirstOrDefault();
            if (pin != null) pin.GetComponent<Button>().onClick.Invoke();
            Asentar(rec);

            GameObject detalle = Detalle(rec);

            Comprobar(log, "un pin se puede identificar con el mouse y pulsándolo",
                !string.IsNullOrEmpty(hover) && detalle != null && detalle.activeSelf,
                hover ?? "(sin texto de hover)");

            SoltarCelular(celular);
        }

        /// <summary>
        /// Cada botón abre SU pantalla y ninguna otra.
        ///
        /// El botón se clona de un vecino, y el clonado trae su <c>onClick</c>
        /// guardado en el prefab —abrir el inventario—. Esas llamadas son persistentes
        /// y <c>RemoveAllListeners</c> no las quita: sin apagarlas una a una, pulsar
        /// «Evidencias» abriría además el inventario. Se comprueba pulsando y mirando
        /// qué queda encendido, no leyendo el estado de las llamadas: apagar una deja
        /// su nombre escrito en el objeto.
        /// </summary>
        private static void ProbarCadaPuertaAbreSuPaginaYNoOtra(StringBuilder log)
        {
            Reiniciar();
            PhoneMenuNavigation celular = NuevoCelularConAlbumes(log);
            if (celular == null) return;

            // El botón del menú lleva a evidencias; el ítem de la mochila, a
            // recompensas. Son las dos únicas puertas, y cada una abre la suya.
            bool botonBien = AbreSolo(celular, AccesoAlbumEnElCelular.IndiceEvidencias, () =>
            {
                Transform b = celular.home.transform.Find(AccesoAlbumEnElCelular.NombreBotonEvidencias);
                if (b != null) b.GetComponent<Button>().onClick.Invoke();
            }, out string detalleBoton);

            bool itemBien = AbreSolo(celular, AccesoAlbumEnElCelular.IndiceRecompensas,
                AlbumEvidenciasUI.Show, out string detalleItem);

            Comprobar(log, "el botón del menú abre evidencias y el ítem de la mochila, recompensas",
                botonBien && itemBien,
                string.Join(" | ", new[] { detalleBoton, detalleItem }
                    .Where(s => !string.IsNullOrEmpty(s))));

            SoltarCelular(celular);
        }

        /// <summary>
        /// Hace algo y comprueba que de ahí queda abierta esa página y solo esa.
        ///
        /// Mirar TODAS las páginas y no solo la esperada es el punto: el botón se
        /// clona de un vecino y trae su <c>onClick</c> guardado en el prefab —abrir el
        /// inventario—, que es persistente y <c>RemoveAllListeners</c> no quita. Si no
        /// se apagara una a una, quedarían DOS páginas abiertas y mirar solo la buena
        /// no lo vería.
        /// </summary>
        private static bool AbreSolo(PhoneMenuNavigation celular, int indice,
            Action abrir, out string detalle)
        {
            detalle = "";
            celular.ShowHome();
            abrir();

            for (int i = 0; i < celular.pages.Length; i++)
            {
                bool deberia = i == indice;
                if (celular.pages[i].activeSelf == deberia) continue;

                detalle = $"esperando la página {indice}, la {i} quedó " +
                          $"{(celular.pages[i].activeSelf ? "abierta" : "cerrada")} y no tocaba";
                return false;
            }
            return true;
        }

        private static void ProbarColocarDosVecesNoDuplica(StringBuilder log)
        {
            Reiniciar();
            PhoneMenuNavigation celular = NuevoCelularConAlbumes(log);
            if (celular == null) return;

            int botones = celular.home.transform.childCount;
            int paginas = celular.pages.Length;

            AccesoAlbumEnElCelular.Colocar(celular);
            AccesoAlbumEnElCelular.Colocar(celular);

            Comprobar(log, "colocar los álbumes tres veces deja dos botones y dos páginas",
                celular.home.transform.childCount == botones && celular.pages.Length == paginas,
                $"botones {botones} → {celular.home.transform.childCount}, " +
                $"páginas {paginas} → {celular.pages.Length}");

            SoltarCelular(celular);
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

        /// <summary>
        /// Un celular instanciado del prefab, ya con los dos álbumes puestos.
        ///
        /// Se instancia el prefab de verdad en vez de armar un doble: lo que hay que
        /// comprobar es justamente el encaje con él —la cuadrícula del inicio, el
        /// emparejamiento de páginas y títulos, el arte que se copia—, y un doble no
        /// se equivoca nunca en eso.
        /// </summary>
        private static PhoneMenuNavigation NuevoCelularConAlbumes(StringBuilder log)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PhoneMenu.prefab");
            if (prefab == null)
            {
                if (log != null)
                    Comprobar(log, "instanciar el prefab del celular", false,
                        "no está Assets/Prefabs/PhoneMenu.prefab");
                return null;
            }

            GameObject copia = UnityEngine.Object.Instantiate(prefab);
            var celular = copia.GetComponentInChildren<PhoneMenuNavigation>(true);
            if (celular == null)
            {
                UnityEngine.Object.DestroyImmediate(copia);
                return null;
            }

            AccesoAlbumEnElCelular.Colocar(celular);
            return celular;
        }

        private static void SoltarCelular(PhoneMenuNavigation celular)
        {
            if (celular != null)
                UnityEngine.Object.DestroyImmediate(celular.transform.root.gameObject);
        }

        private static GameObject PaginaEvidencias(PhoneMenuNavigation celular)
            => celular.pages[AccesoAlbumEnElCelular.IndiceEvidencias];

        private static GameObject PaginaRecompensas(PhoneMenuNavigation celular)
            => celular.pages[AccesoAlbumEnElCelular.IndiceRecompensas];

        /// <summary>
        /// Enciende una página y la deja medida.
        ///
        /// En el juego de esto se encarga Unity: la página se activa y corren su Awake
        /// (que la construye) y su OnEnable (que la repuebla). En edición no corre
        /// ninguno de los dos, así que se llaman a mano — y Awake solo la primera vez,
        /// porque construir dos veces dejaría dos scrolls encima del otro.
        /// </summary>
        private static void AbrirPagina(GameObject pagina)
        {
            if (pagina == null) return;

            var comp = pagina.GetComponent<PaginaDeAlbum>();
            pagina.SetActive(true);

            if (pagina.transform.Find("Scroll") == null) Invocar(comp, "Awake");
            Invocar(comp, "OnEnable");

            Asentar(pagina);
        }

        /// <summary>Deja la página medida. En el juego lleva muchos frames viva; en
        /// edición el escalador todavía no actuó y los rects medirían el tamaño
        /// crudo.</summary>
        private static void Asentar(GameObject pagina)
        {
            if (pagina == null) return;

            foreach (CanvasScaler escalador in
                     pagina.GetComponentsInParent<CanvasScaler>(true))
                typeof(CanvasScaler).GetMethod("Handle", Privado)?.Invoke(escalador, null);

            Canvas.ForceUpdateCanvases();

            RectTransform contenido = Contenido(pagina);
            if (contenido != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contenido);
        }

        /// <summary>El contenido del scroll, que es donde cada página cuelga lo suyo.
        /// Es una propiedad protegida de la base, así que se lee por reflexión.</summary>
        private static RectTransform Contenido(GameObject pagina)
        {
            var comp = pagina.GetComponent<PaginaDeAlbum>();
            if (comp == null) return null;

            return typeof(PaginaDeAlbum)
                .GetProperty("Contenido", Privado)
                ?.GetValue(comp) as RectTransform;
        }

        /// <summary>La tarjeta de detalle de esa página (campo privado de la base).</summary>
        private static GameObject Detalle(GameObject pagina)
        {
            var comp = pagina.GetComponent<PaginaDeAlbum>();
            if (comp == null) return null;

            return typeof(PaginaDeAlbum).GetField("_detalle", Privado)?.GetValue(comp) as GameObject;
        }

        private static void Invocar(MonoBehaviour comp, string metodo, params object[] args)
        {
            if (comp == null) return;
            comp.GetType().GetMethod(metodo, Privado)?.Invoke(comp, args);
        }

        /// <summary>Si en la pantalla se lee ese texto, en cualquier TMP.</summary>
        private static bool Dice(GameObject pagina, string fragmento)
        {
            if (pagina == null || string.IsNullOrEmpty(fragmento)) return false;

            return pagina.GetComponentsInChildren<TMP_Text>(true)
                .Any(t => t.text != null && t.text.Contains(fragmento));
        }

        private static List<Transform> Tarjetas(GameObject pagina)
            => HijosDelContenido(pagina, "Grupo_");

        private static List<Transform> Filas(GameObject pagina)
            => PorPrefijo(pagina, "Evidencia_");

        private static List<Transform> Pines(GameObject pagina)
            => PorPrefijo(pagina, "Pin");

        private static List<Transform> HijosDelContenido(GameObject pagina, string prefijo)
        {
            var hijos = new List<Transform>();
            RectTransform contenido = Contenido(pagina);
            if (contenido == null) return hijos;

            for (int i = 0; i < contenido.childCount; i++)
            {
                Transform hijo = contenido.GetChild(i);
                if (hijo.name.StartsWith(prefijo)) hijos.Add(hijo);
            }
            return hijos;
        }

        private static List<Transform> PorPrefijo(GameObject pagina, string prefijo)
        {
            if (pagina == null) return new List<Transform>();

            return pagina.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith(prefijo) && t.gameObject != pagina)
                .ToList();
        }

        /// <summary>El texto que la casilla de pin enseña al pasarle el mouse. PinHover
        /// es una clase anidada privada de la página, así que se la busca por nombre.</summary>
        private static string TextoDelHover(GameObject pagina)
        {
            foreach (MonoBehaviour comp in pagina.GetComponentsInChildren<MonoBehaviour>(true))
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
