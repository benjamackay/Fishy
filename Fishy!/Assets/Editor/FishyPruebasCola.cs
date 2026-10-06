#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Fishy.Net;
using Fishy.World;
using UnityEditor;
using UnityEngine;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Pruebas headless de la cola de cambios: qué se fusiona, en qué orden sale, qué se
    /// descarta y qué se pierde.
    ///
    /// Se prueba aquí y no contra el backend porque lo que puede romperse son las reglas,
    /// no las peticiones: que un desbloqueo de zona pise un completado, que el progreso
    /// retroceda, que un cierre diga "guardado" con cambios en el aire. Nada de eso se ve
    /// en un log de red — se ve en un contador.
    ///
    /// El <c>Enviar</c> de cada prueba es una función de mentira que decide al momento si
    /// sale bien o mal. Así se pueden provocar a voluntad los casos que en un servidor de
    /// verdad son difíciles de conseguir: el que falla siempre, el que no contesta nunca.
    ///
    /// Se corren desde el menú  Fishy ▸ Probar cola de cambios,  o sin abrir el editor:
    ///
    ///   Unity.exe -batchmode -nographics -quit -projectPath "&lt;ruta&gt;" `
    ///             -executeMethod Fishy.EditorTools.FishyPruebasCola.Ejecutar `
    ///             -logFile -
    ///
    /// Termina con código 0 si todo pasa y 1 si algo falla.
    /// </summary>
    public static class FishyPruebasCola
    {
        private static int _ok;
        private static readonly List<string> _fallas = new List<string>();

        private const int PartidaA = 991001;
        private const int PartidaB = 991002;

        [MenuItem("Fishy/Probar cola de cambios")]
        public static void Ejecutar()
        {
            _ok = 0;
            _fallas.Clear();

            var log = new StringBuilder();
            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine("PRUEBAS DE LA COLA DE CAMBIOS (headless)");
            log.AppendLine(new string('=', 70));

            LimpiarSobras();

            var api = ApiLocal();
            var cola = ColaNueva();
            UsarPartida(api, PartidaA);

            // ── Reglas del almacén: no hace falta vaciar para comprobarlas ──
            ProbarCoalescenciaDeSnapshot(log);
            ProbarDedupPorClave(log);
            ProbarZonaFusionaPorOr(log);
            ProbarProgresoFusionaPorMaximo(log);
            ProbarReencolarConservaLaPosicion(log);
            ProbarFalloViejoNoPisaAlNuevo(log);
            ProbarOlvidar(log);

            // ── El vaciado ──
            ProbarVaciadoSubeTodo(log, cola);
            ProbarOrdenDeSalida(log, cola);
            ProbarCadenaVaSola(log, cola);
            ProbarElThunkLeeAlVaciar(log, cola);
            ProbarInsisteHastaVaciarLaCola(log, cola);
            ProbarInsistirTienePlazo(log, cola);
            ProbarCierreTambienInsiste(log, cola);
            ProbarSinRespuestaNoEsTodoBien(log, cola);
            ProbarNoSeGuardaLaPosicionAntesDeRestaurar(log, cola, api);
            ProbarVolverAlMenuNoPisaLaPosicion(log, cola, api);
            ProbarSelloDePartida(log, cola, api);
            ProbarTopeDeTiempoSeRestaura(log, cola, api);
            ProbarElPlazoSoloRecortaLosEnviosDeLaCola(log, cola, api);
            ProbarElUltimoGuardadoFijaElPlazo(log, cola, api);

            // ── Los interruptores de SaveManager ──
            ProbarInterruptorDeMomentos(log, cola);
            ProbarGuardarActualizaLaFechaDeLaPartida(log, api);

            Limpiar(cola, api);

            log.AppendLine();
            log.AppendLine(new string('=', 70));
            log.AppendLine($"RESULTADO: {_ok} OK, {_fallas.Count} fallas");
            log.AppendLine(new string('=', 70));

            if (_fallas.Count > 0) Debug.LogError(log.ToString());
            else                   Debug.Log(log.ToString());

            if (Application.isBatchMode)
                EditorApplication.Exit(_fallas.Count > 0 ? 1 : 0);
        }

        // ── Reglas del almacén ────────────────────────────────────────────────

        private static void ProbarCoalescenciaDeSnapshot(StringBuilder log)
        {
            Vaciar();
            for (int i = 0; i < 3; i++) ColaDeCambios.EncolarSnapshot("inventario", Bien());

            Comprobar(log, "Tres veces el mismo snapshot son una sola entrada",
                ColaDeCambios.Pendientes == 1, $"{ColaDeCambios.Pendientes} pendientes");
        }

        private static void ProbarDedupPorClave(StringBuilder log)
        {
            Vaciar();
            string subido = null;
            ColaDeCambios.EncolarAppend("objeto:FLOR", Marcar(() => subido = "primero"));
            ColaDeCambios.EncolarAppend("objeto:FLOR", Marcar(() => subido = "segundo"));

            Comprobar(log, "Encolar dos veces la misma clave deja una entrada",
                ColaDeCambios.Pendientes == 1, $"{ColaDeCambios.Pendientes} pendientes");

            // El valor que queda es el último, no el primero.
            Primero().Resolver()(() => { }, _ => { });
            Comprobar(log, "Y se queda con el último valor, no con el primero",
                subido == "segundo", $"subió '{subido}'");
        }

        private static void ProbarZonaFusionaPorOr(StringBuilder log)
        {
            // El caso real: BosqueDesconocidosManager marca la zona completada y después
            // MisionBackendSync la "desbloquea" al recalcular. Si ganara el último, el
            // reporte del adulto mostraría como pendiente una zona ya terminada.
            Vaciar();
            ColaDeCambios.EncolarZona("desconocidos", completada: true);
            ColaDeCambios.EncolarZona("desconocidos", completada: false);

            Comprobar(log, "Completar una zona no se puede deshacer con un desbloqueo",
                ColaDeCambios.Pendientes == 1 && Primero().Valor is bool b && b,
                $"valor = {Primero().Valor}");

            // Y al revés también: primero desbloquear, después completar.
            Vaciar();
            ColaDeCambios.EncolarZona("retos", completada: false);
            ColaDeCambios.EncolarZona("retos", completada: true);
            Comprobar(log, "Y completarla después del desbloqueo sí cuenta",
                Primero().Valor is bool c && c, $"valor = {Primero().Valor}");
        }

        private static void ProbarProgresoFusionaPorMaximo(StringBuilder log)
        {
            Vaciar();
            ColaDeCambios.EncolarProgreso(25f);
            ColaDeCambios.EncolarProgreso(10f);

            Comprobar(log, "El progreso de la partida nunca retrocede",
                ColaDeCambios.Pendientes == 1 && Primero().Valor is float v && Mathf.Approximately(v, 25f),
                $"valor = {Primero().Valor}");
        }

        private static void ProbarReencolarConservaLaPosicion(StringBuilder log)
        {
            // Si reencolar reordenara, un snapshot que se repite mucho —la posición de
            // Otto, que se marca en cada guardado— se iría colando por delante de hechos
            // más viejos y esos serían los que se quedan fuera cuando vence el plazo.
            Vaciar();
            ColaDeCambios.EncolarSnapshot("personaje", Bien());
            ColaDeCambios.EncolarAppend("objeto:CARACOL", Bien());
            ColaDeCambios.EncolarSnapshot("personaje", Bien());

            var claves = Claves();
            Comprobar(log, "Reencolar una clave no la adelanta en la fila",
                claves.Count == 2 && claves[0] == "personaje" && claves[1] == "objeto:CARACOL",
                string.Join(" → ", claves));
        }

        private static void ProbarFalloViejoNoPisaAlNuevo(StringBuilder log)
        {
            // Un envío que falla vuelve a la cola. Si mientras tanto entró uno más nuevo con
            // la misma clave, el viejo no puede pisarlo: el "completada" nuevo se perdería.
            Vaciar();
            var almacen = ColaDeCambios.AlmacenParaPruebas;

            ColaDeCambios.EncolarAppend("mision:X", Bien(), "disponible");
            var viejo = almacen.Tomar("mision:X");
            ColaDeCambios.EncolarAppend("mision:X", Bien(), "completada");
            almacen.Reencolar(viejo);

            Comprobar(log, "Un fallo viejo no pisa al cambio más nuevo de su clave",
                ColaDeCambios.Pendientes == 1 && Primero().Descripcion == "completada",
                $"{ColaDeCambios.Pendientes} pendientes, descripción = {(ColaDeCambios.Pendientes > 0 ? Primero().Descripcion : "-")}");

            // Con fusión (zona por OR) se combinan: un "desbloqueada" que falló no puede
            // borrar un "completada" que entró después.
            Vaciar();
            ColaDeCambios.EncolarZona("desconocidos", false);
            var zonaVieja = almacen.Tomar("zona:desconocidos");
            ColaDeCambios.EncolarZona("desconocidos", true);
            almacen.Reencolar(zonaVieja);

            Comprobar(log, "Una zona 'desbloqueada' que falló no borra la 'completada' posterior",
                ColaDeCambios.Pendientes == 1 && Primero().Valor is bool b && b,
                $"valor = {(ColaDeCambios.Pendientes > 0 ? Primero().Valor : null)}");

            // Tomar saca lo que hay AHORA, no una copia vieja.
            Vaciar();
            ColaDeCambios.EncolarAppend("detective:C", Bien(), "v1");
            ColaDeCambios.EncolarAppend("detective:C", Bien(), "v2");
            var tomado = almacen.Tomar("detective:C");
            Comprobar(log, "Tomar devuelve la versión más reciente y deja la cola vacía",
                tomado != null && tomado.Descripcion == "v2" && ColaDeCambios.Pendientes == 0,
                $"tomado = {tomado?.Descripcion}, {ColaDeCambios.Pendientes} pendientes");
        }

        private static void ProbarOlvidar(StringBuilder log)
        {
            Vaciar();
            ColaDeCambios.EncolarAppend("npc:ALEX", Bien());
            ColaDeCambios.Olvidar("npc:ALEX");

            Comprobar(log, "Olvidar saca el cambio sin mandarlo",
                ColaDeCambios.Pendientes == 0, $"{ColaDeCambios.Pendientes} pendientes");
        }

        // ── El vaciado ────────────────────────────────────────────────────────

        private static void ProbarVaciadoSubeTodo(StringBuilder log, ColaDeCambios cola)
        {
            Vaciar();
            int subidos = 0;
            for (int i = 0; i < 5; i++)
                ColaDeCambios.EncolarAppend($"objeto:{i}", Marcar(() => subidos++));

            Correr(cola.Vaciar("prueba", 5f, reintentarSiFalla: false));

            Comprobar(log, "Un vaciado normal sube todo y deja la cola vacía",
                subidos == 5 && ColaDeCambios.Pendientes == 0 && cola.UltimoResultado.TodoBien,
                $"{subidos} subidos, {ColaDeCambios.Pendientes} pendientes");
        }

        private static void ProbarOrdenDeSalida(StringBuilder log, ColaDeCambios cola)
        {
            Vaciar();
            var orden = new List<string>();
            ColaDeCambios.EncolarAppend("uno",  Marcar(() => orden.Add("uno")));
            ColaDeCambios.EncolarAppend("dos",  Marcar(() => orden.Add("dos")));
            ColaDeCambios.EncolarAppend("tres", Marcar(() => orden.Add("tres")));

            Correr(cola.Vaciar("prueba", 5f, reintentarSiFalla: false));

            Comprobar(log, "Salen en el orden en que se encolaron",
                orden.Count == 3 && orden[0] == "uno" && orden[1] == "dos" && orden[2] == "tres",
                string.Join(" → ", orden));
        }

        private static void ProbarCadenaVaSola(StringBuilder log, ColaDeCambios cola)
        {
            // Una conversación de chat son varias peticiones encadenadas que usan NpcId y
            // ChatId de ApiManager, que es estado global: si otra cadena se solapara, la
            // segunda escribiría sus mensajes en el chat de la primera.
            Vaciar();
            var traza = new List<string>();
            int dentro = 0;
            bool seSolaparon = false;

            Action<Action, Action<string>> cadena = (ok, error) =>
            {
                dentro++;
                if (dentro > 1) seSolaparon = true;
                traza.Add("cadena");
                dentro--;
                ok();
            };

            ColaDeCambios.EncolarAppend("antes", Marcar(() => traza.Add("antes")));
            ColaDeCambios.EncolarCadena("chat:0", cadena);
            ColaDeCambios.EncolarCadena("chat:1", cadena);
            ColaDeCambios.EncolarAppend("despues", Marcar(() => traza.Add("despues")));

            Correr(cola.Vaciar("prueba", 5f, reintentarSiFalla: false));

            Comprobar(log, "Las cadenas van de una en una y en su sitio",
                !seSolaparon && traza.Count == 4 && traza[0] == "antes" && traza[3] == "despues",
                string.Join(" → ", traza));
        }

        private static void ProbarElThunkLeeAlVaciar(StringBuilder log, ColaDeCambios cola)
        {
            // Es toda la gracia de la familia Snapshot: se encola una marca de "esto está
            // sucio", no una copia de los datos. Si copiara, la mochila que sube sería la
            // de hace tres flores.
            Vaciar();
            string mochila = "una flor";
            string subido = null;
            ColaDeCambios.EncolarSnapshot("inventario", (ok, error) => { subido = mochila; ok(); });

            mochila = "tres flores y un caracol";
            Correr(cola.Vaciar("prueba", 5f, reintentarSiFalla: false));

            Comprobar(log, "Un snapshot sube el estado de cuando se vacía, no el de cuando se encoló",
                subido == "tres flores y un caracol", $"subió '{subido}'");
        }

        private static void ProbarInsisteHastaVaciarLaCola(StringBuilder log, ColaDeCambios cola)
        {
            // El contrato nuevo: un cambio de zona no da UNA pasada, insiste hasta vaciar
            // la cola. Antes lo que fallaba esperaba al siguiente momento de guardado, y
            // como los momentos son solo dos ese siguiente podía estar a media partida.
            Vaciar();
            int intentos = 0;
            ColaDeCambios.EncolarAppend("objeto:TERCO", (ok, error) =>
            {
                intentos++;
                if (intentos < 3) error("la red falló");
                else ok();
            });

            Correr(cola.Vaciar("zona", 5f, reintentarSiFalla: true));

            Comprobar(log, "Un cambio de zona insiste hasta vaciar la cola",
                intentos == 3 && ColaDeCambios.Pendientes == 0 && cola.UltimoResultado.TodoBien,
                $"{intentos} intento(s), {ColaDeCambios.Pendientes} pendientes, " +
                $"fallidos = {cola.UltimoResultado.Fallidos}");

            // Y los fallos del camino no ensucian el veredicto: si al final entró, entró.
            Comprobar(log, "Haber fallado en el camino no impide que el vaciado sea correcto",
                cola.UltimoResultado.Fallidos == 2 && cola.UltimoResultado.TodoBien,
                $"fallidos = {cola.UltimoResultado.Fallidos}, " +
                $"todo bien = {cola.UltimoResultado.TodoBien}");
        }

        private static void ProbarInsistirTienePlazo(StringBuilder log, ColaDeCambios cola)
        {
            // Insistir no puede ser para siempre: contra un servidor caído el plazo es lo
            // que evita que el juego se quede dando vueltas. Lo que no salga se queda en
            // la cola para el siguiente cambio de zona.
            Vaciar();
            int intentos = 0;
            ColaDeCambios.EncolarAppend("objeto:IMPOSIBLE", (ok, error) => { intentos++; error("cae"); });

            Correr(cola.Vaciar("zona", 1f, reintentarSiFalla: true));

            Comprobar(log, "Insistir se acaba con el plazo y lo pendiente sigue en la cola",
                intentos > 1 && ColaDeCambios.Pendientes == 1 && !cola.UltimoResultado.TodoBien,
                $"{intentos} intento(s), {ColaDeCambios.Pendientes} pendientes");
        }

        private static void ProbarCierreTambienInsiste(StringBuilder log, ColaDeCambios cola)
        {
            // Antes al cerrar NO se reintentaba, con el argumento de que no habría otra
            // oportunidad. Es al revés: como no hay otra, el plazo hay que gastarlo
            // justamente en insistir. El juego no cierra hasta que la cola esté vacía o
            // hasta que el jugador diga que se va.
            Vaciar();
            int intentos = 0;
            ColaDeCambios.EncolarAppend("objeto:TERCO", (ok, error) =>
            {
                intentos++;
                if (intentos < 2) error("la red falló");
                else ok();
            });

            Correr(cola.Vaciar("CierreDeAplicacion", 5f, reintentarSiFalla: true));

            Comprobar(log, "Al cerrar también se insiste, en vez de rendirse al primer fallo",
                intentos == 2 && ColaDeCambios.Pendientes == 0 && cola.UltimoResultado.TodoBien,
                $"{intentos} intento(s), {ColaDeCambios.Pendientes} pendientes");
        }

        private static void ProbarNoSeGuardaLaPosicionAntesDeRestaurar(
            StringBuilder log, ColaDeCambios cola, ApiManager api)
        {
            // El bug del 29 de septiembre, visto en la partida 6: Otto aparece en el
            // spawnPoint y la resolución inicial de zona —de null a zona_1— cuenta como
            // cambio de zona. Así que el PRIMER guardado salía antes de que llegara la
            // respuesta del GET y escribía el spawnPoint encima de la posición guardada;
            // el GET leía después lo que ese PATCH acababa de pisar y "restauraba" a Otto
            // justo donde empieza. En el log salía como
            // `Otto restaurado en (0,0, -8,0)` con la base diciendo (-5,89, 51,44).
            Vaciar();
            UsarPartida(api, PartidaA);

            // Donde quedó Otto la vez anterior.
            api.GuardarPersonaje("MainScene", -5.89f, 51.44f, "zona_3");

            // Otto acaba de aparecer en el spawnPoint, y todavía no se restauró nada.
            //
            // OJO: sin HideAndDontSave. `BuscarOtto` usa FindAnyObjectByType, que NO ve
            // los objetos ocultos, así que con esa bandera la prueba pasaría por el
            // motivo equivocado —no encontraría a Otto y se saldría por "escena de menú"—.
            var ottoGO = new GameObject("OttoDePrueba");
            ottoGO.transform.position = new Vector3(0f, -8f, 0f);
            ottoGO.AddComponent<Fishy.World.OttoController>();

            var syncGO = new GameObject("PersonajeSyncDePrueba");
            var sync = syncGO.AddComponent<PersonajeBackendSync>();

            sync.MarcarSucio();
            Correr(cola.Vaciar("zona", 5f, reintentarSiFalla: false));

            PersonajeDto leido = null;
            api.ObtenerPersonaje(onSuccess: d => leido = d);

            bool intacta = leido != null && leido.tiene_posicion &&
                           leido.pos_x.HasValue && Mathf.Approximately(leido.pos_x.Value, -5.89f);

            Comprobar(log, "No se guarda la posición antes de haberla restaurado",
                intacta,
                leido == null ? "no se pudo leer"
                              : $"quedó en ({leido.pos_x}, {leido.pos_y}), zona {leido.zona_actual}");

            UnityEngine.Object.DestroyImmediate(ottoGO);
            UnityEngine.Object.DestroyImmediate(syncGO);
            SoltarInstancia(typeof(PersonajeBackendSync));
        }

        private static void ProbarVolverAlMenuNoPisaLaPosicion(
            StringBuilder log, ColaDeCambios cola, ApiManager api)
        {
            // Esc → volver al menú → Continuar con la misma partida. PersonajeBackendSync
            // recordaba "ya restaurado en MainScene para esta partida", así que al volver
            // dejaba a Otto en el spawnPoint y daba por abierto el candado de no guardar
            // antes de restaurar: el siguiente guardado pisaba la posición buena.
            Vaciar();
            UsarPartida(api, PartidaA);
            api.GuardarPersonaje("MainScene", -5.89f, 51.44f, "zona_3");

            var ottoGO = new GameObject("OttoDePrueba");
            ottoGO.transform.position = new Vector3(0f, -8f, 0f);
            ottoGO.AddComponent<Fishy.World.OttoController>();

            var syncGO = new GameObject("PersonajeSyncDePrueba");
            var sync = syncGO.AddComponent<PersonajeBackendSync>();

            // Quien estaba jugando: la posición de esta partida ya se restauró en esta escena.
            var tipo = typeof(PersonajeBackendSync);
            const BindingFlags privado = BindingFlags.Instance | BindingFlags.NonPublic;
            tipo.GetField("partidaAtendida", privado).SetValue(sync, (int?)PartidaA);
            tipo.GetField("escenaAtendida", privado)
                .SetValue(sync, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

            // Control: con eso puesto el guardado SÍ sale. Si no saliera, la comprobación
            // de abajo pasaría por el motivo equivocado.
            sync.MarcarSucio();
            Correr(cola.Vaciar("zona", 5f, reintentarSiFalla: false));
            PersonajeDto control = null;
            api.ObtenerPersonaje(onSuccess: d => control = d);
            bool controlOk = control != null && control.pos_x.HasValue &&
                             Mathf.Approximately(control.pos_x.Value, 0f);
            Comprobar(log, "Con la posición ya restaurada, el guardado sí la sube (control)",
                controlOk, control == null ? "no se pudo leer" : $"quedó en ({control.pos_x}, {control.pos_y})");

            api.GuardarPersonaje("MainScene", -5.89f, 51.44f, "zona_3");

            // En modo edición no corre OnEnable, que es donde se suscribe.
            var alCerrar = (Action)Delegate.CreateDelegate(typeof(Action), sync,
                tipo.GetMethod("AlCerrarPartida", privado));
            ApiManager.OnPartidaCerrada += alCerrar;
            try
            {
                api.CerrarPartida();
                UsarPartida(api, PartidaA);

                // La escena volvió a cargar y Otto está otra vez en el spawnPoint.
                sync.MarcarSucio();
                Correr(cola.Vaciar("zona", 5f, reintentarSiFalla: false));
            }
            finally
            {
                ApiManager.OnPartidaCerrada -= alCerrar;
            }

            PersonajeDto leido = null;
            api.ObtenerPersonaje(onSuccess: d => leido = d);
            bool intacta = leido != null && leido.tiene_posicion &&
                           leido.pos_x.HasValue && Mathf.Approximately(leido.pos_x.Value, -5.89f);

            Comprobar(log, "Volver al menú y entrar a la misma partida no pisa la posición",
                intacta,
                leido == null ? "no se pudo leer"
                              : $"quedó en ({leido.pos_x}, {leido.pos_y}), zona {leido.zona_actual}");

            UnityEngine.Object.DestroyImmediate(ottoGO);
            UnityEngine.Object.DestroyImmediate(syncGO);
            SoltarInstancia(typeof(PersonajeBackendSync));
        }

        private static void ProbarSinRespuestaNoEsTodoBien(StringBuilder log, ColaDeCambios cola)
        {
            // El caso peligroso: la petición sale y nadie contesta. Si eso no se contara,
            // el resultado diría "todo bien" y el cierre daría por guardado un cambio que
            // se acaba de perder.
            Vaciar();
            ColaDeCambios.EncolarAppend("objeto:MUDO", (ok, error) => { /* nunca contesta */ });

            Correr(cola.Vaciar("CierreDeAplicacion", 0.5f, reintentarSiFalla: false), tope: 10f);

            var r = cola.UltimoResultado;
            Comprobar(log, "Lo que sale y no contesta cuenta como perdido, no como guardado",
                !r.TodoBien && r.SinRespuesta == 1 && r.Pendientes == 1,
                $"subidos {r.Subidos}, fallidos {r.Fallidos}, " +
                $"sin respuesta {r.SinRespuesta}, sin intentar {r.SinIntentar}");
        }

        private static void ProbarSelloDePartida(StringBuilder log, ColaDeCambios cola, ApiManager api)
        {
            // Dos hermanos, un mismo equipo. Si la cola sobrevive al cambio de perfil, esto
            // es lo único que impide escribirle el avance de uno en la partida del otro.
            Vaciar();
            UsarPartida(api, PartidaA);
            bool seMando = false;
            ColaDeCambios.EncolarAppend("objeto:DEL_HERMANO", Marcar(() => seMando = true));

            UsarPartida(api, PartidaB);
            Correr(cola.Vaciar("prueba", 5f, reintentarSiFalla: false));

            Comprobar(log, "Un cambio de otra partida se descarta en vez de subirse",
                !seMando && ColaDeCambios.Pendientes == 0,
                seMando ? "se subió a la partida equivocada" : "descartado");

            // Y descartar no es fallar: no hay nada que avisarle al jugador.
            Comprobar(log, "Descartar no se cuenta como cambio perdido",
                cola.UltimoResultado.Pendientes == 0,
                $"pendientes reportados = {cola.UltimoResultado.Pendientes}");

            UsarPartida(api, PartidaA);
        }

        private static void ProbarTopeDeTiempoSeRestaura(StringBuilder log, ColaDeCambios cola, ApiManager api)
        {
            // TopeDeTiempoParaPeticiones es estado global de ApiManager. Si un vaciado que
            // falla lo dejara puesto, TODAS las peticiones del resto de la sesión heredarían
            // un timeout de segundos y empezarían a fallar sin motivo aparente.
            Vaciar();
            ColaDeCambios.EncolarAppend("objeto:X", (ok, error) => error("cae"));
            ColaDeCambios.EncolarAppend("objeto:Y", Bien());

            Correr(cola.Vaciar("prueba", 2f, reintentarSiFalla: false));

            Comprobar(log, "El tope de tiempo vuelve a null aunque el vaciado falle",
                api.TopeDeTiempoParaPeticiones == null,
                $"quedó en {(api.TopeDeTiempoParaPeticiones?.ToString() ?? "null")}");
        }

        private static void ProbarElPlazoSoloRecortaLosEnviosDeLaCola(
            StringBuilder log, ColaDeCambios cola, ApiManager api)
        {
            // El recorte duraba todo el vaciado, así que cualquier lectura que otro sistema
            // lanzara mientras tanto —un diálogo, la mochila— heredaba el plazo y se cortaba.
            Vaciar();
            UsarPartida(api, PartidaA);

            int? duranteElEnvio = null;
            Action terminar = null;
            ColaDeCambios.EncolarAppend("objeto:LENTO", (ok, error) =>
            {
                duranteElEnvio = api.TopeDeTiempoParaPeticiones;
                terminar = ok;
            });

            var vaciado = new Rutina(cola.Vaciar("prueba", 30f, reintentarSiFalla: true));
            for (int i = 0; i < 10 && terminar == null; i++) vaciado.Paso();

            // Con el envío en el aire: aquí es donde otro sistema lanzaría su lectura.
            int? entreEnvios = api.TopeDeTiempoParaPeticiones;

            terminar?.Invoke();
            vaciado.HastaElFinal();

            Comprobar(log, "El plazo del guardado recorta su envío y no las demás peticiones",
                duranteElEnvio != null && entreEnvios == null,
                $"durante el envío={duranteElEnvio?.ToString() ?? "null"}, " +
                $"entre envíos={entreEnvios?.ToString() ?? "null"}");
        }

        private static void ProbarElUltimoGuardadoFijaElPlazo(
            StringBuilder log, ColaDeCambios cola, ApiManager api)
        {
            // El cambio de zona insiste mucho rato de fondo. Si el cierre llega en medio,
            // tiene que mandar su plazo corto: si no, el juego se quedaba esperando el
            // rato entero, callado, antes de preguntar.
            Vaciar();
            UsarPartida(api, PartidaA);
            ColaDeCambios.EncolarAppend("objeto:COLGADO", (ok, error) => { /* nunca contesta */ });

            var largo = new Rutina(cola.Vaciar("zona", 45f, reintentarSiFalla: true));
            largo.Paso();

            float desde = Time.realtimeSinceStartup;
            var corto = new Rutina(cola.Vaciar("cierre", 1f, reintentarSiFalla: true));

            // Los dos avanzan a la vez, como los movería Unity.
            while ((largo.Vivo || corto.Vivo) && Time.realtimeSinceStartup - desde < 10f)
            {
                largo.Paso();
                corto.Paso();
            }
            float duro = Time.realtimeSinceStartup - desde;

            Comprobar(log, "Un guardado corto que llega en medio acorta el largo que corre",
                !largo.Vivo && !corto.Vivo && duro < 5f, $"terminaron en {duro:F1} s");

            Vaciar();
        }

        /// <summary>
        /// Una corrutina que se avanza a mano, de a un "frame". <see cref="Correr"/> la
        /// lleva hasta el final de una vez; esto hace falta para mirar el estado en medio
        /// o para mover dos a la vez.
        /// </summary>
        private sealed class Rutina
        {
            private readonly Stack<IEnumerator> _pila = new Stack<IEnumerator>();

            public Rutina(IEnumerator rutina) => _pila.Push(rutina);

            public bool Vivo => _pila.Count > 0;

            public void Paso()
            {
                while (_pila.Count > 0)
                {
                    var actual = _pila.Peek();
                    if (!actual.MoveNext()) { _pila.Pop(); continue; }
                    if (actual.Current is IEnumerator hijo) { _pila.Push(hijo); continue; }
                    return;
                }
            }

            public void HastaElFinal(float tope = 30f)
            {
                float limite = Time.realtimeSinceStartup + tope;
                while (Vivo && Time.realtimeSinceStartup < limite) Paso();
            }
        }

        // ── Los interruptores ─────────────────────────────────────────────────

        private static void ProbarInterruptorDeMomentos(StringBuilder log, ColaDeCambios cola)
        {
            var go = new GameObject("SaveManagerDePrueba") { hideFlags = HideFlags.HideAndDontSave };
            var save = go.AddComponent<SaveManager>();
            save.verboseLogs = false;
            save.esperaMinima = 0f;
            FijarInstancia(save);

            try
            {
                Vaciar();
                int subidos = 0;
                ColaDeCambios.EncolarAppend("objeto:Z", Marcar(() => subidos++));

                // Apagado: el cambio de zona no debe tocar la cola siquiera.
                save.momentosActivos = SaveManager.Momentos.CierreDeAplicacion;
                Correr(save.GuardarYEsperar(SaveManager.Motivo.CambioDeZona, 5f));

                Comprobar(log, "Con CambioDeZona apagado, cambiar de zona no vacía nada",
                    subidos == 0 && ColaDeCambios.Pendientes == 1 && save.Guardados == 0,
                    $"{subidos} subidos, {ColaDeCambios.Pendientes} pendientes");

                // Encendido: el mismo motivo, ahora sí.
                save.momentosActivos = SaveManager.Momentos.CambioDeZona |
                                       SaveManager.Momentos.CierreDeAplicacion;
                Correr(save.GuardarYEsperar(SaveManager.Motivo.CambioDeZona, 5f));

                Comprobar(log, "Con CambioDeZona encendido, el mismo motivo sí vacía",
                    subidos == 1 && ColaDeCambios.Pendientes == 0 && save.Guardados == 1,
                    $"{subidos} subidos, {ColaDeCambios.Pendientes} pendientes");

                // Todo apagado: ni el cierre.
                Vaciar();
                int masSubidos = 0;
                ColaDeCambios.EncolarAppend("objeto:W", Marcar(() => masSubidos++));
                save.momentosActivos = SaveManager.Momentos.Ninguno;
                Correr(save.GuardarYEsperar(SaveManager.Motivo.CierreDeAplicacion, 5f));

                Comprobar(log, "Con momentosActivos en Ninguno no se guarda por ningún motivo",
                    masSubidos == 0 && ColaDeCambios.Pendientes == 1,
                    $"{masSubidos} subidos, {ColaDeCambios.Pendientes} pendientes");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                SoltarInstancia(typeof(SaveManager));
            }
        }

        private static void ProbarGuardarActualizaLaFechaDeLaPartida(StringBuilder log, ApiManager api)
        {
            // La lista de sesiones muestra fecha_update. Ningún guardado tocaba la fila de
            // la partida —todo va a personaje, misiones, mochila…—, así que la fecha que
            // se veía era la de creación. Cada guardado encola ahora un PATCH de la partida.
            const int Jugador = 987654;
            var go = new GameObject("SaveManagerDePrueba") { hideFlags = HideFlags.HideAndDontSave };
            var save = go.AddComponent<SaveManager>();
            save.verboseLogs = false;
            save.esperaMinima = 0f;
            save.momentosActivos = SaveManager.Momentos.CambioDeZona;
            FijarInstancia(save);

            try
            {
                Vaciar();
                PartidaDto creada = null;
                api.CrearPartida(Jugador, onSuccess: p => creada = p);
                string antes = creada?.fecha_update;

                // La fecha local lleva décimas de microsegundo, pero mejor no depender
                // de que el reloj avance entre dos líneas.
                System.Threading.Thread.Sleep(20);
                Correr(save.GuardarYEsperar(SaveManager.Motivo.CambioDeZona, 5f));

                List<PartidaDto> lista = null;
                api.ObtenerPartidasJugador(Jugador, onSuccess: l => lista = l);
                string despues = lista?.Find(p => creada != null && p.id == creada.id)?.fecha_update;

                Comprobar(log, "Guardar actualiza la fecha de la partida que muestra la lista",
                    antes != null && despues != null && string.CompareOrdinal(despues, antes) > 0,
                    $"{antes ?? "null"} → {despues ?? "null"}");
            }
            finally
            {
                PlayerPrefs.DeleteKey($"fishy.partidas.{Jugador}");
                UnityEngine.Object.DestroyImmediate(go);
                SoltarInstancia(typeof(SaveManager));
            }
        }

        // ── Andamiaje ─────────────────────────────────────────────────────────

        /// <summary>
        /// Ejecuta una corrutina a mano, con su anidamiento.
        ///
        /// En el editor no hay bucle de juego que las mueva, así que hay que girarlas
        /// aquí. El <c>yield return otraCorrutina</c> lo resuelve Unity, no el iterador,
        /// de modo que un <c>MoveNext()</c> pelado se saltaría entero el cuerpo de
        /// <c>Vaciar</c> — que es justo lo que se quiere probar. De ahí la pila.
        ///
        /// El tope es un seguro contra una corrutina que no termine: colgaría el editor.
        /// </summary>
        private static void Correr(IEnumerator rutina, float tope = 30f)
        {
            var pila = new Stack<IEnumerator>();
            pila.Push(rutina);

            float limite = Time.realtimeSinceStartup + tope;
            while (pila.Count > 0)
            {
                if (Time.realtimeSinceStartup > limite)
                {
                    _fallas.Add("Una corrutina no terminó a tiempo");
                    return;
                }

                var actual = pila.Peek();
                if (!actual.MoveNext()) { pila.Pop(); continue; }
                if (actual.Current is IEnumerator hijo) pila.Push(hijo);
            }
        }

        /// <summary>
        /// Publica el componente como el <c>Instance</c> de su clase.
        ///
        /// Hace falta porque en modo edición Unity no llama a <c>Awake</c> —solo lo haría
        /// con <c>[ExecuteAlways]</c>—, y es ahí donde estas tres clases se registran. Sin
        /// esto <c>ColaDeCambios.Vaciar</c> se corta en su primera línea por no encontrar
        /// al ApiManager, y toda prueba que vacíe pasa en verde sin haber vaciado nada:
        /// el peor resultado posible para un arnés.
        ///
        /// Se escribe la propiedad en vez de llamar al <c>Awake</c> entero porque ese
        /// <c>Awake</c> llama a <c>DontDestroyOnLoad</c>, que en modo edición lanza. Y no
        /// se le pone <c>[ExecuteAlways]</c> al código de producción: cambiar el juego
        /// para que la prueba ande es justo al revés de como tiene que ser.
        /// </summary>
        private static void FijarInstancia(MonoBehaviour componente)
        {
            var propiedad = componente.GetType()
                .GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            propiedad?.GetSetMethod(nonPublic: true)?.Invoke(null, new object[] { componente });
        }

        /// <summary>Deja el <c>Instance</c> de esa clase en null, como al arrancar.</summary>
        private static void SoltarInstancia(Type tipo)
        {
            var propiedad = tipo.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            propiedad?.GetSetMethod(nonPublic: true)?.Invoke(null, new object[] { null });
        }

        /// <summary>Un envío que siempre sale bien.</summary>
        private static Action<Action, Action<string>> Bien()
            => (ok, error) => ok();

        /// <summary>Un envío que sale bien y deja constancia de que pasó.</summary>
        private static Action<Action, Action<string>> Marcar(Action apunte)
            => (ok, error) => { apunte(); ok(); };

        private static ColaDeCambios.CambioPendiente Primero()
            => ColaDeCambios.AlmacenParaPruebas.Instantanea()[0];

        private static List<string> Claves()
        {
            var claves = new List<string>();
            foreach (var c in ColaDeCambios.AlmacenParaPruebas.Instantanea()) claves.Add(c.Clave);
            return claves;
        }

        /// <summary>Deja la cola limpia entre prueba y prueba: el almacén es estático.</summary>
        private static void Vaciar() => ColaDeCambios.AlmacenParaPruebas.Limpiar();

        /// <summary>
        /// Barre los objetos de una corrida anterior que se cortó a medias.
        ///
        /// Hace falta porque los tres singletons se registran en un estático y se
        /// defienden en <c>Awake</c>: si quedara uno vivo, el que crea la prueba se
        /// destruiría a sí mismo y trabajaríamos sobre un componente muerto. Solo toca
        /// objetos con nuestros nombres, ocultos y no persistidos — nunca nada de una
        /// escena abierta ni un prefab.
        /// </summary>
        private static void LimpiarSobras()
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || EditorUtility.IsPersistent(go)) continue;
                if (go.hideFlags != HideFlags.HideAndDontSave) continue;
                if (go.name != "ApiManagerDePrueba" && go.name != "ColaDePrueba" &&
                    go.name != "SaveManagerDePrueba") continue;

                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static ApiManager ApiLocal()
        {
            var go = new GameObject("ApiManagerDePrueba") { hideFlags = HideFlags.HideAndDontSave };
            var api = go.AddComponent<ApiManager>();

            var so = new SerializedObject(api);
            so.FindProperty("useLocalMode").boolValue = true;
            so.FindProperty("verboseLogs").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            FijarInstancia(api);
            return api;
        }

        private static ColaDeCambios ColaNueva()
        {
            var go = new GameObject("ColaDePrueba") { hideFlags = HideFlags.HideAndDontSave };
            var cola = go.AddComponent<ColaDeCambios>();
            cola.verboseLogs = false;
            FijarInstancia(cola);
            return cola;
        }

        /// <summary>
        /// Fija la partida activa. <c>PartidaId</c> no tiene setter público a propósito —
        /// el avance cuelga del perfil—, y <c>RetomarPartida</c> es la puerta que sí existe.
        /// El <c>usuario_jugador</c> va en 0 para que no toque el perfil seleccionado.
        /// </summary>
        private static void UsarPartida(ApiManager api, int id)
            => api.RetomarPartida(new PartidaDto { id = id, usuario_jugador = 0 });

        private static void Limpiar(ColaDeCambios cola, ApiManager api)
        {
            Vaciar();
            if (cola != null) UnityEngine.Object.DestroyImmediate(cola.gameObject);
            if (api != null)  UnityEngine.Object.DestroyImmediate(api.gameObject);

            // El OnDestroy de cada uno ya lo hace, pero solo si el objeto llegó vivo hasta
            // aquí. Dejar un Instance colgando apuntando a un componente destruido es lo
            // que hace que la SIGUIENTE corrida falle por algo que no tiene que ver.
            SoltarInstancia(typeof(ColaDeCambios));
            SoltarInstancia(typeof(ApiManager));
            SoltarInstancia(typeof(SaveManager));

            PlayerPrefs.DeleteKey($"fishy.inventario.{PartidaA}");
            PlayerPrefs.DeleteKey($"fishy.inventario.{PartidaB}");
            PlayerPrefs.DeleteKey($"fishy.personaje.{PartidaA}");
            PlayerPrefs.DeleteKey($"fishy.personaje.{PartidaB}");
            PlayerPrefs.Save();
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
