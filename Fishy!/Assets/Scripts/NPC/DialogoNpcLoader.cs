using System;
using System.Collections.Generic;
using Fishy.Chat;
using Fishy.Net;
using UnityEngine;

/// <summary>
/// De dónde saca sus líneas un NPC neutro (HDU-1).
///
/// Hay tres fuentes y se usan en este orden, de la más fiable a la más frágil:
///
///  1. <b>El banco</b> (<c>Resources/banco_preguntas.json</c>, sección
///     <c>dialogos_npc_neutros</c>). Va empaquetado con el juego, así que responde en
///     el mismo frame, sin conexión y sin sesión. Es la misma fuente de la que se
///     alimenta la tabla DialogoNPC del backend —<c>manage.py cargar_banco</c> la
///     llena desde este archivo—, y la misma que ya usa el chat con
///     <see cref="BancoPreguntasLoader"/>. Por eso es la fuente por defecto: coincide
///     con la base y nunca falla.
///  2. <b>El backend</b> (<c>ApiManager.ObtenerDialogoNpc</c>). Llega tarde y puede no
///     llegar, pero es donde el equipo puede corregir un texto sin recompilar. Si
///     responde, pisa a lo del banco.
///  3. <b>El NPCDialogue del inspector</b>. Lo que había antes de todo esto. Sigue
///     valiendo como último recurso, y de él se conservan siempre el retrato, la
///     velocidad de tecleo y la voz, porque el banco no trae nada de eso: su campo
///     <c>npc_avatar</c> nombra un sprite que hoy no existe en el proyecto.
/// </summary>
public static class DialogoNpcLoader
{
    /// <summary>
    /// Busca el diálogo en el banco y lo devuelve listo para usar, o null si no está.
    /// Es síncrono a propósito: el banco ya está en memoria y el NPC tiene que poder
    /// hablar desde el primer frame.
    /// </summary>
    /// <param name="baseLocal">El NPCDialogue del inspector, del que se heredan
    /// retrato, velocidad y voz. Puede ser null.</param>
    public static NPCDialogue DesdeBanco(string dialogoId, NPCDialogue baseLocal)
    {
        if (string.IsNullOrEmpty(dialogoId)) return null;

        DialogoNeutroBanco entrada = BuscarEnBanco(dialogoId);
        if (entrada == null)
        {
            Debug.LogWarning($"[DialogoNpcLoader] '{dialogoId}' no está en " +
                             "dialogos_npc_neutros del banco; se usa el diálogo del inspector.");
            return null;
        }

        return Construir(entrada.npc_nombre, entrada.lineas, baseLocal);
    }

    /// <summary>Entrada del banco con ese id, o null. Pública porque el NPC también
    /// quiere la pista de misión y el id de la misión que desbloquea.</summary>
    public static DialogoNeutroBanco BuscarEnBanco(string dialogoId)
    {
        if (string.IsNullOrEmpty(dialogoId)) return null;

        var banco = BancoPreguntasLoader.Load();
        if (banco?.dialogos_npc_neutros == null) return null;

        foreach (var d in banco.dialogos_npc_neutros)
            if (d != null && d.id == dialogoId) return d;

        return null;
    }

    /// <summary>
    /// Pide al backend su versión del diálogo. Si no hay sesión, se está en modo
    /// local o la llamada falla, no llama a <paramref name="onLoaded"/>: lo que ya
    /// se aplicó del banco o del inspector sigue siendo válido.
    /// </summary>
    public static void LoadAsync(string dialogoId, NPCDialogue baseLocal, Action<NPCDialogue> onLoaded)
    {
        var api = ApiManager.Instance;
        if (string.IsNullOrEmpty(dialogoId) || api == null || api.IsLocalMode || !api.IsLoggedIn)
            return;

        api.ObtenerDialogoNpc(dialogoId,
            onSuccess: dto =>
            {
                if (dto == null || dto.lineas == null || dto.lineas.Count == 0) return;
                onLoaded?.Invoke(Construir(dto.npc_nombre, dto.lineas, baseLocal));
            },
            onError: e => Debug.LogWarning(
                $"[DialogoNpcLoader] No se pudo obtener '{dialogoId}' del backend ({e}); " +
                "se mantiene el diálogo que ya estaba."));
    }

    /// <summary>
    /// Arma el NPCDialogue de runtime. Hereda de <paramref name="baseLocal"/> todo lo
    /// que no son texto: si no, el NPC se quedaba sin cara —MostrarRetrato apaga el
    /// Image cuando no hay sprite— y tecleaba a la velocidad por defecto.
    /// </summary>
    private static NPCDialogue Construir(string nombre, IList<string> lineas, NPCDialogue baseLocal)
    {
        var dialogo = ScriptableObject.CreateInstance<NPCDialogue>();

        dialogo.npcName = !string.IsNullOrEmpty(nombre)
            ? nombre
            : (baseLocal != null ? baseLocal.npcName : "");

        dialogo.dialogueLines = lineas != null ? new List<string>(lineas).ToArray() : new string[0];

        // Ninguna línea avanza sola: el banco no dice nada de eso y el diálogo neutro
        // se pasa con el botón de interacción. El array tiene que existir igual porque
        // Typeline lo indexa.
        dialogo.autoProgressLine = new bool[dialogo.dialogueLines.Length];

        if (baseLocal != null)
        {
            dialogo.npcPortrait      = baseLocal.npcPortrait;
            dialogo.typingSpeed      = baseLocal.typingSpeed;
            dialogo.voiceSound       = baseLocal.voiceSound;
            dialogo.voicePitch       = baseLocal.voicePitch;
            dialogo.autoProgressDelay = baseLocal.autoProgressDelay;
        }
        else
        {
            dialogo.typingSpeed = 0.05f;
        }

        return dialogo;
    }
}
