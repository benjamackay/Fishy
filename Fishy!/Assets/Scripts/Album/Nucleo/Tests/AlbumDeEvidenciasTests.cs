using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fishy.Album;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// HDU-12 CA5 — Qué guarda el álbum al agregar una evidencia y qué pasa cuando el
/// mismo caso se juega varias veces o se siembra desde el servidor.
///
/// Las partidas son ficticias y de números altos: se escriben de verdad en
/// PlayerPrefs y el TearDown borra sus claves. No se usa PlayerPrefs.DeleteAll,
/// que se llevaría el avance real de quien corra las pruebas en su equipo.
/// </summary>
public class AlbumDeEvidenciasTests
{
    private const int PartidaA = 991201;
    private const int PartidaB = 991202;

    [SetUp]
    public void SetUp() => AlbumDeEvidencias.Limpiar();

    [TearDown]
    public void TearDown()
    {
        AlbumDeEvidencias.Limpiar();
        BorrarPrefs();
    }

    private static void BorrarPrefs()
    {
        PlayerPrefs.DeleteKey($"{AlbumDeEvidencias.Prefijo}Evidencias.Partida.{PartidaA}");
        PlayerPrefs.DeleteKey($"{AlbumDeEvidencias.Prefijo}Evidencias.Partida.{PartidaB}");
        PlayerPrefs.Save();
    }

    /// <summary>Una evidencia como la que arma el puente desde un caso real.</summary>
    private static EvidenciaAlbum Senal(string casoId, string mensajeId, string texto = "oye cuántos años tienes?")
    {
        return new EvidenciaAlbum
        {
            casoId = casoId,
            mensajeId = mensajeId,
            casoTitulo = "Los Mensajes del Puma al Pudú",
            casoZona = "desconocidos",
            etiquetas = new List<string> { "grooming", "privacidad" },
            autor = "Puma",
            texto = texto,
            explicacion = "Pedir la edad a un desconocido de internet es una alerta.",
        };
    }

    // ── Metadatos (CA1) ──────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator AlRegistrar_LaEvidenciaGuardaTodosSusMetadatos()
    {
        yield return null;

        AlbumDeEvidencias.Registrar(Senal("DC_CASO_01", "DC01_MSG03"));

        EvidenciaAlbum guardada = AlbumDeEvidencias.Todas().Single();
        Assert.AreEqual("DC_CASO_01", guardada.casoId);
        Assert.AreEqual("DC01_MSG03", guardada.mensajeId);
        Assert.AreEqual("Los Mensajes del Puma al Pudú", guardada.casoTitulo);
        Assert.AreEqual("desconocidos", guardada.casoZona);
        Assert.AreEqual("Puma", guardada.autor);
        Assert.AreEqual("oye cuántos años tienes?", guardada.texto);
        Assert.IsTrue(guardada.explicacion.StartsWith("Pedir la edad"));
        CollectionAssert.AreEqual(new[] { "grooming", "privacidad" }, guardada.etiquetas);
        Assert.IsTrue(guardada.registradaEn > 0, "tiene que quedar fechada para poder ordenarla");
        Assert.IsTrue(guardada.EstaCompleta);
    }

    [UnityTest]
    public IEnumerator Registrar_NoSeQuedaConLaInstanciaQueLePasaron()
    {
        yield return null;

        EvidenciaAlbum suelta = Senal("DC_CASO_01", "DC01_MSG03");
        AlbumDeEvidencias.Registrar(suelta);
        suelta.texto = "otra cosa";

        Assert.AreEqual("oye cuántos años tienes?", AlbumDeEvidencias.Todas().Single().texto);
    }

    // ── Idempotencia ─────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator RegistrarDosVeces_LaMismaSenal_NoDuplica()
    {
        yield return null;

        Assert.IsTrue(AlbumDeEvidencias.Registrar(Senal("DC_CASO_01", "DC01_MSG03")));
        Assert.IsFalse(AlbumDeEvidencias.Registrar(Senal("DC_CASO_01", "DC01_MSG03")),
            "la segunda vez no cambia nada");

        Assert.AreEqual(1, AlbumDeEvidencias.Cantidad);
    }

