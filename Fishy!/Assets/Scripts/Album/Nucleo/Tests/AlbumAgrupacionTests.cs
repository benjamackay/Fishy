using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fishy.Album;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// HDU-12 CA5 — Cómo se agrupa el álbum para la vista (CA2) y qué sobrevive a un
/// reinicio.
/// </summary>
public class AlbumAgrupacionTests
{
    private const int Partida = 991203;
    private const int OtraPartida = 991204;
    private static string ClavePrefs => $"{AlbumDeEvidencias.Prefijo}Evidencias.Partida.{Partida}";

    [SetUp]
    public void SetUp() => AlbumDeEvidencias.Limpiar();

    [TearDown]
    public void TearDown()
    {
        AlbumDeEvidencias.Limpiar();
        PlayerPrefs.DeleteKey(ClavePrefs);
        PlayerPrefs.DeleteKey($"{AlbumDeEvidencias.Prefijo}Evidencias.Partida.{OtraPartida}");
        PlayerPrefs.Save();
    }

    private static EvidenciaAlbum Puma(string mensajeId)
    {
        return new EvidenciaAlbum
        {
            casoId = "DC_CASO_01",
            mensajeId = mensajeId,
            casoTitulo = "Los Mensajes del Puma al Pudú",
            casoZona = "desconocidos",
            etiquetas = new List<string> { "grooming", "privacidad" },
            autor = "Puma",
            texto = "texto del " + mensajeId,
            explicacion = "explicación del " + mensajeId,
        };
    }

    private static EvidenciaAlbum Flamenco(string mensajeId)
    {
        return new EvidenciaAlbum
        {
            casoId = "DC_CASO_02",
            mensajeId = mensajeId,
            casoTitulo = "El Grupo del Pantano de Flamenco",
            casoZona = "ciberacoso",
            etiquetas = new List<string> { "ciberacoso", "exclusion" },
            autor = "Flamenco",
            texto = "texto del " + mensajeId,
            explicacion = "explicación del " + mensajeId,
        };
    }

    // ── Por caso (CA2) ───────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator PorCaso_AgrupaCadaEvidenciaBajoSuCaso_ConTituloYZona()
    {
        yield return null;

        AlbumDeEvidencias.Registrar(Puma("DC01_MSG03"));
        AlbumDeEvidencias.Registrar(Puma("DC01_MSG05"));
        AlbumDeEvidencias.Registrar(Flamenco("DC02_MSG03"));

        List<GrupoAlbum> grupos = AlbumDeEvidencias.PorCaso();

        Assert.AreEqual(2, grupos.Count);

        GrupoAlbum primero = grupos[0];
        Assert.AreEqual("DC_CASO_01", primero.clave, "el caso que se empezó antes va arriba");
        Assert.AreEqual("Los Mensajes del Puma al Pudú", primero.titulo);
        Assert.AreEqual("desconocidos · 2 evidencias", primero.subtitulo);
        Assert.AreEqual(2, primero.Cantidad);
        CollectionAssert.AreEqual(new[] { "DC01_MSG03", "DC01_MSG05" },
            primero.evidencias.Select(e => e.mensajeId).ToList());

        Assert.AreEqual("El Grupo del Pantano de Flamenco", grupos[1].titulo);
        Assert.AreEqual("ciberacoso · 1 evidencia", grupos[1].subtitulo);
    }

    [UnityTest]
    public IEnumerator PorCaso_RejugarMarcandoMenos_ElGrupoConservaLasCuatroEvidencias()
    {
        yield return null;

        // Primer intento: clava las cuatro señales del caso.
        foreach (string id in new[] { "DC01_MSG03", "DC01_MSG05", "DC01_MSG07", "DC01_MSG09" })
            AlbumDeEvidencias.Registrar(Puma(id));

        // Segundo intento: vuelve a jugarlo y marca una sola. El servidor pisaría
        // mensajes_marcados con esa única marca; el álbum no.
        AlbumDeEvidencias.Registrar(Puma("DC01_MSG03"));

        Assert.AreEqual(4, AlbumDeEvidencias.PorCaso().Single().Cantidad);
    }

    [UnityTest]
    public IEnumerator PorCaso_UnEsqueletoSinTitulo_SeAgrupaBajoSuIdYNoDesaparece()
    {
        yield return null;

        AlbumDeEvidencias.SembrarIds("DC_CASO_03", new[] { "DC03_MSG04" });

        GrupoAlbum grupo = AlbumDeEvidencias.PorCaso().Single();
        Assert.AreEqual("DC_CASO_03", grupo.titulo, "sin título se enseña el id, no un hueco");
        Assert.AreEqual("1 evidencia", grupo.subtitulo, "sin zona, solo la cuenta");
    }

