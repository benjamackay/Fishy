using System;
using System.Collections.Generic;
using Fishy.Mision;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Pruebas del guion de un NPC: qué diálogo le toca decir y qué misión entrega, según
/// cómo vayan las misiones.
///
/// Son <c>[Test]</c> y no <c>[UnityTest]</c> porque aquí no hace falta ni un frame ni
/// un singleton: <c>GuionDelNpc.Resolver</c> recibe el estado de las misiones como
/// función, que es justo lo que permite probar el orden de prioridades sin montar una
/// escena ni una partida.
/// </summary>
public class GuionDelNpcTests
{
    private const string Exploracion = "MISION_EXPLORACION_01";
    private const string Secundaria  = "MISION_SEC_01";
    private const string Tercera     = "MISION_SEC_03";

    private const string DiceHola       = "HUEMUL_ENTREGA_EXPLORACION";
    private const string DiceRecuerda   = "HUEMUL_RECUERDA_EXPLORACION";
    private const string DiceSecundaria = "HUEMUL_ENTREGA_SEC_01";
    private const string DiceTercera    = "HUEMUL_ENTREGA_SEC_03";
    private const string DiceAdios      = "HUEMUL_DESPEDIDA";

    // ── Andamios ─────────────────────────────────────────────────────────────

    /// <summary>El estado del mundo: lo que no esté en el diccionario está sin
    /// entregar, que es lo que devuelve <c>MissionManager.GetEstado</c>.</summary>
    private static Func<string, EstadoDesafio?> Mundo(
        params (string id, EstadoDesafio estado)[] conocidas)
    {
        var estados = new Dictionary<string, EstadoDesafio>();
        foreach ((string id, EstadoDesafio estado) in conocidas) estados[id] = estado;

        return id => estados.TryGetValue(id, out EstadoDesafio e)
            ? (EstadoDesafio?)e
            : null;
    }

    private static PasoDelGuion Paso(string misionId, string entrega, string mientras = "",
        params string[] puertas)
    {
        return new PasoDelGuion
        {
            misionId = misionId,
            dialogoDeEntrega = entrega,
            dialogoMientras = mientras,
            requiereCompletadas = new List<string>(puertas),
        };
    }

    /// <summary>La cadena de Huemul del caso real: exploración, secundaria, tercera.</summary>
    private static List<PasoDelGuion> CadenaDeHuemul()
    {
        return new List<PasoDelGuion>
        {
            Paso(Exploracion, DiceHola, DiceRecuerda),
            Paso(Secundaria, DiceSecundaria),
            Paso(Tercera, DiceTercera),
        };
    }

    // ── La cadena ────────────────────────────────────────────────────────────

    [Test]
    public void NadaEntregadoTodavia_EntregaElPrimerPaso()
    {
        LoQueDiceElNpc dice = GuionDelNpc.Resolver(CadenaDeHuemul(), Mundo());

        Assert.AreEqual(QueDiceElNpc.Entregar, dice.Que);
        Assert.AreEqual(DiceHola, dice.DialogoId);
        Assert.AreEqual(Exploracion, dice.MisionAEntregar);
        Assert.AreEqual(0, dice.Paso);
    }

    [Test]
    public void ElPrimerPasoEnCurso_RecuerdaYNoEntregaNada()
    {
        LoQueDiceElNpc dice = GuionDelNpc.Resolver(CadenaDeHuemul(),
            Mundo((Exploracion, EstadoDesafio.Disponible)));

        Assert.AreEqual(QueDiceElNpc.Recordar, dice.Que);
        Assert.AreEqual(DiceRecuerda, dice.DialogoId);
        Assert.IsEmpty(dice.MisionAEntregar);
    }

    [Test]
    public void CompletadoElPrimero_EntregaElSegundo()
    {
        LoQueDiceElNpc dice = GuionDelNpc.Resolver(CadenaDeHuemul(),
            Mundo((Exploracion, EstadoDesafio.Completado)));

        Assert.AreEqual(QueDiceElNpc.Entregar, dice.Que);
        Assert.AreEqual(DiceSecundaria, dice.DialogoId);
        Assert.AreEqual(Secundaria, dice.MisionAEntregar);
        Assert.AreEqual(1, dice.Paso);
    }

    [Test]
    public void CompletadosLosDosPrimeros_EntregaElTercero()
    {
        LoQueDiceElNpc dice = GuionDelNpc.Resolver(CadenaDeHuemul(),
            Mundo((Exploracion, EstadoDesafio.Completado),
                  (Secundaria,  EstadoDesafio.Completado)));

        Assert.AreEqual(QueDiceElNpc.Entregar, dice.Que);
        Assert.AreEqual(Tercera, dice.MisionAEntregar);
    }

