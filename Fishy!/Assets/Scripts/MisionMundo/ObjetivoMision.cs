using System;
using UnityEngine;

/// <summary>
/// Un objetivo concreto de una misión: juntar cierto objeto, o hablar con cierto NPC.
///
/// Vive en el ensamblado por defecto (Assembly-CSharp) y NO dentro de la carpeta
/// Scripts/Mision, porque esa tiene el asmdef "Fishy.Mision" y desde ahí no se
/// pueden ver ni <see cref="ItemData"/> ni <see cref="NPC"/>. Las fichas de datos
/// de misión (DesafioData) siguen viviendo allá; esto es sólo el pegamento con
/// el mundo.
/// </summary>
public enum TipoObjetivo
{
    RecogerObjeto,
    HablarConNpc,
}

[Serializable]
public class ObjetivoMision
{
    public TipoObjetivo tipo = TipoObjetivo.RecogerObjeto;

    [Header("Si el tipo es Recoger Objeto")]
    [Tooltip("Qué hay que juntar. Es el mismo ItemData que lleva el WorldItem del mapa.")]
    public ItemData objeto;

    [Min(1)]
    [Tooltip("Cuántas unidades hacen falta.")]
    public int cantidad = 1;

    [Header("Si el tipo es Hablar Con Npc")]
    [Tooltip("Con quién hay que conversar para cumplirlo.")]
    public NPC npc;

    /// <summary>
    /// Cumplido en esta sesión. No se serializa: el estado de la misión completa
    /// lo guarda MissionManager en PlayerPrefs, los objetivos sueltos no.
    /// </summary>
    [NonSerialized] public bool cumplido;

    /// <summary>Texto para el panel de misiones. Ej: "Juntar Concha (1/3)".</summary>
    public string Describir()
    {
        switch (tipo)
        {
            case TipoObjetivo.RecogerObjeto:
                string nombreObjeto = objeto != null && !string.IsNullOrEmpty(objeto.itemName)
                    ? objeto.itemName
                    : "(objeto sin asignar)";
                int tiene = InventoryManager.Instance.GetQuantity(objeto);
                return $"Juntar {nombreObjeto} ({Mathf.Min(tiene, cantidad)}/{cantidad})";

            case TipoObjetivo.HablarConNpc:
                string nombreNpc = npc != null ? npc.name : "(NPC sin asignar)";
                return $"Hablar con {nombreNpc}";

            default:
                return "(objetivo desconocido)";
        }
    }

    /// <summary>Comprueba contra el mundo si este objetivo ya está cumplido.</summary>
    public bool Evaluar()
    {
        if (cumplido) return true;

        // "Hablar con" no se puede consultar: es un hecho puntual, lo marca
        // MissionTracker cuando el NPC cierra su diálogo.
        if (tipo == TipoObjetivo.RecogerObjeto && objeto != null)
            cumplido = InventoryManager.Instance.GetQuantity(objeto) >= cantidad;

        return cumplido;
    }
}
