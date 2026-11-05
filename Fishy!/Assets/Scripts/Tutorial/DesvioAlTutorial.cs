using UnityEngine;

/// <summary>
/// Manda a la escena del tutorial, donde sale la oferta, mientras la partida no lo
/// haya completado u omitido.
///
/// <b>Cómo, sin tocar el menú.</b> <see cref="Segundomenu"/> entra a la escena que
/// diga su campo público <c>escenaJuego</c>, y lo lee en el momento de pulsar
/// Jugar. Este componente solo deja ahí escrito el destino correcto.
///
/// <b>Por qué se decide justo al entrar.</b> La respuesta depende de la partida
/// (<see cref="TutorialPasos.YaSeVio"/> va por su id), y la partida no se conoce
/// hasta que se crea o se elige. Por eso lo llama <see cref="Segundomenu"/> en el
/// último momento, con la partida ya puesta.
///
/// <b>Si el tutorial se abandona a medias no cuenta</b>: el estado se escribe al
/// terminarlo o saltarlo (<see cref="TutorialPasos.Registrar"/>), así que quien
/// cierre el juego en mitad recibe la oferta otra vez. Es lo que se quiere: a medias
/// no se enseñó.
/// </summary>
[DisallowMultipleComponent]
public class DesvioAlTutorial : MonoBehaviour
{
    [Tooltip("El menú por el que se entra al juego. Se busca en la escena si queda vacío.")]
    public Segundomenu menu;

    [Tooltip("Escena del tutorial. Tiene que estar en Build Settings o no se desvía nada.")]
    public string escenaTutorial = "Tutorial";

    [Tooltip("Escena del juego: el destino de siempre, para quien ya resolvió el tutorial.")]
    public string escenaDelJuego = "MainScene";

    /// <summary>Deja en el menú la escena que le toca a la partida en curso.</summary>
    public void Decidir()
    {
        if (menu == null) menu = FindAnyObjectByType<Segundomenu>();
        if (menu == null) return;

        string destino = TutorialPasos.YaSeVio() ? escenaDelJuego : escenaTutorial;

        // Una escena fuera de Build Settings no se puede cargar, y el menú solo se
        // entera al pulsar Jugar: el niño/a vería el botón fallar con un mensaje de
        // error. Antes que eso, se entra al juego como siempre.
        if (destino != escenaDelJuego && !Application.CanStreamedLevelBeLoaded(destino))
        {
            Debug.LogWarning($"[Tutorial] La escena '{destino}' no está en Build Settings: " +
                              "se entra directo al juego y el tutorial no se muestra.", this);
            destino = escenaDelJuego;
        }

        menu.escenaJuego = destino;

        // Este componente trabaja en silencio sobre un campo de otro, así que sin
        // esto no hay forma de saber qué decidió.
        int? partida = Fishy.Net.ApiManager.Instance?.PartidaId;
        Debug.Log($"[Tutorial] Se entra a '{destino}' " +
                  $"(partida {(partida.HasValue ? partida.Value.ToString() : "sin identificar")}, " +
                  $"tutorial: {TutorialPasos.EstadoDeLaPartida()}).", this);
    }
}
