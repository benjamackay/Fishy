using System.Collections.Generic;
using UnityEngine;
using Fishy.Net;

/// <summary>
/// Prueba de humo del ApiManager contra el backend real.
/// Uso: pon este componente en un GameObject de la escena y dale Play.
/// Mira la consola: cada paso imprime [OK] o [FALLO].
///
/// No necesitas montar el ApiManager aparte: si no existe, este script lo crea.
/// </summary>
public class ApiSmokeTest : MonoBehaviour
{
    [Tooltip("Se le agrega un numero al azar para no chocar con usuarios existentes.")]
    public string nombreBase = "unity_test";
    public string password = "1234";

    [Header("Perfil de menor")]
    [Tooltip("Nombre del perfil de menor que crea la prueba (control parental).")]
    public string nombreJugador = "Peque de prueba";
    public int edadJugador = 9;

    [Tooltip("Si está activo y ya hay una sesión iniciada, NO corre la prueba " +
             "(evita sobrescribir el token/partida del jugador real).")]
    public bool skipIfLoggedIn = true;

    private void Start()
    {
        // Evita pisar la sesión del jugador real si este componente quedó por
        // error en la escena de juego: si ya hay login activo, no hacemos nada.
        if (skipIfLoggedIn && ApiManager.Instance != null && ApiManager.Instance.IsLoggedIn)
        {
            Debug.LogWarning("[SmokeTest] Hay una sesión activa: se omite la prueba " +
                             "para no sobrescribir el token/partida del jugador. " +
                             "Quita este componente de la escena de juego cuando termines de probar.");
            return;
        }

        // Asegura que exista el ApiManager (singleton).
        if (ApiManager.Instance == null)
        {
            var go = new GameObject("ApiManager");
            go.AddComponent<ApiManager>();
            Debug.Log("[SmokeTest] ApiManager creado automaticamente.");
        }

        string nombre = $"{nombreBase}_{Random.Range(1000, 999999)}";
        string email  = $"{nombre}@fishy.test";
        Debug.Log($"[SmokeTest] === INICIO === adulto='{nombre}' email='{email}'");

        // 1) Registro (adulto) -> 1b) Perfil de menor -> 2) Partida -> 3) NPC
        // -> 4) Chat -> 5) Mensajes -> 6) Historial -> 7) Finalizar
        ApiManager.Instance.Registro(nombre, email, password,
            onSuccess: () =>
            {
                Debug.Log($"[OK] 1. Registro. token={ApiManager.Instance.Token[..10]}... adulto_id={ApiManager.Instance.AdultoId}");
                PasoCrearJugador();
            },
            onError: err => Debug.LogError($"[FALLO] 1. Registro: {err}\n(Si dice 'ya existe', cambia 'nombreBase' o reinicia Play.)"));
    }

    private void PasoCrearJugador()
    {
        // Sin perfil de menor no se puede crear partida: el backend la exige.
        ApiManager.Instance.CrearJugador(nombreJugador, edadJugador,
            onSuccess: j =>
            {
                Debug.Log($"[OK] 1b. Perfil de menor creado. jugador_id={j.id} nombre={j.nombre}");
                ApiManager.Instance.SeleccionarJugador(j.id);
                PasoCrearPartida();
            },
            onError: err => Debug.LogError($"[FALLO] 1b. CrearJugador: {err}"));
    }

    private void PasoCrearPartida()
    {
        // ContinuarOCrearPartida es el flujo real del juego: retoma la última
        // partida del menor y solo crea una si nunca ha jugado.
        ApiManager.Instance.ContinuarOCrearPartida(
            onSuccess: (p, esNueva) =>
            {
                Debug.Log($"[OK] 2. Partida {(esNueva ? "creada" : "retomada")}. " +
                          $"partida_id={p.id} perfil={p.usuario_jugador} progreso={p.progreso}");
                PasoRegistrarNPC();
            },
            onError: err => Debug.LogError($"[FALLO] 2. ContinuarOCrearPartida: {err}"));
    }

    private void PasoRegistrarNPC()
    {
        ApiManager.Instance.RegistrarNPC("Desconocido del bosque", "Bosque de los Desconocidos", "enemigo", 0,
            onSuccess: npc =>
            {
                Debug.Log($"[OK] 3. NPC registrado. npc_id={npc.id} tipo={npc.tipo}");
                ApiManager.Instance.ActualizarConfianzaNPC(40,
                    onSuccess: n => { Debug.Log($"[OK] 3b. Confianza NPC -> {n.confianza}"); PasoIniciarChat(); },
                    onError: err => Debug.LogError($"[FALLO] 3b. ActualizarConfianza: {err}"));
            },
            onError: err => Debug.LogError($"[FALLO] 3. RegistrarNPC: {err}"));
    }

    private void PasoIniciarChat()
    {
        ApiManager.Instance.IniciarChat("grooming",
            onSuccess: chat =>
            {
                Debug.Log($"[OK] 4. Chat iniciado. chat_id={chat.id} categoria={chat.categoria_riesgo}");
                PasoMensajes();
            },
            onError: err => Debug.LogError($"[FALLO] 4. IniciarChat: {err}"));
    }

