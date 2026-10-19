using System.Collections;
using System.Collections.Generic;
using Fishy.Mision;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// HDU-16 — Pruebas PlayMode de la misión activa y de la zona que hay que señalar.
///
/// Cubren los tres puntos que pide la sección de Testing de la HDU:
///
///   1. Que el cartel muestre el objetivo correcto según el progreso → aquí se
///      prueba de dónde saca el cartel esa misión: <see cref="MissionManager.Activa"/>.
///   2. Que al completar la activa pase sola a la siguiente, o se quede sin ninguna.
///   3. Que el indicador de zona se encienda al empezar la misión y se apague al
///      completarla.
///
/// <b>Se prueba la decisión, no el dibujo.</b> <c>MissionUIController</c> y
/// <c>ZoneMarker</c> viven en Assembly-CSharp —dependen de NPC, ItemData y de la
/// cámara— y Unity no permite que un assembly de tests con .asmdef referencie el
/// ensamblado por defecto. Por eso la decisión completa está en este lado:
/// <see cref="MissionManager.ZonaObjetivoActiva"/> es literalmente el interruptor
/// del marcador, que se limita a obedecerlo (<c>Apuntar(zona)</c>). Probar aquí es
/// probar eso, sin escena ni cámara de por medio.
/// </summary>
public class MisionActivaTests
{
    private MissionManager manager;
    private readonly List<DesafioData> fichas = new List<DesafioData>();

    [SetUp]
    public void SetUp()
    {
        if (MissionManager.Instance != null)
            Object.DestroyImmediate(MissionManager.Instance.gameObject);

        var go = new GameObject("MissionManagerTest");
        manager = go.AddComponent<MissionManager>();
        manager.persistirLocalmente = false;   // aislar de PlayerPrefs entre corridas
    }

    [TearDown]
    public void TearDown()
    {
        if (manager != null) Object.DestroyImmediate(manager.gameObject);

        foreach (DesafioData ficha in fichas)
            if (ficha != null) Object.DestroyImmediate(ficha);
        fichas.Clear();
    }

    /// <summary>Ficha de prueba. Se apunta para destruirla en el TearDown.</summary>
    private DesafioData Ficha(string id, string titulo, int orden, string zona = null)
    {
        var data = ScriptableObject.CreateInstance<DesafioData>();
        data.desafioId = id;
        data.titulo = titulo;
        data.orden = orden;
        data.zonaObjetivo = zona;
        fichas.Add(data);
        return data;
    }

    // ── Cuál es la misión activa ─────────────────────────────────────────────

    [UnityTest]
    public IEnumerator SinNingunaRegistrada_NoHayMisionActiva()
    {
        yield return null;

        Assert.IsNull(manager.Activa);
        Assert.IsFalse(manager.HayMisionActiva);
        Assert.IsNull(manager.ZonaObjetivoActiva);
    }

    [UnityTest]
    public IEnumerator LaActiva_EsLaDisponibleDeMenorOrden()
    {
        yield return null;

        // A propósito registradas al revés del orden de historia: si mandara el
        // orden de registro —o el alfabético, que es lo que había antes— la activa
        // saldría "Zampar algo".
        manager.RegistrarDesafioDisponible(Ficha("M_30", "Zampar algo", 30));
        manager.RegistrarDesafioDisponible(Ficha("M_10", "Buscar a Coipo", 10));
        manager.RegistrarDesafioDisponible(Ficha("M_20", "Abrir el mapa", 20));

        Assert.IsNotNull(manager.Activa);
        Assert.AreEqual("M_10", manager.Activa.Id);
    }

    [UnityTest]
    public IEnumerator RegistrarUnaMasUrgente_SeVuelveLaActiva()
    {
        yield return null;

        manager.RegistrarDesafioDisponible(Ficha("M_20", "Abrir el mapa", 20));
        Assert.AreEqual("M_20", manager.Activa.Id);

        manager.RegistrarDesafioDisponible(Ficha("M_10", "Buscar a Coipo", 10));

        Assert.AreEqual("M_10", manager.Activa.Id);
    }

    [UnityTest]
    public IEnumerator UnaMisionYaCompletada_NuncaEsLaActiva()
    {
        yield return null;

        DesafioData primera = Ficha("M_10", "Buscar a Coipo", 10);
        manager.PrecargarCompletados(new[] { "M_10" });
        manager.RegistrarDesafioDisponible(primera);
        manager.RegistrarDesafioDisponible(Ficha("M_20", "Abrir el mapa", 20));

        Assert.AreEqual("M_20", manager.Activa.Id);
    }

    // ── CA5: al completar, entra la siguiente ────────────────────────────────

    [UnityTest]
    public IEnumerator CompletarLaActiva_PasaALaSiguienteDisponible()
    {
        yield return null;

        manager.RegistrarDesafioDisponible(Ficha("M_10", "Buscar a Coipo", 10));
        manager.RegistrarDesafioDisponible(Ficha("M_20", "Abrir el mapa", 20));
        Assert.AreEqual("M_10", manager.Activa.Id);

        manager.CompletarDesafio("M_10");

        Assert.AreEqual("M_20", manager.Activa.Id);
        Assert.IsTrue(manager.HayMisionActiva);
    }

    [UnityTest]
    public IEnumerator CompletarUnaQueNoEsLaActiva_NoCambiaLaActiva()
    {
        yield return null;

        manager.RegistrarDesafioDisponible(Ficha("M_10", "Buscar a Coipo", 10));
        manager.RegistrarDesafioDisponible(Ficha("M_20", "Abrir el mapa", 20));

        manager.CompletarDesafio("M_20");

        Assert.AreEqual("M_10", manager.Activa.Id);
    }

