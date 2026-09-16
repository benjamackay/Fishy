using System.Collections;
using Fishy.Phone;
using Fishy.World;
using UnityEngine;

/// <summary>
/// Abre un chat (<see cref="PhoneChatLauncher"/>) apenas termina la cinemática de
/// desbloqueo de zona.
///
/// Pensado para el mensaje de cierre de una temática —"Antes de que sigas al
/// arrecife…"—: se engancha al 'Al Completar' de la misión que abre la zona siguiente,
/// justo después del BlockedZone.Unlock(). Si el chat se abriera a la vez, la cámara
/// de la cinemática y el zoom del celular se pelearían.
///
/// En 'Al Restaurar' no va: al retomar una partida ya terminada no hay que repetirlo.
/// </summary>
public class AbrirChatTrasCinematica : MonoBehaviour
{
    [Tooltip("Chat a abrir. Si se deja vacío se busca en este mismo GameObject.")]
    public PhoneChatLauncher chat;

    [Tooltip("Segundos de espera después de que termina la cinemática.")]
    public float esperaExtra = 0.5f;

    /// <summary>Sin parámetros para poder engancharlo desde cualquier UnityEvent.</summary>
    public void Abrir()
    {
        if (chat == null) chat = GetComponent<PhoneChatLauncher>();
        if (chat == null)
        {
            Debug.LogWarning($"[{name}] AbrirChatTrasCinematica sin PhoneChatLauncher.", this);
            return;
        }
        StartCoroutine(AbrirCuandoTermine());
    }

    private IEnumerator AbrirCuandoTermine()
    {
        // Un frame para que la cinemática pedida en la misma llamada alcance a arrancar.
        yield return null;

        ZoneUnlockCinematic cinematica = ZoneUnlockCinematic.Instance;
        while (cinematica != null && cinematica.IsPlaying) yield return null;

        if (esperaExtra > 0f) yield return new WaitForSecondsRealtime(esperaExtra);
        chat.OpenManual();
    }
}