    [Test]
    public void CadenaAgotada_SeDespide()
    {
        LoQueDiceElNpc dice = GuionDelNpc.Resolver(CadenaDeHuemul(),
            Mundo((Exploracion, EstadoDesafio.Completado),
                  (Secundaria,  EstadoDesafio.Completado),
                  (Tercera,     EstadoDesafio.Completado)),
            dialogoAlFinal: DiceAdios);

        Assert.AreEqual(QueDiceElNpc.Despedirse, dice.Que);
        Assert.AreEqual(DiceAdios, dice.DialogoId);
        Assert.AreEqual(-1, dice.Paso);
    }

    /// <summary>
    /// Con un paso suyo todavía por hacer, el NPC no se despide aunque tenga
    /// despedida escrita: sonaría a que ya terminó algo que no ha terminado. Este es
    /// el caso de un paso en curso al que nadie le escribió el recordatorio.
    /// </summary>
    [Test]
    public void PasoEnCursoSinRecordatorio_NoSeDespide()
    {
        var cadena = new List<PasoDelGuion> { Paso(Exploracion, DiceHola) };

        LoQueDiceElNpc dice = GuionDelNpc.Resolver(cadena,
            Mundo((Exploracion, EstadoDesafio.Disponible)),
            dialogoAlFinal: DiceAdios);

        Assert.AreEqual(QueDiceElNpc.Nada, dice.Que);
    }

    // ── El orden de prioridades ──────────────────────────────────────────────

    /// <summary>Tener algo nuevo que dar gana a comentar lo que el niño/a trae entre
    /// manos: es lo que mueve la partida.</summary>
    [Test]
    public void TeniendoAlgoQueEntregar_NoComentaLaMisionActiva()
    {
        var reglas = new List<ReglaPorMisionActiva>
        {
            new ReglaPorMisionActiva { misionId = Secundaria, dialogo = "HUEMUL_COMENTA" },
        };

        LoQueDiceElNpc dice = GuionDelNpc.Resolver(CadenaDeHuemul(), Mundo(),
            misionActivaId: Secundaria, reglasPorMisionActiva: reglas);

        Assert.AreEqual(QueDiceElNpc.Entregar, dice.Que);
        Assert.AreEqual(DiceHola, dice.DialogoId);
    }

    [Test]
    public void SinNadaQueEntregar_LaMisionActivaGanaAlRecordatorio()
    {
        var reglas = new List<ReglaPorMisionActiva>
        {
            new ReglaPorMisionActiva { misionId = Tercera, dialogo = "HUEMUL_COMENTA_TERCERA" },
        };

        LoQueDiceElNpc dice = GuionDelNpc.Resolver(CadenaDeHuemul(),
            Mundo((Exploracion, EstadoDesafio.Disponible)),
            misionActivaId: Tercera, reglasPorMisionActiva: reglas);

        Assert.AreEqual(QueDiceElNpc.Comentar, dice.Que);
        Assert.AreEqual("HUEMUL_COMENTA_TERCERA", dice.DialogoId);
    }

    [Test]
    public void CadenaAgotada_LaMisionActivaGanaALaDespedida()
    {
        var reglas = new List<ReglaPorMisionActiva>
        {
            new ReglaPorMisionActiva { misionId = Tercera, dialogo = "HUEMUL_COMENTA_TERCERA" },
        };

        LoQueDiceElNpc dice = GuionDelNpc.Resolver(CadenaDeHuemul(),
            Mundo((Exploracion, EstadoDesafio.Completado),
                  (Secundaria,  EstadoDesafio.Completado),
                  (Tercera,     EstadoDesafio.Completado)),
            misionActivaId: Tercera, reglasPorMisionActiva: reglas,
            dialogoAlFinal: DiceAdios);

        Assert.AreEqual(QueDiceElNpc.Comentar, dice.Que);
    }

    [Test]
    public void UnaReglaQueNoEsLaMisionActiva_NoSeUsa()
    {
        var reglas = new List<ReglaPorMisionActiva>
        {
            new ReglaPorMisionActiva { misionId = "OTRA_CUALQUIERA", dialogo = "NO_TOCA" },
        };

        LoQueDiceElNpc dice = GuionDelNpc.Resolver(CadenaDeHuemul(),
            Mundo((Exploracion, EstadoDesafio.Disponible)),
            misionActivaId: Tercera, reglasPorMisionActiva: reglas);

        Assert.AreEqual(QueDiceElNpc.Recordar, dice.Que);
        Assert.AreEqual(DiceRecuerda, dice.DialogoId);
    }

