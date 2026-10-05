using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lo que el niño/a ya hizo en esta partida: con qué NPC habló, qué chats atendió y qué
/// casos del Modo Detective cerró.
///
/// <b>Por qué hace falta.</b> Los objetivos de misión son de dos clases. Unos se pueden
/// consultar contra el mundo —"juntar 3 conchas" se le pregunta a la mochila, "llegar al
/// pantano" se le pregunta a la zona actual— y por eso se cumplen solos aunque la
/// condición ya estuviera dada antes de recibir la misión. Los otros son <b>hechos
/// puntuales</b>: hablar con alguien, atender un chat, cerrar un caso. Esos solo se
/// enteraban por su evento, así que <c>MissionTracker</c> avisaba de que
/// <i>"haber hablado ANTES de recibir la misión no cuenta —no hay historial—, hay que
/// volver a hacerlo"</i>.
///
/// Eso es un problema real de juego, no una sutileza: el niño/a que explora por su cuenta
/// y habla con el Huemul antes de que se la encarguen recibe después una misión que ya
/// cumplió, y tiene que volver a hablarle para que el panel se entere. Peor con los chats
/// del celular: un <c>PhoneChatLauncher</c> sin <c>repetible</c> <b>no se vuelve a
/// abrir</b>, así que ese objetivo quedaba imposible.
///
/// Esto es el historial que faltaba. No inventa una fuente de verdad nueva: solo anota el
/// hecho en el momento en que ocurre, que es justo cuando se dispara el evento que ya
/// existía.
///
/// <b>Se guarda por partida.</b> Igual que el progreso de misión en
/// <see cref="Fishy.Mision.MissionManager"/>, y por el mismo motivo: sin separar por
/// partida, lo que hizo un hermano se le aparecería al otro. Y se persiste en PlayerPrefs
/// porque quien habló con el Huemul ayer no tiene por qué volver a hacerlo hoy.
/// </summary>
public static class HistorialDeObjetivos
{
    private const string Prefijo = "Fishy.Historial.";

    private static readonly HashSet<string> _dialogos = new HashSet<string>();
    private static readonly HashSet<string> _chats    = new HashSet<string>();
    private static readonly HashSet<string> _casos    = new HashSet<string>();

    /// <summary>Partida de la que son estos datos. Null = todavía no se ató ninguna.</summary>
    private static int? _partida;

    /// <summary>
    /// Ata el historial a una partida y carga lo suyo. Llamarla dos veces con la misma no
    /// hace nada; con otra, descarta lo anterior.
    ///
    /// La llama <c>MisionBackendSync.AtarProgresoALaPartida</c>, junto al resto de los
    /// sistemas que separan el avance por perfil.
    /// </summary>
    public static void ConfigurarParaPartida(int partidaId)
    {
        if (partidaId <= 0 || _partida == partidaId) return;

        // Lo que se anotó mientras todavía no había partida es de ESTA: entre que arranca
        // la escena y que llega el PartidaId hay una ventana de segundos en la que el
        // niño/a ya puede haber hablado con alguien, y tirar eso sería el mismo bug que
        // este historial viene a arreglar.
        //
        // Pasar de una partida a OTRA sí descarta: es lo que evita que lo que hizo un
        // hermano se le aparezca al siguiente.
        bool veniaSinPartida = _partida == null;

        _partida = partidaId;
        Cargar(_dialogos, "Dialogos", veniaSinPartida);
        Cargar(_chats,    "Chats",    veniaSinPartida);
        Cargar(_casos,    "Casos",    veniaSinPartida);

        // Lo que venía en memoria todavía no estaba en disco: ahora que hay partida a la
        // que atarlo, se guarda junto con lo que ya había.
        if (veniaSinPartida)
        {
            Guardar(_dialogos, "Dialogos");
            Guardar(_chats,    "Chats");
            Guardar(_casos,    "Casos");
        }

        if (_dialogos.Count + _chats.Count + _casos.Count > 0)
            Debug.Log($"[Historial] Partida {partidaId}: {_dialogos.Count} conversación(es), " +
                      $"{_chats.Count} chat(s) y {_casos.Count} caso(s) ya hechos.");
    }

