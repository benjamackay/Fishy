using System;
using UnityEngine;
using Fishy.Net;

/// <summary>
/// Carga el diálogo de un NPC neutro (HDU-1) desde la tabla DialogoNPC del
/// backend. Intenta el backend primero (ApiManager.ObtenerDialogoNpc) y si no
/// hay sesión, está en modo local, o falla la llamada, no hace nada: el
/// NPCDialogue ya asignado a mano en el Inspector de NPC.cs sigue siendo
/// válido — el NPC nunca se queda sin diálogo por falta de conexión.
/// </summary>
public static class DialogoNpcLoader
{
    public static void LoadAsync(string dialogoId, Action<NPCDialogue> onLoaded)
    {
        var api = ApiManager.Instance;
        if (string.IsNullOrEmpty(dialogoId) || api == null || api.IsLocalMode || !api.IsLoggedIn)
            return;

        api.ObtenerDialogoNpc(dialogoId,
            onSuccess: dto => onLoaded?.Invoke(FromDto(dto)),
            onError: e => Debug.LogWarning(
                $"[DialogoNpcLoader] No se pudo obtener '{dialogoId}' del backend ({e}); se mantiene el diálogo local."));
    }

    private static NPCDialogue FromDto(DialogoNpcDto dto)
    {
        var dialogo = ScriptableObject.CreateInstance<NPCDialogue>();
        dialogo.npcName = dto.npc_nombre;
        dialogo.dialogueLines = dto.lineas != null ? dto.lineas.ToArray() : new string[0];
        dialogo.autoProgressLine = new bool[dialogo.dialogueLines.Length];
        dialogo.typingSpeed = 0.05f;
        return dialogo;
    }
}
