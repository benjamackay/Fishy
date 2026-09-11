using Fishy.Mision;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Espera a que una misión concreta quede DISPONIBLE, es decir, cuando el niño/a la
/// acaba de recibir y todavía no la ha terminado. Ver <see cref="DisparadorDeMision"/>
/// para el porqué de los dos eventos y de los dos caminos.
///
/// Ejemplo típico: encender el objeto que hay que recoger, o el NPC con el que hay que
/// hablar, solo cuando la misión que lo pide está en curso.
///
/// <b>Nunca dispara por una misión ya completada.</b> Se apoya en
/// <c>EstaDisponible</c>, que es falso para las completadas, y en que al cargar la
/// partida <c>PrecargarCompletados</c> corre ANTES que <c>PrecargarConocidos</c>: para
/// cuando la misión se registra, su estado ya es el definitivo. Así, al retomar una
/// partida donde la misión ya estaba terminada, esto se queda quieto en vez de volver
/// a encender lo que ya sobra.
///
/// Para apagar eso mismo al terminarla, pon además un <see cref="AlCompletarMision"/>
/// en el mismo GameObject: son componentes distintos, conviven sin problema.
/// </summary>
[DisallowMultipleComponent]
public class AlQuedarDisponibleMision : DisparadorDeMision
{
    [Header("Qué pasa")]
    [Tooltip("La misión se acaba de recibir, con el niño/a delante. Aquí van el " +
             "cartel de misión nueva, el sonido y lo que haya que encender.")]
    public UnityEvent alQuedarDisponible = new UnityEvent();

    [Tooltip("La misión ya estaba en curso al cargar la partida. Aquí va solo dejar " +
             "el mundo como quedó, sin anunciar nada: el aviso de 'misión nueva' al " +
             "abrir el juego sería tratar lo viejo como novedad.")]
    public UnityEvent alRestaurar = new UnityEvent();

    protected override UnityEvent EnVivo => alQuedarDisponible;
    protected override UnityEvent AlRestaurar => alRestaurar;
    protected override string Momento => "disponible";

    protected override bool YaSeCumple(MissionManager manager, string misionId) =>
        manager.EstaDisponible(misionId);

    protected override void Suscribir(MissionManager manager) =>
        manager.onDesafioDisponible.AddListener(DispararEnVivo);

    protected override void Desuscribir(MissionManager manager) =>
        manager.onDesafioDisponible.RemoveListener(DispararEnVivo);
}
