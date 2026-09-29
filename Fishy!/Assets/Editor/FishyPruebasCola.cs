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
            ProbarFalloSeReintentaConRetardo(log, cola);
            ProbarCierreNoReintenta(log, cola);
            ProbarSinRespuestaNoEsTodoBien(log, cola);
            ProbarSelloDePartida(log, cola, api);
            ProbarTopeDeTiempoSeRestaura(log, cola, api);

            // ── El diario: que nada se pierda al morir el proceso ──
            ProbarDiarioSobreviveAlReinicio(log);
            ProbarSnapshotsNoSeAnotan(log);
            ProbarDiarioConservaSuPartida(log, api);
            ProbarDiarioNoDegradaUnaZona(log);
            ProbarIdaYVueltaDeLasRecetas(log);

            // ── Los interruptores de SaveManager ──
            ProbarInterruptorDeMomentos(log, cola);

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

        /// <summary>El valor fusionado de una entrada de zona dice "completada".
        /// Antes el valor era un bool a secas; ahora es el objeto de argumentos de la
        /// receta, que es lo que permite anotarlo en el diario.</summary>
        private static bool ZonaCompletada(ColaDeCambios.CambioPendiente cambio)
            => cambio != null && cambio.Valor is RecetasDeCola.ArgsZona z && z.Completada;

        private static void ProbarZonaFusionaPorOr(StringBuilder log)
        {
            // El caso real: BosqueDesconocidosManager marca la zona completada y después
            // MisionBackendSync la "desbloquea" al recalcular. Si ganara el último, el
            // reporte del adulto mostraría como pendiente una zona ya terminada.
            Vaciar();
            ColaDeCambios.EncolarZona("desconocidos", completada: true);
            ColaDeCambios.EncolarZona("desconocidos", completada: false);

            Comprobar(log, "Completar una zona no se puede deshacer con un desbloqueo",
                ColaDeCambios.Pendientes == 1 && ZonaCompletada(Primero()),
                $"valor = {Primero().Valor}");

            // Y al revés también: primero desbloquear, después completar.
            Vaciar();
            ColaDeCambios.EncolarZona("retos", completada: false);
            ColaDeCambios.EncolarZona("retos", completada: true);
            Comprobar(log, "Y completarla después del desbloqueo sí cuenta",
                ZonaCompletada(Primero()), $"valor = {Primero().Valor}");
        }

        private static void ProbarProgresoFusionaPorMaximo(StringBuilder log)
        {
            Vaciar();
            ColaDeCambios.EncolarProgreso(25f);
            ColaDeCambios.EncolarProgreso(10f);

            Comprobar(log, "El progreso de la partida nunca retrocede",
                ColaDeCambios.Pendientes == 1 &&
                Primero().Valor is RecetasDeCola.ArgsProgreso v &&
                Mathf.Approximately(v.Progreso, 25f),
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
                ColaDeCambios.Pendientes == 1 && ZonaCompletada(Primero()),
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

        private static void ProbarFalloSeReintentaConRetardo(StringBuilder log, ColaDeCambios cola)
        {
            // Esto es lo que antes hacían por su cuenta ObjetosRecogidosSync y
            // NpcTematicaSync con sus propias listas de pendientes.
            Vaciar();
            cola.maxIntentos = 3;
            int intentos = 0;
            ColaDeCambios.EncolarAppend("objeto:TERCO", (ok, error) => { intentos++; error("cae"); });

            Correr(cola.Vaciar("zona", 5f, reintentarSiFalla: true));
            Comprobar(log, "Un fallo al cambiar de zona vuelve a la cola",
                ColaDeCambios.Pendientes == 1 && cola.UltimoResultado.Fallidos == 1,
                $"{intentos} intento(s), {ColaDeCambios.Pendientes} pendientes");

            // El siguiente vaciado NO lo toca: tras fallar espera antes de reintentar.
            // Contra un servidor caído, insistir cada pocos segundos solo llena el log.
            Correr(cola.Vaciar("zona", 5f, reintentarSiFalla: true));
            Comprobar(log, "Tras fallar, espera su retardo antes de volver a intentarlo",
                intentos == 1 && cola.UltimoResultado.Esperando == 1,
                $"{intentos} intento(s), {cola.UltimoResultado.Esperando} esperando");

            // Se simula que pasó el rato y se agotan los intentos.
            for (int i = 0; i < 5; i++)
            {
                foreach (var c in ColaDeCambios.AlmacenParaPruebas.Instantanea()) c.NoAntesDe = 0f;
                Correr(cola.Vaciar("zona", 5f, reintentarSiFalla: true));
            }

            // El contrato cambió a propósito: antes esto se ABANDONABA, y con ello se
            // perdía de verdad. Ahora deja de reintentarse solo, pero sigue en la cola.
            Comprobar(log, "Tras maxIntentos deja de insistir solo pero NO se pierde",
                intentos == cola.maxIntentos && ColaDeCambios.Pendientes == 1 &&
                ColaDeCambios.HayAtasco,
                $"{intentos} intentos, {ColaDeCambios.Pendientes} pendientes, " +
                $"atasco = {ColaDeCambios.HayAtasco}");

            // Y el jugador puede desatascarlo.
            ColaDeCambios.ReintentarAhora();
            Comprobar(log, "«Intentar ahora» desatasca lo que había dejado de reintentarse",
                !ColaDeCambios.HayAtasco && ColaDeCambios.Pendientes == 1,
                $"atasco = {ColaDeCambios.HayAtasco}, {ColaDeCambios.Pendientes} pendientes");
        }

        // ── El diario ─────────────────────────────────────────────────────────

        private static void ProbarDiarioSobreviveAlReinicio(StringBuilder log)
        {
            // El caso real, y el que costó 19 cambios en una tarde: se para el Play (o se
            // cae el juego) con la cola a medias. Antes eso era una pérdida definitiva de
            // los objetivos, porque el chat que los marca no se vuelve a disparar.
            Vaciar();
            ColaDeCambios.EncolarMision("MISION_NPC_03", completada: true);
            ColaDeCambios.EncolarObjetivo("MISION_NPC_03", 2);

            // "Se murió el proceso": la cola en memoria desaparece, el diario no.
            ColaDeCambios.AlmacenParaPruebas.Limpiar();
            Comprobar(log, "Al morir el proceso la cola en memoria queda vacía",
                ColaDeCambios.Pendientes == 0, $"{ColaDeCambios.Pendientes} pendientes");

            ColaDeCambios.Reproducir();

            bool volvioLaMision = false, volvioElObjetivo = false;
            foreach (var c in ColaDeCambios.AlmacenParaPruebas.Instantanea())
            {
                if (c.Valor is RecetasDeCola.ArgsMision m && m.MisionId == "MISION_NPC_03" && m.Completada)
                    volvioLaMision = true;
                if (c.Valor is RecetasDeCola.ArgsObjetivo o && o.MisionId == "MISION_NPC_03" && o.Orden == 2)
                    volvioElObjetivo = true;
            }

            Comprobar(log, "Al volver a entrar, lo anotado vuelve a la cola con sus datos",
                ColaDeCambios.Pendientes == 2 && volvioLaMision && volvioElObjetivo,
                $"{ColaDeCambios.Pendientes} pendientes, misión = {volvioLaMision}, " +
                $"objetivo = {volvioElObjetivo}");
        }

        private static void ProbarSnapshotsNoSeAnotan(StringBuilder log)
        {
            // La mochila y la posición leen el estado vivo al subir, así que el siguiente
            // guardado los manda completos igual. Anotarlos sería guardar una foto vieja
            // de algo que se puede volver a mirar.
            Vaciar();
            ColaDeCambios.EncolarSnapshot("inventario", Bien());
            ColaDeCambios.EncolarSnapshot("personaje", Bien());
            ColaDeCambios.EncolarObjeto("ITEM_FLOR_01");

            ColaDeCambios.AlmacenParaPruebas.Limpiar();
            ColaDeCambios.Reproducir();

            var claves = Claves();
            Comprobar(log, "La mochila y la posición no se anotan; el objeto recogido sí",
                claves.Count == 1 && claves[0] == "objeto:ITEM_FLOR_01",
                claves.Count == 0 ? "(ninguna)" : string.Join(", ", claves));
        }

        private static void ProbarDiarioConservaSuPartida(StringBuilder log, ApiManager api)
        {
            // Un cambio anotado en la partida 3 se sube A LA 3, aunque ahora se juegue la
            // 4. Antes esto se descartaba, y era lo único que evitaba escribirle el
            // avance de un hermano al otro; ahora no hay que tirar nada, porque todos los
            // endpoints de escritura reciben la partida.
            Vaciar();
            UsarPartida(api, PartidaA);
            ColaDeCambios.EncolarObjetivo("MISION_NPC_03", 3);

            ColaDeCambios.AlmacenParaPruebas.Limpiar();
            UsarPartida(api, PartidaB);
            ColaDeCambios.Reproducir();

            var repuesto = Primero();
            Comprobar(log, "Lo anotado vuelve con SU partida, no con la que se juega ahora",
                repuesto != null && repuesto.Partida == PartidaA,
                $"partida del cambio = {repuesto?.Partida}, partida activa = {api.PartidaId}");

            UsarPartida(api, PartidaA);
        }

        private static void ProbarDiarioNoDegradaUnaZona(StringBuilder log)
        {
            // El caso: la zona quedó COMPLETADA la sesión pasada y no alcanzó a subirse.
            // Esta sesión, al recalcular, MisionBackendSync la manda como "desbloqueada".
            // Si al reponer el diario ganara sin más el valor de ahora, el reporte del
            // adulto pasaría a mostrar como pendiente una zona ya terminada.
            //
            // Se prueba en el orden que de verdad rompe: primero lo de esta sesión y
            // después la reposición. En el juego el orden normal es el contrario, así que
            // este es el camino que se escapa sin querer.
            Vaciar();
            ColaDeCambios.EncolarZona("ciberacoso", completada: true);

            var anotado = DiarioDeCambios.Leer();
            ColaDeCambios.AlmacenParaPruebas.Limpiar();

            ColaDeCambios.EncolarZona("ciberacoso", completada: false);
            ColaDeCambios.Reproducir();

            Comprobar(log, "Reponer del diario no degrada una zona ya completada",
                ColaDeCambios.Pendientes == 1 && ZonaCompletada(Primero()),
                $"anotadas {anotado.Count}, valor = {Primero()?.Valor}");
        }

        private static void ProbarIdaYVueltaDeLasRecetas(StringBuilder log)
        {
            // La conversión de closures a (receta, argumentos) tocó nueve sitios, y un
            // cuerpo mal armado ahí es una escritura silenciosamente equivocada. Esto
            // comprueba que cada receta sobrevive al viaje por disco con sus datos.
            Vaciar();
            ColaDeCambios.EncolarMision("MISION_X", completada: true);
            ColaDeCambios.EncolarObjetivo("MISION_X", 7);
            ColaDeCambios.EncolarZona("ciberacoso", completada: true);
            ColaDeCambios.EncolarObjeto("ITEM_ROCA");
            ColaDeCambios.EncolarNpc("NPC_ALEX", exito: true);
            ColaDeCambios.EncolarDetective("DC_CASO_01",
                new List<string> { "m1", "m2" }, 4, 5, 80f);
            ColaDeCambios.EncolarProgreso(42f);
            ColaDeCambios.EncolarChat("chat:0", "Puma", "zona_2", "desconocidos",
                new List<RecetasDeCola.MensajeDeChat>
                {
                    new RecetasDeCola.MensajeDeChat { Tipo = "chain", Texto = "hola", Calidad = "segura" },
                },
                "cerrado");

            ColaDeCambios.AlmacenParaPruebas.Limpiar();
            ColaDeCambios.Reproducir();

            var porClave = new Dictionary<string, object>();
            foreach (var c in ColaDeCambios.AlmacenParaPruebas.Instantanea())
                porClave[c.Clave] = c.Valor;

            Comprobar(log, "Las ocho recetas vuelven del diario",
                porClave.Count == 8, $"{porClave.Count} de 8");

            Comprobar(log, "Una misión vuelve con su id y su estado",
                porClave.TryGetValue("mision:MISION_X", out var vm) &&
                vm is RecetasDeCola.ArgsMision m && m.MisionId == "MISION_X" && m.Completada,
                Describir(porClave, "mision:MISION_X"));

            Comprobar(log, "Un objetivo vuelve con su orden",
                porClave.TryGetValue("objetivo:MISION_X:7", out var vo) &&
                vo is RecetasDeCola.ArgsObjetivo o && o.Orden == 7,
                Describir(porClave, "objetivo:MISION_X:7"));

            Comprobar(log, "Un caso de detective vuelve con sus marcados y su puntaje",
                porClave.TryGetValue("detective:DC_CASO_01", out var vd) &&
                vd is RecetasDeCola.ArgsDetective d && d.Marcados != null &&
                d.Marcados.Count == 2 && d.Aciertos == 4 && Mathf.Approximately(d.Porcentaje, 80f),
                Describir(porClave, "detective:DC_CASO_01"));

            Comprobar(log, "Una conversación vuelve con sus mensajes",
                porClave.TryGetValue("chat:0", out var vc) &&
                vc is RecetasDeCola.ArgsChat ch && ch.Contacto == "Puma" &&
                ch.Mensajes != null && ch.Mensajes.Count == 1 && ch.Mensajes[0].Texto == "hola",
                Describir(porClave, "chat:0"));
        }

        private static string Describir(Dictionary<string, object> porClave, string clave)
            => porClave.TryGetValue(clave, out var v) && v != null ? v.GetType().Name : "(no volvió)";

        private static void ProbarCierreNoReintenta(StringBuilder log, ColaDeCambios cola)
        {
            Vaciar();
            int intentos = 0;
            ColaDeCambios.EncolarAppend("objeto:TERCO", (ok, error) => { intentos++; error("cae"); });

            Correr(cola.Vaciar("CierreDeAplicacion", 5f, reintentarSiFalla: false));

            Comprobar(log, "Al cerrar no se reintenta: no hay otra oportunidad que esperar",
                intentos == 1 && ColaDeCambios.Pendientes == 0 && !cola.UltimoResultado.TodoBien,
                $"{intentos} intento(s), fallidos = {cola.UltimoResultado.Fallidos}");
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

        /// <summary>
        /// Deja la cola limpia entre prueba y prueba: el almacén es estático.
        ///
        /// <b>Y el diario también</b>, que es lo importante: ahora encolar escribe en
        /// PlayerPrefs, así que sin esto una corrida de pruebas le dejaría al juego de
        /// verdad un puñado de cambios inventados para subir al backend.
        /// </summary>
        private static void Vaciar()
        {
            ColaDeCambios.AlmacenParaPruebas.Limpiar();
            DiarioDeCambios.Limpiar();
        }

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