    // Pregunta y opción reales del banco v1.8 (zona "desconocidos"), verificadas
    // contra la base: R1 es segura_optima, así que el riesgo de esa zona debe
    // quedar en +2. Si cambian estos ids, el paso 8 falla avisando que la opción
    // no existe en el banco — que es justo lo que pasaba con los ids viejos
    // "HDU2_NPC01_F2_Q01_R3": ese formato con fase (_F2_) ya no existe, la
    // respuesta se guardaba igual pero caía en sin_clasificar y no sumaba riesgo.
    private const string PreguntaBanco = "HDU2_NPC01_Q01";
    private const string OpcionElegida = "HDU2_NPC01_Q01_R1";
    private const int    ImpactoEsperado = 2;

    private void PasoMensajes()
    {
        // start (NPC neutro)
        ApiManager.Instance.RegistrarMensaje("start", "Hola! Me gusta tu mochila :)",
            onSuccess: _ =>
            {
                Debug.Log("[OK] 5a. Mensaje START");
                // request (NPC con riesgo + opciones)
                var opciones = new List<OpcionRespuesta>
                {
                    new("Le doy mi nombre completo y mi colegio", 0, "mala"),
                    new("Le digo que me conocen por mi apodo", 1, "buena"),
                    new("Le digo que no doy mis datos a desconocidos", 2, "buena"),
                };
                ApiManager.Instance.RegistrarMensaje("request",
                    "Oye, en mi grupo privado todos nos conocemos de verdad. Como te llamas?",
                    "", opciones,
                    onSuccess: m =>
                    {
                        Debug.Log($"[OK] 5b. Mensaje REQUEST con {m.posibles_respuestas.Count} opciones");
                        // chain (respuesta del jugador). El opcion_banco_id es lo que
                        // hace que esta respuesta cuente para el riesgo por zona.
                        ApiManager.Instance.RegistrarRespuestaJugador(
                            "Le digo que no doy mis datos a desconocidos", "buena",
                            preguntaBancoId: PreguntaBanco,
                            opcionBancoId: OpcionElegida,
                            onSuccess: msg =>
                            {
                                if (msg.opcion_banco_id == OpcionElegida)
                                    Debug.Log($"[OK] 5c. Respuesta (CHAIN) registrada con opcion_banco_id={msg.opcion_banco_id}");
                                else
                                    Debug.LogError($"[FALLO] 5c. el backend devolvio opcion_banco_id='{msg.opcion_banco_id}', " +
                                                   $"se esperaba '{OpcionElegida}'. Sin ese campo el riesgo por zona no suma.");
                                PasoHistorial();
                            },
                            onError: err => Debug.LogError($"[FALLO] 5c. RespuestaJugador: {err}"));
                    },
                    onError: err => Debug.LogError($"[FALLO] 5b. REQUEST: {err}"));
            },
            onError: err => Debug.LogError($"[FALLO] 5a. START: {err}"));
    }

    private void PasoHistorial()
    {
        ApiManager.Instance.ObtenerHistorial(
            onSuccess: hist =>
            {
                Debug.Log($"[OK] 6. Historial recuperado: {hist.Count} mensajes");
                foreach (var m in hist)
                    Debug.Log($"        [{m.tipo}] '{m.respuesta}' calidad={m.calidad_respuesta} opciones={(m.posibles_respuestas?.Count ?? 0)}");
                PasoFinalizar();
            },
            onError: err => Debug.LogError($"[FALLO] 6. ObtenerHistorial: {err}"));
    }

    private void PasoFinalizar()
    {
        ApiManager.Instance.FinalizarChat("Otto se siente orgulloso!",
            onSuccess: end =>
            {
                Debug.Log($"[OK] 7. Chat finalizado (mensaje END id={end.id})");
                PasoRiesgoPorZona();
            },
            onError: err => Debug.LogError($"[FALLO] 7. FinalizarChat: {err}"));
    }

    private void PasoRiesgoPorZona()
    {
        ApiManager.Instance.ObtenerRiesgoPorZona(
            onSuccess: riesgo =>
            {
                foreach (var z in riesgo.zonas)
                    Debug.Log($"        zona '{z.zona}': {z.riesgo_acumulado:+#;-#;0} " +
                              $"en {z.respuestas} respuesta(s), escala {z.minimo_posible} a {z.maximo_posible} " +
                              $"({z.Normalizado:P0})");

                // La única respuesta registrada fue segura_optima, así que el total
                // tiene que ser exactamente +2 y nada puede quedar sin clasificar.
                bool bien = riesgo.total == ImpactoEsperado
                            && riesgo.respuestas == 1
                            && riesgo.sin_clasificar == 0;

                if (bien)
                {
                    Debug.Log($"[OK] 8. Riesgo por zona: total={riesgo.total} (mas alto = mas seguro)");
                    Debug.Log("[SmokeTest] === TODO OK: el ApiManager habla con el backend correctamente ===");
                }
                else
                {
                    Debug.LogError($"[FALLO] 8. Riesgo por zona: total={riesgo.total} (esperado {ImpactoEsperado}), " +
                                   $"respuestas={riesgo.respuestas} (esperado 1), " +
                                   $"sin_clasificar={riesgo.sin_clasificar} (esperado 0).\n" +
                                   "Si sin_clasificar es 1, el banco de la base no tiene la opcion " +
                                   $"'{OpcionElegida}': recarga el banco con 'manage.py cargar_banco'.");
                }
            },
            onError: err => Debug.LogError($"[FALLO] 8. ObtenerRiesgoPorZona: {err}"));
    }
}
