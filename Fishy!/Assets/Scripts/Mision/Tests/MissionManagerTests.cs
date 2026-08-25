using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Fishy.Mision;

/// <summary>
/// HDU-1 — Pruebas PlayMode del MissionManager.
///
/// Requiere que este archivo viva dentro de una carpeta de pruebas con su propio
/// Assembly Definition (.asmdef) referenciando "UnityEngine.TestRunner" y
/// "UnityEditor.TestRunner" (Tests → PlayMode), como indica la sección de
/// Testing y UX de la HDU. Si el proyecto aún no tiene ese asmdef, crear uno en
/// esta carpeta desde Assets → Create → Testing → Assembly Definition, marcado
/// "Test Assemblies".
/// </summary>
public class MissionManagerTests
{
    private MissionManager manager;
    private DesafioData desafio;

    [SetUp]
    public void SetUp()
    {
        // Limpia cualquier instancia previa (los tests corren en el mismo proceso).
        if (MissionManager.Instance != null)
            Object.DestroyImmediate(MissionManager.Instance.gameObject);

        var go = new GameObject("MissionManagerTest");
        manager = go.AddComponent<MissionManager>();
        manager.persistirLocalmente = false; // aislar de PlayerPrefs entre corridas

        desafio = ScriptableObject.CreateInstance<DesafioData>();
        desafio.desafioId = "TEST_DESAFIO_01";
        desafio.titulo = "Desafío de prueba";
    }

    [TearDown]
    public void TearDown()
    {
        if (manager != null) Object.DestroyImmediate(manager.gameObject);
        if (desafio != null) Object.DestroyImmediate(desafio);
    }

    [UnityTest]
    public IEnumerator RegistrarDesafioDisponible_QuedaComoDisponible()
    {
        yield return null; // deja correr Awake()

        var runtime = manager.RegistrarDesafioDisponible(desafio);

        Assert.IsNotNull(runtime);
        Assert.AreEqual(EstadoDesafio.Disponible, runtime.estado);
        Assert.IsTrue(manager.EstaDisponible(desafio.desafioId));
    }

    [UnityTest]
    public IEnumerator RegistrarDesafioDisponible_DisparaEvento()
    {
        yield return null;

        bool eventoDisparado = false;
        manager.onDesafioDisponible.AddListener(r => eventoDisparado = true);

        manager.RegistrarDesafioDisponible(desafio);

        Assert.IsTrue(eventoDisparado);
    }

    [UnityTest]
    public IEnumerator CompletarDesafio_QuedaComoCompletado()
    {
        yield return null;

        manager.RegistrarDesafioDisponible(desafio);
        bool resultado = manager.CompletarDesafio(desafio.desafioId);

        Assert.IsTrue(resultado);
        Assert.IsTrue(manager.EstaCompletado(desafio.desafioId));
        Assert.IsFalse(manager.EstaDisponible(desafio.desafioId));
    }

    [UnityTest]
    public IEnumerator CompletarDesafio_SinRegistrarPrevio_DevuelveFalse()
    {
        yield return null;

        bool resultado = manager.CompletarDesafio("DESAFIO_INEXISTENTE");

        Assert.IsFalse(resultado);
    }

    [UnityTest]
    public IEnumerator CompletarDesafio_DosVeces_EsIdempotente()
    {
        yield return null;

        manager.RegistrarDesafioDisponible(desafio);
        manager.CompletarDesafio(desafio.desafioId);

        int llamadasEvento = 0;
        manager.onDesafioCompletado.AddListener(r => llamadasEvento++);

        bool segundaVez = manager.CompletarDesafio(desafio.desafioId);

        Assert.IsTrue(segundaVez);
        Assert.AreEqual(0, llamadasEvento); // no debe volver a disparar el evento
    }
}
