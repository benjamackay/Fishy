using System.Collections;
using System.Collections.Generic;
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

    /// <summary>
    /// Una misión que vive SOLO en el catálogo, sin ningún asset en
    /// <c>Resources/Misiones/</c>. Es el caso normal: el catálogo real trae 12 misiones
    /// y en esa carpeta hay 3 assets, con cero solapamiento entre las dos listas.
    /// </summary>
    private const string IdSoloEnCatalogo = "M_SOLO_EN_CATALOGO";

    private const string JsonDelCatalogo = @"{
        ""version"": ""test"",
        ""misiones"": [
            {
                ""mision_id"": ""M_SOLO_EN_CATALOGO"",
                ""titulo"": ""Una del catálogo"",
                ""tipo"": ""exploracion"",
                ""zona"": ""desconocidos"",
                ""zona_objetivo"": ""zona_1"",
                ""orden"": 10,
                ""objetivos"": []
            }
        ]
    }";

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

        // CatalogoDesafios y CatalogoMisiones son estáticos y viven todo el proceso: sin
        // esto, la ficha que fabrique una prueba se la encuentra hecha la siguiente y la
        // prueba de la restauración pasaría por el motivo equivocado.
        CatalogoDesafios.Olvidar(IdSoloEnCatalogo);
        CatalogoMisiones.Recargar();
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

    // ── Restaurar el panel al retomar la partida ─────────────────────────────

    [UnityTest]
    public IEnumerator PrecargarConocidos_RestauraUnaMisionQueSoloEstaEnElCatalogo()
    {
        yield return null;

        CatalogoMisiones.LeerTexto(JsonDelCatalogo);

        // Nadie llamó a Ficha() todavía, y ese es justo el estado al retomar: el id llega
        // del backend sin haber pasado por ningún NPC ni disparador. Si esta afirmación
        // fallara, la prueba estaría midiendo otra cosa.
        Assert.IsNull(CatalogoDesafios.Buscar(IdSoloEnCatalogo),
            "La ficha no debería existir todavía: es lo que hace real a esta prueba.");

        manager.PrecargarConocidos(new[] { IdSoloEnCatalogo });

        Assert.IsTrue(manager.EstaDisponible(IdSoloEnCatalogo),
            "La misión volvió del backend pero el panel quedó vacío.");
    }

    [UnityTest]
    public IEnumerator PrecargarConocidos_UnaMisionCompletadaVuelveComoCompletada()
    {
        yield return null;

        CatalogoMisiones.LeerTexto(JsonDelCatalogo);

        // El orden real de MisionBackendSync.AplicarMisiones: primero las completadas.
        manager.PrecargarCompletados(new[] { IdSoloEnCatalogo });
        manager.PrecargarConocidos(new[] { IdSoloEnCatalogo });

        Assert.IsTrue(manager.EstaCompletado(IdSoloEnCatalogo),
            "Una misión ya terminada reapareció como pendiente.");
    }

    [UnityTest]
    public IEnumerator PrecargarConocidos_SiElCatalogoLlegaDespues_LaMisionSeRecupera()
    {
        yield return null;

        // El catálogo y el progreso se bajan por separado y son independientes, así que
        // el progreso puede llegar primero. Antes eso costaba la misión: se avisaba y se
        // descartaba, y no volvía hasta la sesión siguiente.
        CatalogoMisiones.LeerTexto(@"{ ""version"": ""vacio"", ""misiones"": [] }");

        manager.PrecargarConocidos(new[] { IdSoloEnCatalogo });
        Assert.IsFalse(manager.EstaDisponible(IdSoloEnCatalogo),
            "Sin catálogo no hay título ni orden: todavía no se puede pintar.");

        CatalogoMisiones.AplicarDesdeBase(new List<MisionRegistro>
        {
            new MisionRegistro
            {
                mision_id = IdSoloEnCatalogo,
                titulo = "Una del catálogo",
                zona_objetivo = "zona_1",
                orden = 10,
            }
        });

        Assert.IsTrue(manager.EstaDisponible(IdSoloEnCatalogo),
            "Al llegar el catálogo, la misión pendiente tenía que entrar sola al panel.");
    }
}
