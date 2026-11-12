using System.Collections;
using System.Collections.Generic;
using Fishy.Mision;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Pruebas PlayMode del catálogo de misiones y de su respaldo en archivo.
///
/// Lo que se prueba aquí es la parte que decide: leer los datos, quedarse con la
/// fuente correcta y fabricar la ficha. Resolver un objetivo contra la escena —buscar
/// el NPC, el ItemData— vive en <c>ObjetivoMision</c>, del lado de Assembly-CSharp,
/// que un assembly de tests con .asmdef no puede ver.
/// </summary>
public class CatalogoMisionesTests
{
    private const string JsonDosMisiones = @"{
        ""version"": ""test"",
        ""misiones"": [
            {
                ""mision_id"": ""M_SEGUNDA"",
                ""titulo"": ""La segunda"",
                ""tipo"": ""secundaria"",
                ""zona"": ""ciberacoso"",
                ""zona_objetivo"": ""zona_2"",
                ""orden"": 20,
                ""objetivos"": []
            },
            {
                ""mision_id"": ""M_PRIMERA"",
                ""titulo"": ""La primera"",
                ""tipo"": ""exploracion"",
                ""zona"": ""desconocidos"",
                ""zona_objetivo"": ""zona_1"",
                ""orden"": 10,
                ""objetivos"": [
                    { ""orden"": 2, ""tipo"": ""llegar_zona"", ""zona_id"": ""zona_2"" },
                    { ""orden"": 1, ""tipo"": ""recoger_objeto"", ""item_id"": ""ITEM_BRUJULA"", ""cantidad"": 3 }
                ]
            }
        ]
    }";

    [TearDown]
    public void TearDown()
    {
        // Dejar el catálogo como estaba: es estático y se lo llevaría a la prueba
        // siguiente. Recargar vuelve al archivo real de Resources.
        CatalogoMisiones.Recargar();
    }

    // ── Leer el archivo ──────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator LeerTexto_CargaLasMisionesYQuedaComoArchivo()
    {
        yield return null;

        CatalogoMisiones.LeerTexto(JsonDosMisiones);

        Assert.AreEqual(2, CatalogoMisiones.Todas.Count);
        Assert.AreEqual(CatalogoMisiones.Origen.Archivo, CatalogoMisiones.DeDonde);
        Assert.IsNotNull(CatalogoMisiones.Buscar("M_PRIMERA"));
    }

    [UnityTest]
    public IEnumerator EnOrden_DevuelveLaHistoriaEnOrden()
    {
        yield return null;

        CatalogoMisiones.LeerTexto(JsonDosMisiones);
        List<MisionRegistro> lista = CatalogoMisiones.EnOrden();

        // Van al revés en el archivo a propósito: manda el campo `orden`.
        CollectionAssert.AreEqual(new[] { "M_PRIMERA", "M_SEGUNDA" },
            lista.ConvertAll(m => m.mision_id));
    }

    [UnityTest]
    public IEnumerator ObjetivosEnOrden_RespetaSuOrdenYNoElDelArchivo()
    {
        yield return null;

        CatalogoMisiones.LeerTexto(JsonDosMisiones);
        List<ObjetivoRegistro> objetivos = CatalogoMisiones.Buscar("M_PRIMERA").ObjetivosEnOrden();

        Assert.AreEqual(2, objetivos.Count);
        Assert.AreEqual("recoger_objeto", objetivos[0].tipo, "El de orden 1 va primero.");
        Assert.AreEqual("llegar_zona", objetivos[1].tipo);
        Assert.AreEqual(3, objetivos[0].cantidad);
        Assert.AreEqual("ITEM_BRUJULA", objetivos[0].item_id);
    }

    [UnityTest]
    public IEnumerator JsonRoto_NoTiraElJuegoAbajo()
    {
        yield return null;

        LogAssert.ignoreFailingMessages = true;   // se espera un error en consola
        CatalogoMisiones.LeerTexto("{ esto no es json }");

        Assert.AreEqual(0, CatalogoMisiones.Todas.Count);
        Assert.AreEqual(CatalogoMisiones.Origen.Ninguno, CatalogoMisiones.DeDonde);
        LogAssert.ignoreFailingMessages = false;
    }

    // ── El archivo de verdad ─────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator ArchivoDeRespaldo_ExisteYTraeElRecorridoCompleto()
    {
        yield return null;

        CatalogoMisiones.Recargar();

        Assert.AreEqual(CatalogoMisiones.Origen.Archivo, CatalogoMisiones.DeDonde,
            "Sin Resources/misiones.json el juego se queda sin respaldo.");
        // El recorrido de narrativa2.5: 3 bienvenidas de zona + las 7 principales
        // (M1, M2, M3, M4, 5a, 5b y 6) + las 7 secundarias que lista el PDF.
        Assert.AreEqual(17, CatalogoMisiones.Todas.Count);
        Assert.AreEqual(3, Cuantas("exploracion"), "Son tres zonas, tres bienvenidas.");
        Assert.AreEqual(7, Cuantas("principal"), "El hilo del PDF son siete misiones.");
        Assert.AreEqual(7, Cuantas("secundaria"), "El PDF lista siete secundarias.");

        // Las dos de la Zona 1 son las que más veces se han ido del archivo: estuvieron
        // fundidas en una sola misión de depuración ('MISION_NPC_03'), y con ellas
        // fuera la zona del grooming no tenía ni una misión principal.
        Assert.IsNotNull(CatalogoMisiones.Buscar("MISION_M1_AMISTAD"));
        Assert.IsNotNull(CatalogoMisiones.Buscar("MISION_M2_REGALO"));
        Assert.IsNotNull(CatalogoMisiones.Buscar("MISION_SEC_MASCOTA_COIPO"));

        // La presentación de Coipo abre los testimonios: si JsonUtility no leyera el
        // campo, la misión no llegaría nunca y no habría ningún error. Lo mismo vale
        // para la cadena de la Zona 1, que es lo único que entrega el hilo principal.
        Assert.AreEqual("MISION_M3_TESTIMONIOS",
            CatalogoMisiones.Buscar("MISION_EXPLORACION_02").desbloquea_mision);
        Assert.AreEqual("MISION_M1_AMISTAD",
            CatalogoMisiones.Buscar("MISION_EXPLORACION_01").desbloquea_mision);
        Assert.AreEqual("MISION_M2_REGALO",
            CatalogoMisiones.Buscar("MISION_M1_AMISTAD").desbloquea_mision);

        // Ninguna principal la entrega un NPC del mapa: estos campos son su ÚNICA vía
        // de llegada, así que si uno se cae, la aventura se corta ahí sin un solo
        // mensaje de error. Cada bienvenida de zona abre la primera principal de su
        // zona, y de ahí en adelante van en cadena.
        Assert.AreEqual("MISION_M4_ACOSO",
            CatalogoMisiones.Buscar("MISION_M3_TESTIMONIOS").desbloquea_mision);
        Assert.AreEqual("MISION_M5A_RETO_CUEVA",
            CatalogoMisiones.Buscar("MISION_EXPLORACION_03").desbloquea_mision);
        Assert.AreEqual("MISION_M5B_SEGUNDO_RETO",
            CatalogoMisiones.Buscar("MISION_M5A_RETO_CUEVA").desbloquea_mision);
        Assert.AreEqual("MISION_M6_ULTIMO_DESAFIO",
            CatalogoMisiones.Buscar("MISION_M5B_SEGUNDO_RETO").desbloquea_mision);
    }

    /// <summary>
    /// El invariante que de verdad importa, por encima de cada enlace suelto: una
    /// misión principal a la que no apunta ningún <c>desbloquea_mision</c> no tiene
    /// forma de llegarle al jugador, porque ningún NPC de MainScene entrega el hilo.
    /// Pasó con M4, M5a, M5b y la 6 a la vez, y el juego no se queja: simplemente la
    /// aventura se queda a medias.
    /// </summary>
    [UnityTest]
    public IEnumerator ArchivoDeRespaldo_NingunaPrincipalSeQuedaSinQuienLaAbra()
    {
        yield return null;

        CatalogoMisiones.Recargar();

        var destinos = new HashSet<string>();
        foreach (MisionRegistro m in CatalogoMisiones.Todas.Values)
            if (!string.IsNullOrWhiteSpace(m.desbloquea_mision))
                destinos.Add(m.desbloquea_mision.Trim());

        foreach (MisionRegistro m in CatalogoMisiones.Todas.Values)
        {
            if (m.tipo != "principal") continue;
            Assert.IsTrue(destinos.Contains(m.mision_id),
                $"'{m.mision_id}' es principal y ninguna misión la desbloquea: " +
                "nadie puede recibirla.");
        }
    }

    /// <summary>Cuántas misiones del archivo son de ese <c>tipo</c>.</summary>
    private static int Cuantas(string tipo)
    {
        int cuenta = 0;
        foreach (MisionRegistro m in CatalogoMisiones.Todas.Values)
            if (m.tipo == tipo) cuenta++;
        return cuenta;
    }

    /// <summary>
    /// Las secundarias van por encima del 100 a propósito. <c>MissionManager.Activa</c>
    /// es siempre la disponible de menor <c>orden</c>, así que con un recado de la
    /// Zona 1 en el 20 el cartel de misión tapaba el hilo principal entero.
    /// </summary>
    [UnityTest]
    public IEnumerator ArchivoDeRespaldo_LasSecundariasVanDetrasDelHilo()
    {
        yield return null;

        CatalogoMisiones.Recargar();

        int ultimaDelHilo = 0;
        int primeraSecundaria = int.MaxValue;
        foreach (MisionRegistro m in CatalogoMisiones.Todas.Values)
        {
            if (m.tipo == "secundaria") primeraSecundaria = Mathf.Min(primeraSecundaria, m.orden);
            else ultimaDelHilo = Mathf.Max(ultimaDelHilo, m.orden);
        }

        Assert.Less(ultimaDelHilo, primeraSecundaria,
            $"Una secundaria (orden {primeraSecundaria}) se cuela antes del final del " +
            $"hilo (orden {ultimaDelHilo}) y se queda fija en el cartel de misión.");
    }

    [UnityTest]
    public IEnumerator ArchivoDeRespaldo_TodasTraenTituloYOrdenPropio()
    {
        yield return null;

        CatalogoMisiones.Recargar();

        var ordenes = new HashSet<int>();
        foreach (MisionRegistro m in CatalogoMisiones.Todas.Values)
        {
            Assert.IsNotEmpty(m.Titulo, $"'{m.mision_id}' no tiene título.");
            Assert.AreNotEqual(m.mision_id, m.Titulo,
                $"'{m.mision_id}' está mostrando su id como título.");
            Assert.IsTrue(ordenes.Add(m.orden),
                $"El orden {m.orden} está repetido: la misión activa quedaría al azar.");
        }
    }

    // ── Que mande la base ────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator AplicarDesdeBase_Manda_PeroElArchivoTapaHuecos()
    {
        yield return null;

        CatalogoMisiones.LeerTexto(JsonDosMisiones);

        LogAssert.ignoreFailingMessages = true;   // avisa que la base venía incompleta
        CatalogoMisiones.AplicarDesdeBase(new List<MisionRegistro>
        {
            new MisionRegistro { mision_id = "M_DE_LA_BASE", titulo = "Traída de la base", orden = 5 },
        });
        LogAssert.ignoreFailingMessages = false;

        Assert.AreEqual(CatalogoMisiones.Origen.Base, CatalogoMisiones.DeDonde);
        Assert.AreEqual("Traída de la base", CatalogoMisiones.Buscar("M_DE_LA_BASE").titulo);

        // Una base incompleta (falta correr cargar_banco) no puede dejar al juego sin
        // las misiones del archivo.
        Assert.IsNotNull(CatalogoMisiones.Buscar("M_PRIMERA"));
        Assert.AreEqual(3, CatalogoMisiones.Todas.Count);
    }

    [UnityTest]
    public IEnumerator AplicarDesdeBase_ElTituloDeLaBaseGanaAlDelArchivo()
    {
        yield return null;

        CatalogoMisiones.LeerTexto(JsonDosMisiones);

        LogAssert.ignoreFailingMessages = true;
        CatalogoMisiones.AplicarDesdeBase(new List<MisionRegistro>
        {
            new MisionRegistro { mision_id = "M_PRIMERA", titulo = "Título de la base" },
        });
        LogAssert.ignoreFailingMessages = false;

        Assert.AreEqual("Título de la base", CatalogoMisiones.Buscar("M_PRIMERA").titulo);
    }

    [UnityTest]
    public IEnumerator AplicarDesdeBase_VaciaConservaElArchivo()
    {
        yield return null;

        CatalogoMisiones.LeerTexto(JsonDosMisiones);

        // Una respuesta vacía es casi siempre un problema del servidor, no un juego
        // que de verdad se quedó sin misiones. Quedarse sin nada sería peor.
        LogAssert.ignoreFailingMessages = true;
        CatalogoMisiones.AplicarDesdeBase(new List<MisionRegistro>());
        LogAssert.ignoreFailingMessages = false;

        Assert.AreEqual(CatalogoMisiones.Origen.Archivo, CatalogoMisiones.DeDonde);
        Assert.AreEqual(2, CatalogoMisiones.Todas.Count);
    }

    [UnityTest]
    public IEnumerator AplicarDesdeBase_NullConservaElArchivo()
    {
        yield return null;

        CatalogoMisiones.LeerTexto(JsonDosMisiones);

        LogAssert.ignoreFailingMessages = true;
        CatalogoMisiones.AplicarDesdeBase(null);
        LogAssert.ignoreFailingMessages = false;

        Assert.AreEqual(2, CatalogoMisiones.Todas.Count);
    }

    [UnityTest]
    public IEnumerator AplicarDesdeBase_Avisa()
    {
        yield return null;

        CatalogoMisiones.LeerTexto(JsonDosMisiones);

        bool avisado = false;
        void Escuchar() => avisado = true;
        CatalogoMisiones.OnCatalogoCambiado += Escuchar;

        CatalogoMisiones.AplicarDesdeBase(new List<MisionRegistro>
        {
            new MisionRegistro { mision_id = "M_DE_LA_BASE", titulo = "Traída de la base" },
        });

        CatalogoMisiones.OnCatalogoCambiado -= Escuchar;
        Assert.IsTrue(avisado, "Los MissionGiver se enganchan a esto para re-resolverse.");
    }

    // ── El campo `nombre` de la tabla que ya existe ───────────────────────────

    [UnityTest]
    public IEnumerator Titulo_AceptaElCampoNombreDeLaTabla()
    {
        yield return null;

        // La tabla `Mision` del backend llama `nombre` a lo que aquí es `titulo`.
        var registro = new MisionRegistro { mision_id = "M_X", nombre = "El rumor del pantano" };

        Assert.AreEqual("El rumor del pantano", registro.Titulo);
    }

    [UnityTest]
    public IEnumerator Titulo_SinNadaCaeAlId()
    {
        yield return null;

        var registro = new MisionRegistro { mision_id = "M_X" };

        Assert.AreEqual("M_X", registro.Titulo);
    }

    // ── Fabricar la ficha ────────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Ficha_SeFabricaConLosDatosDelCatalogo()
    {
        yield return null;

        CatalogoMisiones.LeerTexto(JsonDosMisiones);
        DesafioData ficha = CatalogoMisiones.Ficha("M_PRIMERA");

        Assert.IsNotNull(ficha);
        Assert.AreEqual("M_PRIMERA", ficha.desafioId);
        Assert.AreEqual("La primera", ficha.titulo);
        Assert.AreEqual("zona_1", ficha.zonaObjetivo);
        Assert.AreEqual(10, ficha.orden);
    }

    [UnityTest]
    public IEnumerator Ficha_LaMismaMisionDevuelveLaMismaFicha()
    {
        yield return null;

        CatalogoMisiones.LeerTexto(JsonDosMisiones);

        // Dos fichas distintas para el mismo id harían que el panel y el progreso
        // hablaran de objetos diferentes.
        Assert.AreSame(CatalogoMisiones.Ficha("M_PRIMERA"), CatalogoMisiones.Ficha("M_PRIMERA"));
    }

    [UnityTest]
    public IEnumerator Ficha_DeUnaMisionQueNoEstaEsNull()
    {
        yield return null;

        CatalogoMisiones.LeerTexto(JsonDosMisiones);

        Assert.IsNull(CatalogoMisiones.Ficha("M_QUE_NO_EXISTE"));
    }

    [UnityTest]
    public IEnumerator Ficha_QuedaBuscableEnCatalogoDesafios()
    {
        yield return null;

        CatalogoMisiones.LeerTexto(JsonDosMisiones);
        CatalogoMisiones.Ficha("M_PRIMERA");

        // Es lo que necesita MissionManager.PrecargarConocidos para repintar el panel
        // al retomar la partida: del backend vuelve un id de texto y hace falta la ficha.
        Assert.IsNotNull(CatalogoDesafios.Buscar("M_PRIMERA"));
    }

    [UnityTest]
    public IEnumerator Ficha_UsableEnMissionManagerDePuntaAPunta()
    {
        yield return null;

        if (MissionManager.Instance != null)
            Object.DestroyImmediate(MissionManager.Instance.gameObject);

        var go = new GameObject("MissionManagerTest");
        MissionManager manager = go.AddComponent<MissionManager>();
        manager.persistirLocalmente = false;

        CatalogoMisiones.LeerTexto(JsonDosMisiones);

        manager.RegistrarDesafioDisponible(CatalogoMisiones.Ficha("M_SEGUNDA"));
        manager.RegistrarDesafioDisponible(CatalogoMisiones.Ficha("M_PRIMERA"));

        // El `orden` del catálogo tiene que llegar hasta la elección de misión activa.
        Assert.AreEqual("M_PRIMERA", manager.Activa.Id);
        Assert.AreEqual("zona_1", manager.ZonaObjetivoActiva);

        Object.DestroyImmediate(go);
    }
}
