using UnityEngine;

/// <summary>
/// Manda al tutorial en vez de al juego mientras este jugador no lo haya visto.
///
/// <b>Cómo, sin tocar el menú.</b> <see cref="Segundomenu"/> entra a la escena que
/// diga su campo público <c>escenaJuego</c>, y lo lee en el momento de pulsar
/// Jugar. Así que no hace falta modificar ese script: basta con dejar ahí escrito
/// el destino correcto. Este componente solo mantiene ese campo al día.
///
/// <b>Por qué se revisa cada tanto y no una sola vez.</b> La respuesta depende del
/// jugador que haya iniciado sesión (<see cref="TutorialPasos.YaSeVio"/> va por su
/// id), y en este menú se puede cambiar de perfil sin recargar la escena. Decidir
/// una vez en Start dejaría el destino del primer jugador puesto para el
/// siguiente. Es una consulta a PlayerPrefs ya cargado, así que mirarla dos veces
/// por segundo no cuesta nada.
///
/// <b>Si el tutorial se abandona a medias no cuenta</b>: la marca se escribe al
/// terminarlo (<see cref="TutorialPasos.MarcarVisto"/>), así que quien cierre el
/// juego en mitad se lo encontrará otra vez. Es lo que se quiere: a medias no se
/// enseñó.
/// </summary>
[DisallowMultipleComponent]
public class DesvioAlTutorial : MonoBehaviour
{
    [Tooltip("El menú por el que se entra al juego. Se busca en la escena si queda vacío.")]
    public Segundomenu menu;

    [Tooltip("Escena del tutorial. Tiene que estar en Build Settings o no se desvía nada.")]
    public string escenaTutorial = "Tutorial";

    [Tooltip("Escena del juego: el destino de siempre, para quien ya vio el tutorial.")]
    public string escenaDelJuego = "MainScene";

    [Tooltip("Cada cuántos segundos se vuelve a mirar quién tiene la sesión abierta.")]
    [Min(0.1f)] public float cadaCuantoRevisa = 0.5f;

    private float proximaRevision;
    private bool avisadoDeQueFalta;

    private void Start() => Revisar();

    private void Update()
    {
        if (Time.unscaledTime < proximaRevision) return;
        proximaRevision = Time.unscaledTime + cadaCuantoRevisa;
        Revisar();
    }

    private void Revisar()
    {
        if (menu == null) menu = FindAnyObjectByType<Segundomenu>();
        if (menu == null) return;

        string destino = TutorialPasos.YaSeVio() ? escenaDelJuego : escenaTutorial;

        // Una escena fuera de Build Settings no se puede cargar, y el menú solo se
        // entera al pulsar Jugar: el niño/a vería el botón fallar con un mensaje de
        // error. Antes que eso, se entra al juego como siempre.
        if (destino != escenaDelJuego && !Application.CanStreamedLevelBeLoaded(destino))
        {
            if (!avisadoDeQueFalta)
            {
                avisadoDeQueFalta = true;
                Debug.LogWarning($"[Tutorial] La escena '{destino}' no está en Build Settings: " +
                                  "se entra directo al juego y el tutorial no se muestra.", this);
            }
            destino = escenaDelJuego;
        }

        if (menu.escenaJuego == destino) return;

        menu.escenaJuego = destino;

        // Este componente trabaja en silencio sobre un campo de otro, así que sin
        // esto no hay forma de saber si llegó a decidir nada: no se ve en pantalla
        // hasta que el botón Jugar lleva a un sitio o a otro.
        int? jugador = Fishy.Net.ApiManager.Instance?.JugadorId;
        Debug.Log($"[Tutorial] El botón Jugar entrará a '{destino}' " +
                  $"(jugador {(jugador.HasValue ? jugador.Value.ToString() : "sin identificar")}, " +
                  $"tutorial visto: {TutorialPasos.YaSeVio()}).", this);
    }
}