    // ── Anotar ────────────────────────────────────────────────────────────────

    /// <summary>Se terminó una conversación con un NPC del mapa.</summary>
    public static void AnotarDialogo(string dialogoId) => Anotar(_dialogos, "Dialogos", dialogoId);

    /// <summary>Se cerró un chat del celular. Se anota cada escenario de la conversación:
    /// un lanzador puede reproducir varias fases seguidas.</summary>
    public static void AnotarChats(IEnumerable<string> escenarioIds)
    {
        if (escenarioIds == null) return;
        foreach (string id in escenarioIds) Anotar(_chats, "Chats", id);
    }

    /// <summary>Se cerró un caso del Modo Detective, aprobado o no: el objetivo de misión
    /// no exige superar el umbral, solo haberlo jugado.</summary>
    public static void AnotarCasoDetective(string casoId) => Anotar(_casos, "Casos", casoId);

    // ── Preguntar ─────────────────────────────────────────────────────────────

    public static bool HabloCon(string dialogoId) => Contiene(_dialogos, dialogoId);

    public static bool ResolvioElCaso(string casoId) => Contiene(_casos, casoId);

    /// <summary>
    /// ¿Atendió el chat? <paramref name="escenarioIds"/> puede traer varios separados por
    /// coma, y basta con haber hecho <b>uno</b>: es el mismo criterio con el que
    /// <c>ObjetivoMision</c> busca el lanzador, y un objetivo que nombra varias fases se
    /// cumple con la conversación, no con cada trozo.
    /// </summary>
    public static bool AtendioElChat(string escenarioIds)
    {
        if (string.IsNullOrWhiteSpace(escenarioIds)) return false;

        foreach (string id in escenarioIds.Split(','))
            if (Contiene(_chats, id)) return true;

        return false;
    }

    /// <summary>Se olvida todo, incluido lo guardado de esta partida. Para las pruebas.</summary>
    public static void Limpiar()
    {
        if (_partida != null)
        {
            PlayerPrefs.DeleteKey(Clave("Dialogos"));
            PlayerPrefs.DeleteKey(Clave("Chats"));
            PlayerPrefs.DeleteKey(Clave("Casos"));
            PlayerPrefs.Save();
        }

        _dialogos.Clear();
        _chats.Clear();
        _casos.Clear();
        _partida = null;
    }

    // ── Dentro ────────────────────────────────────────────────────────────────

    private static bool Contiene(HashSet<string> donde, string id)
        => !string.IsNullOrWhiteSpace(id) && donde.Contains(id.Trim());

    private static void Anotar(HashSet<string> donde, string nombre, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        // Sin partida se anota igual, en memoria: entre que arranca la escena y que llega
        // el PartidaId hay una ventana en la que el niño/a ya puede haber hablado con
        // alguien, y perder ese dato sería el mismo bug que esto viene a arreglar.
        if (!donde.Add(id.Trim())) return;
        if (_partida == null) return;

        Guardar(donde, nombre);
    }

    private static string Clave(string nombre) => $"{Prefijo}{nombre}.Partida.{_partida}";

    /// <summary>
    /// Mete lo guardado de esta partida. Con <paramref name="conservarLoDeMemoria"/> se
    /// suma a lo que ya hubiera; sin él, lo reemplaza.
    /// </summary>
    private static void Cargar(HashSet<string> donde, string nombre, bool conservarLoDeMemoria)
    {
        if (!conservarLoDeMemoria) donde.Clear();

        string guardado = PlayerPrefs.GetString(Clave(nombre), "");
        if (string.IsNullOrEmpty(guardado)) return;

        foreach (string id in guardado.Split(','))
            if (!string.IsNullOrWhiteSpace(id)) donde.Add(id.Trim());
    }

    private static void Guardar(HashSet<string> donde, string nombre)
    {
        PlayerPrefs.SetString(Clave(nombre), string.Join(",", donde));
        PlayerPrefs.Save();
    }
}
