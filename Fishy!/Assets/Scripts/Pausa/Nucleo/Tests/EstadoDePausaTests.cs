using System.Collections;
using System.Collections.Generic;
using Fishy.Pausa;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// HDU-17 CA1, CA3 y CA4 — Que pausar congele y calle la entrada, que reanudar
/// devuelva el juego exactamente como estaba, y que no se pueda pausar en mitad de
/// una cinemática o de una carga.
///
/// El <c>timeScale</c> es global y las pruebas lo mueven de verdad: el TearDown lo
/// devuelve a 1 pase lo que pase, porque dejarlo en 0 colgaría a las que vengan
/// detrás —y al editor— sin ningún mensaje que explique por qué.
/// </summary>
public class EstadoDePausaTests
{
    /// <summary>
    /// Un doble de algo que lee la entrada del jugador: el movimiento de Otto, los
    /// botones táctiles. Anota cuántas veces lo callaron y lo dejaron hablar, que es
    /// lo que distingue «se reanudó» de «nunca se suspendió».
    /// </summary>
    private class EntradaFalsa : IEntradaPausable
    {
        public bool EntradaActiva { get; private set; } = true;
        public int Suspensiones;
        public int Reanudaciones;

        public EntradaFalsa(bool activaAlEmpezar = true) => EntradaActiva = activaAlEmpezar;

        public void SuspenderEntrada() { EntradaActiva = false; Suspensiones++; }
        public void ReanudarEntrada()  { EntradaActiva = true;  Reanudaciones++; }
    }

    /// <summary>
    /// Un doble de una sesión abierta —un caso del Modo Detective, un chat— que
    /// guarda lo que el niño/a lleva respondido. Existe para comprobar CA4: la pausa
    /// no la toca, así que al reanudar sigue teniendo lo mismo.
    /// </summary>
    private class SesionFalsa : IEntradaPausable
    {
        public readonly List<string> Respuestas = new List<string>();

        public bool EntradaActiva { get; private set; } = true;
        public void SuspenderEntrada() => EntradaActiva = false;
        public void ReanudarEntrada()  => EntradaActiva = true;
    }

    private readonly List<IEntradaPausable> _registrados = new List<IEntradaPausable>();

    [SetUp]
    public void SetUp()
    {
        EstadoDePausa.Restablecer();
        _registrados.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (IEntradaPausable quien in _registrados) PuertaDeEntrada.Soltar(quien);
        _registrados.Clear();

        EstadoDePausa.SoltarBloqueo(Cinematica);
        EstadoDePausa.SoltarBloqueo(Carga);
        EstadoDePausa.Restablecer();
        Time.timeScale = 1f;
    }

    private const string Cinematica = "cinemática";
    private const string Carga = "pantalla de carga";

    /// <summary>Apunta la entrada y se acuerda de soltarla en el TearDown: el
    /// registro es estático y se lo llevaría a la prueba siguiente.</summary>
    private T Apuntar<T>(T quien) where T : IEntradaPausable
    {
        PuertaDeEntrada.Registrar(quien);
        _registrados.Add(quien);
        return quien;
    }

    // ── CA3: congelar y callar la entrada ────────────────────────────────────

    [UnityTest]
    public IEnumerator Pausar_CongelaElTiempo_YDesactivaLaEntrada()
    {
        yield return null;

        EntradaFalsa otto = Apuntar(new EntradaFalsa());

        Assert.IsTrue(EstadoDePausa.Pausar());

        Assert.AreEqual(0f, Time.timeScale, "CA3: la acción tiene que quedar congelada");
        Assert.IsTrue(EstadoDePausa.Pausado);
        Assert.IsFalse(otto.EntradaActiva, "CA3: el input del jugador se desactiva");
        Assert.IsTrue(PuertaDeEntrada.Suspendida);
        Assert.AreEqual(1, otto.Suspensiones);
    }

    [UnityTest]
    public IEnumerator Pausar_DosVeces_NoCuentaComoDosPausas()
    {
        yield return null;

        EntradaFalsa otto = Apuntar(new EntradaFalsa());

        Assert.IsTrue(EstadoDePausa.Pausar());
        Assert.IsFalse(EstadoDePausa.Pausar(), "ya estaba pausado");

        EstadoDePausa.Reanudar();

        // Si la segunda pausa hubiera vuelto a mirar quién estaba activo, habría
        // visto a Otto ya callado y al reanudar no lo habría encendido.
        Assert.IsTrue(otto.EntradaActiva);
        Assert.AreEqual(1f, Time.timeScale);
    }

