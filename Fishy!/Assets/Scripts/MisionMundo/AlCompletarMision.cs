using Fishy.Mision;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Espera a que se complete una misión concreta. Ver <see cref="DisparadorDeMision"/>
/// para el porqué de los dos eventos y de los dos caminos.
///
/// Ejemplo típico: abrir una zona. <c>alCompletar</c> → la cinemática de apertura;
/// <c>alRestaurar</c> → <c>BlockedZone.Unlock()</c> a secas, sin cinemática.
/// </summary>
[DisallowMultipleComponent]
public class AlCompletarMision : DisparadorDeMision
{
    [Header("Qué pasa")]
    [Tooltip("La misión se acaba de completar, en esta partida y con el niño/a " +
             "delante. Aquí van la cinemática, el cartel y el sonido.")]
    public UnityEvent alCompletar = new UnityEvent();

    [Tooltip("La misión YA estaba completada al cargar la partida. Aquí va solo " +
             "dejar el mundo como quedó: abrir la zona sin cinemática, encender el " +
             "NPC. Si algo tiene que pasar en los dos casos, engánchalo en los dos.")]
    public UnityEvent alRestaurar = new UnityEvent();

    protected override UnityEvent EnVivo => alCompletar;
    protected override UnityEvent AlRestaurar => alRestaurar;
    protected override string Momento => "completada";

    protected override bool YaSeCumple(MissionManager manager, string misionId) =>
        manager.EstaCompletado(misionId);

    protected override void Suscribir(MissionManager manager) =>
        manager.onDesafioCompletado.AddListener(DispararEnVivo);

    protected override void Desuscribir(MissionManager manager) =>
        manager.onDesafioCompletado.RemoveListener(DispararEnVivo);
}
