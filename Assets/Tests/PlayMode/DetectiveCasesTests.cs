using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Fishy;

// Tarea 3 — Validar coherencia (HDU10 CA3, CA4, CA5, CA6)
// Window > General > Test Runner > PlayMode
public class DetectiveCasesTests
{
    private static DetectiveCaseManager CreateManager()
    {
        var go = new GameObject("DCM");
        return go.AddComponent<DetectiveCaseManager>();
    }

    private static DetectiveCaseManager.DetectiveCase BuildCase(
        List<DetectiveCaseManager.DetectiveMessage> msgs)
    {
        return new DetectiveCaseManager.DetectiveCase
        {
            titulo               = "Test",
            zona                 = "desconocidos",
            permissionPlayerText = "¿Puedo revisar los mensajes?",
            permissionNpcNombre  = "Sofía",
            permissionNpcResponse = "Sí, por favor ayúdame.",
            conversacion         = msgs,
            explicaciones        = new Dictionary<string, string>()
        };
    }

    // ── CA1 — flujo de permiso ────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator DetectiveCase_RequestPermission_DispatchesPlayerText()
    {
        var dcm = CreateManager();
        string received = null;
        dcm.OnPermissionRequested += txt => received = txt;

        yield return null;
        dcm.RequestPermission(BuildCase(new List<DetectiveCaseManager.DetectiveMessage>()));

        Assert.AreEqual("¿Puedo revisar los mensajes?", received);
        Assert.IsFalse(dcm.IsPermissionGranted);

        Object.DestroyImmediate(dcm.gameObject);
    }

    [UnityTest]
    public IEnumerator DetectiveCase_GrantPermission_DispatchesNpcResponse()
    {
        var dcm = CreateManager();
        string npcName     = null;
        string npcResponse = null;
        dcm.OnPermissionGranted += (n, r) => { npcName = n; npcResponse = r; };

        yield return null;
        dcm.RequestPermission(BuildCase(new List<DetectiveCaseManager.DetectiveMessage>()));
        dcm.GrantPermission();

        Assert.IsTrue(dcm.IsPermissionGranted);
        Assert.AreEqual("Sofía", npcName);
        StringAssert.Contains("ayúdame", npcResponse);

        Object.DestroyImmediate(dcm.gameObject);
    }

    // ── CA2 — reproducción mensaje a mensaje (jugador no responde) ────────────

    [UnityTest]
    public IEnumerator DetectiveCase_AdvanceMessage_MuestraMensajesEnOrden()
    {
        var msgs = new List<DetectiveCaseManager.DetectiveMessage>
        {
            new() { id = "M1", npcSender = "Alex",  texto = "hola", esSenalRiesgo = false, esAmbiguo = false },
            new() { id = "M2", npcSender = "Sofía", texto = "hola!",esSenalRiesgo = false, esAmbiguo = false }
        };
        var dcm = CreateManager();
        var displayed = new List<string>();
        dcm.OnMessageDisplayed += m => displayed.Add(m.id);

        yield return null;
        dcm.RequestPermission(BuildCase(msgs));
        dcm.GrantPermission();
        dcm.AdvanceMessage();
        dcm.AdvanceMessage();

        Assert.AreEqual(new List<string> { "M1", "M2" }, displayed);
        Object.DestroyImmediate(dcm.gameObject);
    }

    [UnityTest]
    public IEnumerator DetectiveCase_AdvanceSinPermiso_NoMuestraNada()
    {
        var msgs = new List<DetectiveCaseManager.DetectiveMessage>
        {
            new() { id = "M1", npcSender = "Alex", texto = "hola", esSenalRiesgo = false, esAmbiguo = false }
        };
        var dcm = CreateManager();
        int count = 0;
        dcm.OnMessageDisplayed += _ => count++;

        yield return null;
        dcm.RequestPermission(BuildCase(msgs));
        // sin GrantPermission
        dcm.AdvanceMessage();

        Assert.AreEqual(0, count);
        Object.DestroyImmediate(dcm.gameObject);
    }

    // ── CA3 — el jugador marca mensajes como sospechosos ─────────────────────

    [UnityTest]
    public IEnumerator DetectiveCase_MarkCurrentSuspicious_RegistraLaMarca()
    {
        var msgs = new List<DetectiveCaseManager.DetectiveMessage>
        {
            new() { id = "M1", npcSender = "Alex", texto = "cuántos años tienes?", esSenalRiesgo = true, esAmbiguo = false }
        };
        var dcm = CreateManager();
        string markedId = null;
        dcm.OnMessageMarked += id => markedId = id;

        yield return null;
        dcm.RequestPermission(BuildCase(msgs));
        dcm.GrantPermission();
        dcm.AdvanceMessage();           // currentMessage = M1
        dcm.MarkCurrentSuspicious();

        Assert.AreEqual("M1", markedId);

        var result = dcm.EvaluateCase();
        Assert.AreEqual(1, result.aciertos);

        Object.DestroyImmediate(dcm.gameObject);
    }

    // ── CA5 — mensajes ambiguos no se cuentan como error ─────────────────────

