using UnityEngine;

/// <summary>
/// Un permiso para que <see cref="PresenciaSegunMision"/> retire al NPC.
///
/// Las condiciones solo frenan la DESAPARICIÓN, nunca la aparición: que aparezca
/// tarde es una pifia visible —el niño/a va a hablar con alguien que no está—,
/// mientras que retirarlo tarde no se nota.
///
/// Se añade al mismo GameObject del NPC y se recoge sola; no hay que arrastrarla a
/// ninguna lista. Puede haber varias y tienen que decir que sí <b>todas</b>: cada
/// una es un motivo independiente para esperar un poco más.
///
/// Para inventar una nueva, hereda de aquí y responde a
/// <see cref="SePuedeDesaparecer"/> — por ejemplo "no mientras el chat esté abierto"
/// o "no hasta que termine su animación".
/// </summary>
public abstract class CondicionDeDesaparicion : MonoBehaviour
{
    /// <summary>
    /// ¿Se puede retirar al NPC ahora mismo? En false se vuelve a preguntar cada
    /// frame hasta que diga que sí, así que tiene que ser barata.
    /// </summary>
    public abstract bool SePuedeDesaparecer();

    /// <summary>Cómo se llama lo que se está esperando, para el log.</summary>
    public virtual string Motivo => GetType().Name;
}
