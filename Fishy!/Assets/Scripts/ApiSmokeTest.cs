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
        Debug.Log($"[SmokeTest] === INICIO === usuario='{nombre}'");

        // 1) Registro -> 2) Partida -> 3) NPC -> 4) Chat -> 5) Mensajes -> 6) Historial -> 7) Finalizar
        ApiManager.Instance.Registro(nombre, password,
            onSuccess: () =>
            {
                Debug.Log($"[OK] 1. Registro. token={ApiManager.Instance.Token[..10]}... usuario_id={ApiManager.Instance.UsuarioId}");
                PasoCrearPartida();
            },
            onError: err => Debug.LogError($"[FALLO] 1. Registro: {err}\n(Si dice 'ya existe', cambia 'nombreBase' o reinicia Play.)"));
    }

    private void PasoCrearPartida()
    {
        ApiManager.Instance.CrearPartida(progreso: 0f,
            onSuccess: p =>
            {
                Debug.Log($"[OK] 2. Partida creada. partida_id={p.id}");
                PasoRegistrarNPC();
            },
            onError: err => Debug.LogError($"[FALLO] 2. CrearPartida: {err}"));
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
                    new("Aceptar y dar mi direccion", 0, "mala"),
                    new("Rechazar y decir que no me siento comodo", 1, "buena"),
                    new("Bloquear al contacto", 2, "buena"),
                };
                ApiManager.Instance.RegistrarMensaje("request",
                    "Podemos encontrarnos en el parque despues del colegio?", "", opciones,
                    onSuccess: m =>
                    {
                        Debug.Log($"[OK] 5b. Mensaje REQUEST con {m.posibles_respuestas.Count} opciones");
                        // chain (respuesta del jugador)
                        ApiManager.Instance.RegistrarRespuestaJugador("Rechazar y decir que no me siento comodo", "buena",
                            onSuccess: _ => { Debug.Log("[OK] 5c. Respuesta del jugador (CHAIN) registrada"); PasoHistorial(); },
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
                Debug.Log("[SmokeTest] === TODO OK: el ApiManager habla con el backend correctamente ===");
            },
            onError: err => Debug.LogError($"[FALLO] 7. FinalizarChat: {err}"));
    }
}