    [UnityTest]
    public IEnumerator AlCambiarLaActiva_SeAvisaConLaNueva()
    {
        yield return null;

        var avisadas = new List<string>();
        manager.onMisionActivaCambiada.AddListener(m => avisadas.Add(m != null ? m.Id : null));

        manager.RegistrarDesafioDisponible(Ficha("M_10", "Buscar a Coipo", 10));
        manager.RegistrarDesafioDisponible(Ficha("M_20", "Abrir el mapa", 20));
        manager.CompletarDesafio("M_10");

        // Registrar la segunda no cambia la activa, así que no debe avisar de ella.
        CollectionAssert.AreEqual(new[] { "M_10", "M_20" }, avisadas);
    }

    // ── CA6: cuando ya no quedan ─────────────────────────────────────────────

    [UnityTest]
    public IEnumerator CompletarLaUltima_DejaSinMisionActiva()
    {
        yield return null;

        manager.RegistrarDesafioDisponible(Ficha("M_10", "Buscar a Coipo", 10));

        manager.CompletarDesafio("M_10");

        // Es lo que hace que el cartel enseñe "no hay nuevas misiones disponibles".
        Assert.IsNull(manager.Activa);
        Assert.IsFalse(manager.HayMisionActiva);
    }

    [UnityTest]
    public IEnumerator CompletarLaUltima_AvisaConNull()
    {
        yield return null;

        manager.RegistrarDesafioDisponible(Ficha("M_10", "Buscar a Coipo", 10));

        bool avisoDeVacio = false;
        manager.onMisionActivaCambiada.AddListener(m => avisoDeVacio = m == null);

        manager.CompletarDesafio("M_10");

        Assert.IsTrue(avisoDeVacio, "Quedarse sin misiones tiene que avisar, no callarse.");
    }

    // ── CA2 y CA4: el interruptor del ZoneMarker ─────────────────────────────

    [UnityTest]
    public IEnumerator ZonaObjetivo_SeEnciendeAlEmpezarLaMision()
    {
        yield return null;

        manager.RegistrarDesafioDisponible(Ficha("M_10", "Buscar a Coipo", 10, "zona_2"));

        Assert.AreEqual("zona_2", manager.ZonaObjetivoActiva);
    }

    [UnityTest]
    public IEnumerator ZonaObjetivo_SeApagaAlCompletarLaMision()
    {
        yield return null;

        manager.RegistrarDesafioDisponible(Ficha("M_10", "Buscar a Coipo", 10, "zona_2"));
        Assert.AreEqual("zona_2", manager.ZonaObjetivoActiva);

        manager.CompletarDesafio("M_10");

        Assert.IsNull(manager.ZonaObjetivoActiva, "Sin misión activa no hay zona que señalar.");
    }

    [UnityTest]
    public IEnumerator ZonaObjetivo_PasaALaDeLaSiguienteMision()
    {
        yield return null;

        manager.RegistrarDesafioDisponible(Ficha("M_10", "Buscar a Coipo", 10, "zona_2"));
        manager.RegistrarDesafioDisponible(Ficha("M_20", "Abrir el mapa", 20, "zona_3"));

        manager.CompletarDesafio("M_10");

        Assert.AreEqual("zona_3", manager.ZonaObjetivoActiva);
    }

    [UnityTest]
    public IEnumerator MisionSinZona_NoSenalaNada()
    {
        yield return null;

        manager.RegistrarDesafioDisponible(Ficha("M_10", "Buscar a Coipo", 10));

        Assert.IsNotNull(manager.Activa);
        Assert.IsNull(manager.ZonaObjetivoActiva);
    }

    [UnityTest]
    public IEnumerator ZonaObjetivo_IgnoraElRelleno()
    {
        yield return null;

        // El campo se deja vacío en el Inspector, y "vacío" en Unity es "" y no null.
        manager.RegistrarDesafioDisponible(Ficha("M_10", "Buscar a Coipo", 10, "   "));

        Assert.IsNull(manager.ZonaObjetivoActiva);
    }

    // ── La lista del Tab cuenta lo mismo ─────────────────────────────────────

    [UnityTest]
    public IEnumerator GetListaOrdenada_PoneLaActivaPrimero()
    {
        yield return null;

        manager.RegistrarDesafioDisponible(Ficha("M_30", "Abrir el mapa", 30));
        manager.RegistrarDesafioDisponible(Ficha("M_10", "Zampar algo", 10));
        manager.RegistrarDesafioDisponible(Ficha("M_20", "Buscar a Coipo", 20));
        manager.CompletarDesafio("M_20");

        List<DesafioRuntime> lista = manager.GetListaOrdenada();

        // Disponibles por orden de historia y las completadas al final.
        CollectionAssert.AreEqual(new[] { "M_10", "M_30", "M_20" },
            lista.ConvertAll(d => d.Id));
        Assert.AreSame(manager.Activa, lista[0]);
    }

    [UnityTest]
    public IEnumerator CambiarDePartida_OlvidaLaMisionActiva()
    {
        yield return null;

        manager.RegistrarDesafioDisponible(Ficha("M_10", "Buscar a Coipo", 10, "zona_2"));
        Assert.IsNotNull(manager.Activa);

        // Elegir otra partida no puede dejar colgada la misión de la anterior: sería
        // enseñarle al niño/a una misión que en su partida todavía no existe.
        manager.ConfigurarPersistenciaParaPartida(77);

        Assert.IsNull(manager.Activa);
        Assert.IsNull(manager.ZonaObjetivoActiva);
    }
}