    [UnityTest]
    public IEnumerator Pausar_ConservaElTimeScaleQueHabia_YNoSuponeQueEraUno()
    {
        yield return null;

        Time.timeScale = 0.5f;   // una cinemática a cámara lenta, por ejemplo

        EstadoDePausa.Pausar();
        Assert.AreEqual(0f, Time.timeScale);

        EstadoDePausa.Reanudar();
        Assert.AreEqual(0.5f, Time.timeScale, 0.0001f,
            "reanudar devuelve el ritmo que había, no un 1 inventado");
    }

    // ── CA4: reanudar deja todo donde estaba ─────────────────────────────────

    [UnityTest]
    public IEnumerator Reanudar_DevuelveElTiempoYLaEntrada()
    {
        yield return null;

        EntradaFalsa otto = Apuntar(new EntradaFalsa());

        EstadoDePausa.Pausar();
        Assert.IsTrue(EstadoDePausa.Reanudar());

        Assert.AreEqual(1f, Time.timeScale);
        Assert.IsFalse(EstadoDePausa.Pausado);
        Assert.IsTrue(otto.EntradaActiva);
        Assert.IsFalse(PuertaDeEntrada.Suspendida);
        Assert.AreEqual(1, otto.Reanudaciones);
    }

    [UnityTest]
    public IEnumerator Reanudar_NoDespiertaLaEntrada_QueYaEstabaApagadaAntesDePausar()
    {
        yield return null;

        // Otto bloqueado por un diálogo: MenuController.BloquearAOtto ya lo apagó.
        EntradaFalsa otto = Apuntar(new EntradaFalsa(activaAlEmpezar: false));

        EstadoDePausa.Pausar();
        EstadoDePausa.Reanudar();

        Assert.IsFalse(otto.EntradaActiva,
            "pulsar Esc en mitad de una conversación no puede soltar a Otto");
        Assert.AreEqual(0, otto.Reanudaciones);
    }

    [UnityTest]
    public IEnumerator ReanudarUnaSesionDeChatODetective_NoPierdeLasRespuestasYaEntregadas()
    {
        yield return null;

        SesionFalsa caso = Apuntar(new SesionFalsa());
        caso.Respuestas.Add("DC01_MSG02");
        caso.Respuestas.Add("DC01_MSG05");

        EstadoDePausa.Pausar();

        // Con el juego congelado la sesión sigue montada y con lo suyo intacto.
        CollectionAssert.AreEqual(new[] { "DC01_MSG02", "DC01_MSG05" }, caso.Respuestas);
        Assert.IsFalse(caso.EntradaActiva);

        EstadoDePausa.Reanudar();

        CollectionAssert.AreEqual(new[] { "DC01_MSG02", "DC01_MSG05" }, caso.Respuestas,
            "CA4: se reanuda donde se pausó, sin perder respuestas ya entregadas");
        Assert.IsTrue(caso.EntradaActiva, "y el caso vuelve a aceptar marcas");

        caso.Respuestas.Add("DC01_MSG07");
        Assert.AreEqual(3, caso.Respuestas.Count, "se puede seguir jugando donde se dejó");
    }

    [UnityTest]
    public IEnumerator Alternar_PausaYDespuesReanuda()
    {
        yield return null;

        Assert.IsTrue(EstadoDePausa.Alternar());
        Assert.IsTrue(EstadoDePausa.Pausado);

        Assert.IsTrue(EstadoDePausa.Alternar());
        Assert.IsFalse(EstadoDePausa.Pausado);
        Assert.AreEqual(1f, Time.timeScale);
    }

    // ── CA1: no se puede pausar en una cinemática ni en una carga ────────────