    [UnityTest]
    public IEnumerator Registrar_ConMasDatos_CompletaLaQueYaEstaba_SinDuplicar()
    {
        yield return null;

        AlbumDeEvidencias.SembrarIds("DC_CASO_01", new[] { "DC01_MSG03" });
        Assert.IsFalse(AlbumDeEvidencias.Todas().Single().EstaCompleta);

        Assert.IsTrue(AlbumDeEvidencias.Registrar(Senal("DC_CASO_01", "DC01_MSG03")));

        Assert.AreEqual(1, AlbumDeEvidencias.Cantidad);
        Assert.IsTrue(AlbumDeEvidencias.Todas().Single().EstaCompleta);
        Assert.AreEqual("desconocidos", AlbumDeEvidencias.Todas().Single().casoZona);
    }

    [UnityTest]
    public IEnumerator Registrar_ConTextoVacio_NoBorraElTextoQueYaHabia()
    {
        yield return null;

        AlbumDeEvidencias.Registrar(Senal("DC_CASO_01", "DC01_MSG03"));
        AlbumDeEvidencias.Registrar(new EvidenciaAlbum { casoId = "DC_CASO_01", mensajeId = "DC01_MSG03" });

        Assert.AreEqual("oye cuántos años tienes?", AlbumDeEvidencias.Todas().Single().texto);
    }

    [UnityTest]
    public IEnumerator Registrar_SinIds_NoGuardaNada()
    {
        yield return null;

        Assert.IsFalse(AlbumDeEvidencias.Registrar(new EvidenciaAlbum { casoId = "DC_CASO_01" }));
        Assert.IsFalse(AlbumDeEvidencias.Registrar(new EvidenciaAlbum { mensajeId = "DC01_MSG03" }));
        Assert.IsFalse(AlbumDeEvidencias.Registrar(null));

        Assert.AreEqual(0, AlbumDeEvidencias.Cantidad);
    }

    // ── Siembra desde el servidor ────────────────────────────────────────────

    [UnityTest]
    public IEnumerator SembrarIds_CreaEsqueletosSoloDeLoQueFalta_YNoBorraNada()
    {
        yield return null;

        AlbumDeEvidencias.Registrar(Senal("DC_CASO_01", "DC01_MSG03"));

        int nuevas = AlbumDeEvidencias.SembrarIds("DC_CASO_01", new[] { "DC01_MSG03", "DC01_MSG05" });

        Assert.AreEqual(1, nuevas, "DC01_MSG03 ya estaba");
        Assert.AreEqual(2, AlbumDeEvidencias.Cantidad);
        Assert.AreEqual(1, AlbumDeEvidencias.CantidadIncompletas);
        Assert.AreEqual("oye cuántos años tienes?",
            AlbumDeEvidencias.Todas().First(e => e.mensajeId == "DC01_MSG03").texto,
            "la semilla no puede empeorar lo que se ganó jugando");
    }

    [UnityTest]
    public IEnumerator SembrarIds_DosVeces_ElMismoCaso_NoCambiaNada()
    {
        yield return null;

        // Pasa siempre: cada DetectiveLauncher de la escena pide el progreso.
        Assert.AreEqual(2, AlbumDeEvidencias.SembrarIds("DC_CASO_01", new[] { "DC01_MSG03", "DC01_MSG05" }));
        Assert.AreEqual(0, AlbumDeEvidencias.SembrarIds("DC_CASO_01", new[] { "DC01_MSG03", "DC01_MSG05" }));

        Assert.AreEqual(2, AlbumDeEvidencias.Cantidad);
    }

    // ── Conciliación ─────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ConciliarConElCaso_CompletaLosEsqueletos_YNoCreaNadaNuevo()
    {
        yield return null;

        AlbumDeEvidencias.SembrarIds("DC_CASO_01", new[] { "DC01_MSG03", "DC01_MSG05" });

        var senalesDelCaso = new[]
        {
            Senal("DC_CASO_01", "DC01_MSG03"),
            Senal("DC_CASO_01", "DC01_MSG05"),
            Senal("DC_CASO_01", "DC01_MSG07"),   // válida, pero el niño/a no la marcó
        };
        AlbumDeEvidencias.ConciliarConElCaso("DC_CASO_01", senalesDelCaso);

        Assert.AreEqual(2, AlbumDeEvidencias.Cantidad, "conciliar no inventa evidencias");
        Assert.AreEqual(0, AlbumDeEvidencias.CantidadIncompletas);
        Assert.IsFalse(AlbumDeEvidencias.Contiene("DC_CASO_01", "DC01_MSG07"));
    }