    [Test]
    public void VariasReglasParaLaMisma_GanaLaPrimera()
    {
        var reglas = new List<ReglaPorMisionActiva>
        {
            new ReglaPorMisionActiva { misionId = Tercera, dialogo = "LA_DE_ARRIBA" },
            new ReglaPorMisionActiva { misionId = Tercera, dialogo = "LA_DE_ABAJO" },
        };

        LoQueDiceElNpc dice = GuionDelNpc.Resolver(CadenaDeHuemul(),
            Mundo((Exploracion, EstadoDesafio.Disponible)),
            misionActivaId: Tercera, reglasPorMisionActiva: reglas);

        Assert.AreEqual("LA_DE_ARRIBA", dice.DialogoId);
    }

    // ── Las puertas de fuera de la cadena ────────────────────────────────────

    /// <summary>
    /// Con una puerta sin abrir el NPC se queda esperando en ese paso: NO salta al
    /// siguiente, que entregaría la cadena en desorden.
    /// </summary>
    [Test]
    public void PuertaSinCumplir_NoEntregaNiSaltaDePaso()
    {
        var cadena = new List<PasoDelGuion>
        {
            Paso(Secundaria, DiceSecundaria, "HUEMUL_ESPERA", "MISION_DE_COIPO"),
            Paso(Tercera, DiceTercera),
        };

        LoQueDiceElNpc dice = GuionDelNpc.Resolver(cadena, Mundo());

        Assert.AreEqual(QueDiceElNpc.Recordar, dice.Que);
        Assert.AreEqual("HUEMUL_ESPERA", dice.DialogoId);
        Assert.IsEmpty(dice.MisionAEntregar);
        Assert.AreEqual(0, dice.Paso);
    }

    [Test]
    public void PuertaCumplida_YaEntregaEsePaso()
    {
        var cadena = new List<PasoDelGuion>
        {
            Paso(Secundaria, DiceSecundaria, "HUEMUL_ESPERA", "MISION_DE_COIPO"),
        };

        LoQueDiceElNpc dice = GuionDelNpc.Resolver(cadena,
            Mundo(("MISION_DE_COIPO", EstadoDesafio.Completado)));

        Assert.AreEqual(QueDiceElNpc.Entregar, dice.Que);
        Assert.AreEqual(Secundaria, dice.MisionAEntregar);
    }

    // ── Configuraciones a medio hacer ────────────────────────────────────────

    [Test]
    public void UnPasoSinMision_SeSaltaYSigueLaCadena()
    {
        var cadena = new List<PasoDelGuion>
        {
            Paso("", "HUECO_SIN_MISION"),
            Paso(Exploracion, DiceHola),
        };

        LoQueDiceElNpc dice = GuionDelNpc.Resolver(cadena, Mundo());

        Assert.AreEqual(QueDiceElNpc.Entregar, dice.Que);
        Assert.AreEqual(Exploracion, dice.MisionAEntregar);
        Assert.AreEqual(1, dice.Paso);
    }

    [Test]
    public void SinPasosNiDespedida_NoDiceNada()
    {
        LoQueDiceElNpc dice = GuionDelNpc.Resolver(new List<PasoDelGuion>(), Mundo());

        Assert.AreEqual(QueDiceElNpc.Nada, dice.Que);
        Assert.IsEmpty(dice.DialogoId);
    }

    [Test]
    public void SinPasosPeroConDespedida_SeDespide()
    {
        LoQueDiceElNpc dice = GuionDelNpc.Resolver(null, Mundo(), dialogoAlFinal: DiceAdios);

        Assert.AreEqual(QueDiceElNpc.Despedirse, dice.Que);
    }

    /// <summary>Sin forma de preguntar el estado no se inventa nada: el componente
    /// deja al NPC con el diálogo que tuviera en la escena.</summary>
    [Test]
    public void SinSaberElEstado_NoDiceNada()
    {
        LoQueDiceElNpc dice = GuionDelNpc.Resolver(CadenaDeHuemul(), null,
            dialogoAlFinal: DiceAdios);

        Assert.AreEqual(QueDiceElNpc.Nada, dice.Que);
    }

    /// <summary>La ficha arrastrada manda sobre el id escrito, igual que en
    /// <c>PresenciaSegunMision</c>.</summary>
    [Test]
    public void ConFichaYConId_MandaLaFicha()
    {
        var ficha = ScriptableObject.CreateInstance<DesafioData>();
        ficha.desafioId = "ID_DE_LA_FICHA";

        try
        {
            var cadena = new List<PasoDelGuion>
            {
                new PasoDelGuion
                {
                    ficha = ficha,
                    misionId = "ID_ESCRITO_A_MANO",
                    dialogoDeEntrega = DiceHola,
                },
            };

            LoQueDiceElNpc dice = GuionDelNpc.Resolver(cadena, Mundo());

            Assert.AreEqual("ID_DE_LA_FICHA", dice.MisionAEntregar);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ficha);
        }
    }
}