    [UnityTest]
    public IEnumerator NoSePausa_DuranteUnaCinematica()
    {
        yield return null;

        bool cinematicaCorriendo = true;
        EstadoDePausa.RegistrarBloqueo(Cinematica, () => cinematicaCorriendo);
        EntradaFalsa otto = Apuntar(new EntradaFalsa());

        Assert.IsFalse(EstadoDePausa.SePuedePausar(out string motivo));
        Assert.AreEqual(Cinematica, motivo);

        LogAssert.Expect(LogType.Log, $"[Pausa] No se pausa: {Cinematica} está en curso.");
        Assert.IsFalse(EstadoDePausa.Pausar());

        Assert.IsFalse(EstadoDePausa.Pausado);
        Assert.AreEqual(1f, Time.timeScale, "el juego sigue corriendo");
        Assert.IsTrue(otto.EntradaActiva, "y nadie le quitó el control al jugador");

        // Terminada la cinemática, Esc vuelve a funcionar.
        cinematicaCorriendo = false;
        Assert.IsTrue(EstadoDePausa.Pausar());
    }

    [UnityTest]
    public IEnumerator NoSePausa_DuranteUnaTransicionDeCarga()
    {
        yield return null;

        EstadoDePausa.RegistrarBloqueo(Carga, () => true);

        LogAssert.Expect(LogType.Log, $"[Pausa] No se pausa: {Carga} está en curso.");
        Assert.IsFalse(EstadoDePausa.Pausar());
        Assert.IsFalse(EstadoDePausa.Pausado);
    }

    [UnityTest]
    public IEnumerator UnBloqueoSoltado_YaNoVetaLaPausa()
    {
        yield return null;

        EstadoDePausa.RegistrarBloqueo(Cinematica, () => true);
        EstadoDePausa.SoltarBloqueo(Cinematica);

        Assert.IsTrue(EstadoDePausa.SePuedePausar());
        Assert.IsTrue(EstadoDePausa.Pausar());
    }

    [UnityTest]
    public IEnumerator UnBloqueoQueRevienta_NoDejaAlNinoSinPoderPausar()
    {
        yield return null;

        EstadoDePausa.RegistrarBloqueo(Cinematica,
            () => throw new System.InvalidOperationException("la cinemática se rompió"));

        LogAssert.Expect(LogType.Warning,
            "[Pausa] El bloqueo 'cinemática' falló al responder y se ignora: la cinemática se rompió");

        Assert.IsTrue(EstadoDePausa.Pausar(),
            "un veto roto no puede dejar el juego sin menú de pausa");
    }

    // ── La puerta de entrada ─────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator QuienSeApuntaConLaPausaYaPuesta_SeCallaEnElActo()
    {
        yield return null;

        EstadoDePausa.Pausar();

        EntradaFalsa tardio = Apuntar(new EntradaFalsa());
        Assert.IsFalse(tardio.EntradaActiva,
            "un Otto que aparece durante la pausa no puede caminar con el juego congelado");

        EstadoDePausa.Reanudar();
        Assert.IsTrue(tardio.EntradaActiva);
    }

    [UnityTest]
    public IEnumerator QuienSeEnciendeDuranteLaPausa_SeCalla_YRevivePorFinDeLaPausa()
    {
        yield return null;

        EstadoDePausa.Pausar();

        // Como Otto: se apunta en OnEnable todavía apagado y se enciende en Start,
        // un frame después y con el juego ya congelado.
        var otto = new EntradaFalsa(activaAlEmpezar: false);
        Apuntar(otto);
        Assert.AreEqual(0, otto.Suspensiones, "apagado no hay nada que callar");

        otto.ReanudarEntrada();            // el EnableMovement() de su Start
        PuertaDeEntrada.Revisar(otto);

        Assert.IsFalse(otto.EntradaActiva, "no puede caminar con el mundo congelado");

        EstadoDePausa.Reanudar();
        Assert.IsTrue(otto.EntradaActiva,
            "y al volver de la pausa el niño/a tiene que poder moverse");
    }

    [UnityTest]
    public IEnumerator QuienSeSuelta_NoRecibeNadaAlReanudar()
    {
        yield return null;

        EntradaFalsa otto = Apuntar(new EntradaFalsa());
        EstadoDePausa.Pausar();

        PuertaDeEntrada.Soltar(otto);
        EstadoDePausa.Reanudar();

        Assert.AreEqual(0, otto.Reanudaciones);
    }

    [UnityTest]
    public IEnumerator Restablecer_DescongelaAunqueNadieHubieraPausado()
    {
        yield return null;

        Time.timeScale = 0f;
        EstadoDePausa.Restablecer();

        Assert.AreEqual(1f, Time.timeScale);
        Assert.IsFalse(EstadoDePausa.Pausado);
    }
}