    [UnityTest]
    public IEnumerator ConciliarConElCaso_DescartaElEsqueletoDeUnaSenalQueNoExiste_PeroNoLaEvidenciaGanadaLocalmente()
    {
        yield return null;

        // DC01_MSG06 es el mensaje ambiguo: el servidor lo guarda si el niño/a lo
        // marcó, pero el álbum no lo premia.
        AlbumDeEvidencias.SembrarIds("DC_CASO_01", new[] { "DC01_MSG06" });
        AlbumDeEvidencias.Registrar(Senal("DC_CASO_01", "DC01_MSG03"));

        AlbumDeEvidencias.ConciliarConElCaso("DC_CASO_01", new[] { Senal("DC_CASO_01", "DC01_MSG05") });

        Assert.IsFalse(AlbumDeEvidencias.Contiene("DC_CASO_01", "DC01_MSG06"),
            "el esqueleto de una señal que no es válida se descarta");
        Assert.IsTrue(AlbumDeEvidencias.Contiene("DC_CASO_01", "DC01_MSG03"),
            "lo que se ganó jugando acá no se borra nunca");
        Assert.IsFalse(AlbumDeEvidencias.Contiene("DC_CASO_01", "DC01_MSG05"),
            "y tampoco se crea lo que no estaba");
    }

    [UnityTest]
    public IEnumerator ConciliarConElCaso_NoToca_LasEvidenciasDeOtroCaso()
    {
        yield return null;

        AlbumDeEvidencias.SembrarIds("DC_CASO_02", new[] { "DC02_MSG03" });
        AlbumDeEvidencias.ConciliarConElCaso("DC_CASO_01", new[] { Senal("DC_CASO_01", "DC01_MSG03") });

        Assert.IsTrue(AlbumDeEvidencias.Contiene("DC_CASO_02", "DC02_MSG03"));
    }

    // ── Partida ──────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ConfigurarParaPartida_ConservaLoAnotadoAntesDeTenerPartida_YLoGuarda()
    {
        yield return null;

        // La ventana real: el caso se cerró antes de que llegara el PartidaId.
        AlbumDeEvidencias.Registrar(Senal("DC_CASO_01", "DC01_MSG03"));
        AlbumDeEvidencias.ConfigurarParaPartida(PartidaA);

        Assert.AreEqual(1, AlbumDeEvidencias.Cantidad, "no se tira lo de memoria");

        // Y quedó en disco: irse a otra partida y volver lo demuestra.
        AlbumDeEvidencias.ConfigurarParaPartida(PartidaB);
        Assert.AreEqual(0, AlbumDeEvidencias.Cantidad);

        AlbumDeEvidencias.ConfigurarParaPartida(PartidaA);
        Assert.AreEqual(1, AlbumDeEvidencias.Cantidad);
        Assert.AreEqual("Puma", AlbumDeEvidencias.Todas().Single().autor);
    }

    [UnityTest]
    public IEnumerator ConfigurarParaPartida_ConOtraPartida_DescartaLoDeLaAnterior()
    {
        yield return null;

        AlbumDeEvidencias.ConfigurarParaPartida(PartidaA);
        AlbumDeEvidencias.Registrar(Senal("DC_CASO_01", "DC01_MSG03"));

        AlbumDeEvidencias.ConfigurarParaPartida(PartidaB);

        Assert.AreEqual(0, AlbumDeEvidencias.Cantidad,
            "lo de un hermano no le puede aparecer al siguiente");
    }

    [UnityTest]
    public IEnumerator ConfigurarParaPartida_ConLaMisma_NoHaceNada()
    {
        yield return null;

        AlbumDeEvidencias.ConfigurarParaPartida(PartidaA);
        AlbumDeEvidencias.Registrar(Senal("DC_CASO_01", "DC01_MSG03"));
        AlbumDeEvidencias.ConfigurarParaPartida(PartidaA);

        Assert.AreEqual(1, AlbumDeEvidencias.Cantidad);
    }
}