    [UnityTest]
    public IEnumerator DetectiveCase_AmbiguoNoMarcado_NoCuentaComoError()
    {
        var msgs = new List<DetectiveCaseManager.DetectiveMessage>
        {
            new() { id = "M_RIESGO",  npcSender = "Alex", texto = "di tu colegio", esSenalRiesgo = true,  esAmbiguo = false },
            new() { id = "M_AMBIGUO", npcSender = "Alex", texto = "eres la mejor", esSenalRiesgo = false, esAmbiguo = true  }
        };
        var dcm = CreateManager();

        yield return null;
        dcm.RequestPermission(BuildCase(msgs));
        dcm.GrantPermission();
        dcm.AdvanceMessage(); // M_RIESGO
        dcm.MarkCurrentSuspicious();
        dcm.AdvanceMessage(); // M_AMBIGUO — jugador NO lo marca

        var result = dcm.EvaluateCase();

        // El ambiguo no marcado NO reduce el puntaje
        Assert.AreEqual(1, result.totalSenales);   // solo cuenta M_RIESGO
        Assert.AreEqual(1, result.aciertos);
        Assert.IsFalse(result.belowThreshold);

        Object.DestroyImmediate(dcm.gameObject);
    }

    // ── CA4 — resultado muestra aciertos y no identificados ──────────────────

    [UnityTest]
    public IEnumerator DetectiveCase_Evaluate_CuentaNoIdentificadas()
    {
        var msgs = new List<DetectiveCaseManager.DetectiveMessage>
        {
            new() { id = "R1", npcSender = "Alex", texto = "di tu dirección", esSenalRiesgo = true,  esAmbiguo = false },
            new() { id = "R2", npcSender = "Alex", texto = "juntémonos solos", esSenalRiesgo = true,  esAmbiguo = false },
            new() { id = "N1", npcSender = "Alex", texto = "hola",             esSenalRiesgo = false, esAmbiguo = false }
        };
        var dcm = CreateManager();

        yield return null;
        dcm.RequestPermission(BuildCase(msgs));
        dcm.GrantPermission();
        dcm.AdvanceMessage(); // R1
        dcm.MarkCurrentSuspicious(); // jugador identifica R1
        dcm.AdvanceMessage(); // R2 — jugador no la marca
        dcm.AdvanceMessage(); // N1

        var result = dcm.EvaluateCase();

        Assert.AreEqual(2, result.totalSenales);
        Assert.AreEqual(1, result.aciertos);
        Assert.AreEqual(1, result.noIdentificadas);

        Object.DestroyImmediate(dcm.gameObject);
    }

    // ── CA6 — < 50% habilita opción de repetir o ver explicación ─────────────

    [UnityTest]
    public IEnumerator DetectiveCase_BelowThreshold_HabilitaRetryOExplicacion()
    {
        var msgs = new List<DetectiveCaseManager.DetectiveMessage>
        {
            new() { id = "R1", npcSender = "A", texto = "señal 1", esSenalRiesgo = true,  esAmbiguo = false },
            new() { id = "R2", npcSender = "A", texto = "señal 2", esSenalRiesgo = true,  esAmbiguo = false },
            new() { id = "R3", npcSender = "A", texto = "señal 3", esSenalRiesgo = true,  esAmbiguo = false }
        };
        var dcm = CreateManager();
        bool retryEnabled = false;
        dcm.OnRetryOrExplainEnabled += () => retryEnabled = true;

        yield return null;
        dcm.RequestPermission(BuildCase(msgs));
        dcm.GrantPermission();
        dcm.AdvanceMessage(); // R1 — no marca
        dcm.AdvanceMessage(); // R2 — no marca
        dcm.AdvanceMessage(); // R3 — no marca

        var result = dcm.EvaluateCase(); // 0/3 = 0% < 50%

        Assert.IsTrue(result.belowThreshold);
        Assert.IsTrue(retryEnabled);

        Object.DestroyImmediate(dcm.gameObject);
    }

    [UnityTest]
    public IEnumerator DetectiveCase_AboveThreshold_NoHabilitaRetry()
    {
        var msgs = new List<DetectiveCaseManager.DetectiveMessage>
        {
            new() { id = "R1", npcSender = "A", texto = "señal 1", esSenalRiesgo = true,  esAmbiguo = false },
            new() { id = "R2", npcSender = "A", texto = "señal 2", esSenalRiesgo = true,  esAmbiguo = false }
        };
        var dcm = CreateManager();
        bool retryEnabled = false;
        dcm.OnRetryOrExplainEnabled += () => retryEnabled = true;

        yield return null;
        dcm.RequestPermission(BuildCase(msgs));
        dcm.GrantPermission();
        dcm.AdvanceMessage(); dcm.MarkCurrentSuspicious(); // R1 marcada
        dcm.AdvanceMessage(); dcm.MarkCurrentSuspicious(); // R2 marcada

        var result = dcm.EvaluateCase(); // 2/2 = 100%

        Assert.IsFalse(result.belowThreshold);
        Assert.IsFalse(retryEnabled);

        Object.DestroyImmediate(dcm.gameObject);
    }
}