    // ── Por temática (CA2) ───────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator PorTematica_UnaEvidenciaConDosEtiquetas_ApareceEnLosDosGrupos()
    {
        yield return null;

        AlbumDeEvidencias.Registrar(Puma("DC01_MSG03"));
        AlbumDeEvidencias.Registrar(Flamenco("DC02_MSG03"));

        List<GrupoAlbum> grupos = AlbumDeEvidencias.PorTematica();

        CollectionAssert.AreEquivalent(
            new[] { "ciberacoso", "exclusion", "grooming", "privacidad" },
            grupos.Select(g => g.clave).ToList());

        GrupoAlbum grooming = grupos.Single(g => g.clave == "grooming");
        GrupoAlbum privacidad = grupos.Single(g => g.clave == "privacidad");
        Assert.AreEqual("DC01_MSG03", grooming.evidencias.Single().mensajeId);
        Assert.AreEqual("DC01_MSG03", privacidad.evidencias.Single().mensajeId);
    }

    [UnityTest]
    public IEnumerator PorTematica_SinEtiquetas_CaeEnSinClasificar_YNoSePierde()
    {
        yield return null;

        AlbumDeEvidencias.Registrar(Puma("DC01_MSG03"));
        AlbumDeEvidencias.SembrarIds("DC_CASO_03", new[] { "DC03_MSG04" });

        List<GrupoAlbum> grupos = AlbumDeEvidencias.PorTematica();

        Assert.AreEqual(AlbumDeEvidencias.ClaveSinTematica, grupos.Last().clave,
            "el cajón de sastre va al final");
        Assert.AreEqual("DC03_MSG04", grupos.Last().evidencias.Single().mensajeId);
    }

    // ── Reinicio ─────────────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator SeRelee_DePlayerPrefs_ConLosMetadatosIntactos()
    {
        yield return null;

        AlbumDeEvidencias.ConfigurarParaPartida(Partida);
        AlbumDeEvidencias.Registrar(Puma("DC01_MSG03"));
        AlbumDeEvidencias.Registrar(Flamenco("DC02_MSG03"));

        // Irse a otra partida y volver es lo más parecido a cerrar y reabrir el juego
        // que se puede hacer sin reiniciar el proceso: lo de memoria se descarta, así
        // que lo que vuelve sale del disco y de ningún otro lado.
        AlbumDeEvidencias.ConfigurarParaPartida(OtraPartida);
        Assert.AreEqual(0, AlbumDeEvidencias.Cantidad);

        AlbumDeEvidencias.ConfigurarParaPartida(Partida);

        Assert.AreEqual(2, AlbumDeEvidencias.Cantidad);
        EvidenciaAlbum releida = AlbumDeEvidencias.Todas().First(e => e.mensajeId == "DC01_MSG03");
        Assert.AreEqual("Los Mensajes del Puma al Pudú", releida.casoTitulo);
        Assert.AreEqual("desconocidos", releida.casoZona);
        Assert.AreEqual("Puma", releida.autor);
        Assert.AreEqual("explicación del DC01_MSG03", releida.explicacion);
        CollectionAssert.AreEqual(new[] { "grooming", "privacidad" }, releida.etiquetas);

        // Y los grupos se rehacen igual que antes de reiniciar.
        Assert.AreEqual(2, AlbumDeEvidencias.PorCaso().Count);
    }

    [UnityTest]
    public IEnumerator SeRelee_UnJsonConCamposDeMasYListasAusentes_NoRompe()
    {
        yield return null;

        // Lo que escribiría una versión futura del juego: campos que esta no conoce,
        // y una evidencia a la que le falta la lista de etiquetas.
        PlayerPrefs.SetString(ClavePrefs,
            @"{""version"":1,""otraCosaQueVendra"":42,""evidencias"":[
                {""casoId"":""DC_CASO_01"",""mensajeId"":""DC01_MSG03"",
                 ""casoTitulo"":""Los Mensajes del Puma al Pudú"",""casoZona"":""desconocidos"",
                 ""autor"":""Puma"",""texto"":""oye cuántos años tienes?"",
                 ""explicacion"":""una alerta"",""registradaEn"":100,
                 ""campoDelFuturo"":""da igual""}]}");
        PlayerPrefs.Save();

        AlbumDeEvidencias.ConfigurarParaPartida(Partida);

        EvidenciaAlbum releida = AlbumDeEvidencias.Todas().Single();
        Assert.AreEqual("Puma", releida.autor);
        Assert.IsNotNull(releida.etiquetas, "una lista ausente queda en null y hay que normalizarla");
        Assert.AreEqual(0, releida.etiquetas.Count);

        // Y la agrupación por temática no revienta con ella.
        Assert.AreEqual(AlbumDeEvidencias.ClaveSinTematica,
            AlbumDeEvidencias.PorTematica().Single().clave);
    }
}
