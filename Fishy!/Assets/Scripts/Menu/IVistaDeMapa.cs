using UnityEngine;

/// <summary>
/// Una vista del mapa del mundo sobre la que se pueden colocar cosas: la página "Mapa" del
/// celular (<see cref="MapPageUI"/>) y el minimapa del HUD (<see cref="MinimapaUI"/>).
///
/// Existe para que <see cref="MapaMarcadoresUI"/> sirva a las dos sin saber cuál es: sólo
/// necesita el contenedor que se desplaza con el mapa y la conversión mundo → mapa.
/// </summary>
public interface IVistaDeMapa
{
    /// <summary>El dibujo del mapa. Los marcadores cuelgan de él, así que se mueven con él.</summary>
    RectTransform Mapa { get; }

    /// <summary>Posición en el mapa (espacio local, con el centro del dibujo como origen) de un
    /// punto del mundo.</summary>
    Vector2 PosicionEnMapa(Vector2 mundo);
}
