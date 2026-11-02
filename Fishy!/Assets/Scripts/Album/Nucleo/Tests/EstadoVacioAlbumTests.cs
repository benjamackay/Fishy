using System.Collections;
using System.Collections.Generic;
using Fishy.Album;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// HDU-12 CA4 — Qué mensaje le toca a un álbum al que le falta algo, y que pedirlo
/// con el álbum vacío no reviente.
///
/// El renderizado en sí —que el panel se arme sin excepciones con cada uno de estos
/// estados— lo comprueba <c>Fishy.EditorTools.FishyPruebasAlbum</c>, que sí puede
/// ver <c>AlbumEvidenciasUI</c>: esa clase vive en Assembly-CSharp y un .asmdef de
/// pruebas no puede referenciarlo. Acá está la regla que esa pantalla aplica.
/// </summary>
public class EstadoVacioAlbumTests
{
    private const int Partida = 991207;

    [SetUp]
    public void SetUp() => AlbumDeEvidencias.Limpiar();

    [TearDown]
    public void TearDown()
    {
        AlbumDeEvidencias.Limpiar();
        PlayerPrefs.DeleteKey($"{AlbumDeEvidencias.Prefijo}Evidencias.Partida.{Partida}");
        PlayerPrefs.Save();
    }

    private static EvidenciaAlbum Senal(string casoId = "DC_CASO_01", string mensajeId = "DC01_MSG03")
    {
        return new EvidenciaAlbum
        {
            casoId = casoId,
            mensajeId = mensajeId,
            casoTitulo = "Los Mensajes del Puma al Pudú",
            casoZona = "desconocidos",
            etiquetas = new List<string> { "grooming" },
            autor = "Puma",
            texto = "oye cuántos años tienes?",
            explicacion = "Pedir la edad a un desconocido de internet es una alerta.",
        };
    }

    // ── La regla de los tres estados ─────────────────────────────────────────

    [UnityTest]
    public IEnumerator SinPinesYSinEvidencias_ElAlbumEstaReciénEmpezado()
    {
        yield return null;

        Assert.AreEqual(EstadoVacio.AlbumEntero, EstadoVacioAlbum.Decidir(0, 0));
        Assert.IsFalse(EstadoVacioAlbum.MuestraSecciones(EstadoVacio.AlbumEntero),
            "dos títulos sobre dos huecos hacen parecer rota una pantalla que solo está vacía");
    }

    [UnityTest]
    public IEnumerator ConEvidenciasYSinPines_SoloFaltaElMosaico()
    {
        yield return null;

        // Pasa de verdad: marcar una señal de cuatro deja evidencia y no da pin.
        Assert.AreEqual(EstadoVacio.FaltanPines, EstadoVacioAlbum.Decidir(0, 1));
        Assert.IsTrue(EstadoVacioAlbum.MuestraSecciones(EstadoVacio.FaltanPines));
    }

    [UnityTest]
    public IEnumerator ConPinesYSinEvidencias_ElMensajeEsElOtro()
    {
        yield return null;

        Assert.AreEqual(EstadoVacio.FaltanEvidencias, EstadoVacioAlbum.Decidir(3, 0));
        Assert.IsTrue(EstadoVacioAlbum.MuestraSecciones(EstadoVacio.FaltanEvidencias));
    }

    [UnityTest]
    public IEnumerator ConLasDosCosas_NoFaltaNingunMensaje()
    {
        yield return null;

        Assert.AreEqual(EstadoVacio.Completo, EstadoVacioAlbum.Decidir(2, 5));
        Assert.IsTrue(EstadoVacioAlbum.MuestraSecciones(EstadoVacio.Completo));
    }

    [UnityTest]
    public IEnumerator UnaCuentaNegativa_SeTrataComoVacia()
    {
        yield return null;

        Assert.AreEqual(EstadoVacio.AlbumEntero, EstadoVacioAlbum.Decidir(-1, -1));
    }

    // ── Contra el álbum de verdad ────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ElAlbumVacio_SeDejaConsultarSinExcepciones_YDaElEstadoDeAlbumEntero()
    {
        yield return null;

        AlbumDeEvidencias.ConfigurarParaPartida(Partida);

        // Es lo que hace la pantalla al abrirse: pedir los grupos y decidir. Con el
        // álbum vacío tiene que devolver listas vacías, no null, y no reventar.
        List<GrupoAlbum> porCaso = AlbumDeEvidencias.PorCaso();
        List<GrupoAlbum> porTematica = AlbumDeEvidencias.PorTematica();

        Assert.IsNotNull(porCaso);
        Assert.IsNotNull(porTematica);
        Assert.AreEqual(0, porCaso.Count);
        Assert.AreEqual(0, porTematica.Count);

        Assert.AreEqual(EstadoVacio.AlbumEntero, EstadoVacioAlbum.Decidir(0, porCaso.Count));
    }

    [UnityTest]
    public IEnumerator ConUnaEvidencia_ElEstadoCambia_EnLasDosAgrupaciones()
    {
        yield return null;

        AlbumDeEvidencias.ConfigurarParaPartida(Partida);
        AlbumDeEvidencias.Registrar(Senal());

        Assert.AreEqual(EstadoVacio.FaltanPines,
            EstadoVacioAlbum.Decidir(0, AlbumDeEvidencias.PorCaso().Count));
        Assert.AreEqual(EstadoVacio.FaltanPines,
            EstadoVacioAlbum.Decidir(0, AlbumDeEvidencias.PorTematica().Count));
    }

    [UnityTest]
    public IEnumerator UnaEvidenciaSinEtiquetas_TampocoDejaElAlbumPareciendoVacio()
    {
        yield return null;

        AlbumDeEvidencias.ConfigurarParaPartida(Partida);

        EvidenciaAlbum suelta = Senal();
        suelta.etiquetas = new List<string>();
        AlbumDeEvidencias.Registrar(suelta);

        // Cae en "sin clasificar", que es un grupo de verdad: agrupando por temática
        // la pantalla no puede quedarse sin nada que enseñar.
        Assert.AreEqual(1, AlbumDeEvidencias.PorTematica().Count);
        Assert.AreEqual(EstadoVacio.FaltanPines,
            EstadoVacioAlbum.Decidir(0, AlbumDeEvidencias.PorTematica().Count));
    }
}
