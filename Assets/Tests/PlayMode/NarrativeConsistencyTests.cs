using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Fishy;

// Tarea 3 — Validar consistencia entre escenas (HDU09 CA1, CA2, CA3, CA4)
// Window > General > Test Runner > PlayMode
public class NarrativeConsistencyTests
{
    // ── CA4 — historial insegura en zona se recuerda durante la sesión ────────

    [UnityTest]
    public IEnumerator DecisionHistory_InseguraEnZona_EsRecordada()
    {
        var go  = new GameObject("DHM");
        var dhm = go.AddComponent<DecisionHistoryManager>();

        yield return null;
        dhm.RecordDecision("HDU2_NPC01_F1_Q01", "insegura", "desconocidos");

        Assert.IsTrue(dhm.HasPriorInseguraInZone("desconocidos"));
        Assert.IsFalse(dhm.HasPriorInseguraInZone("ciberacoso")); // otra zona no contaminada

        Object.DestroyImmediate(go);
    }

    [UnityTest]
    public IEnumerator DecisionHistory_SoloDecisionesSeguras_NoMarcaInsegura()
    {
        var go  = new GameObject("DHM");
        var dhm = go.AddComponent<DecisionHistoryManager>();

        yield return null;
        dhm.RecordDecision("HDU2_NPC01_F1_Q01", "segura_optima", "desconocidos");
        dhm.RecordDecision("HDU2_NPC01_F1_Q02", "segura_basica", "desconocidos");

        Assert.IsFalse(dhm.HasPriorInseguraInZone("desconocidos"));

        Object.DestroyImmediate(go);
    }

    // ── CA3 — patrón de zona se calcula correctamente ─────────────────────────

    [UnityTest]
    public IEnumerator ZoneSummary_PatronExcelente_CuandoMayoriaOptima()
    {
        var go  = new GameObject("DHM");
        var dhm = go.AddComponent<DecisionHistoryManager>();

        yield return null;
        dhm.RecordDecision("Q01", "segura_optima", "ciberacoso");
        dhm.RecordDecision("Q02", "segura_optima", "ciberacoso");
        dhm.RecordDecision("Q03", "segura_basica", "ciberacoso");

        var s = dhm.GetZoneSummary("ciberacoso");
        Assert.AreEqual("excelente", s.pattern);
        Assert.AreEqual(2, s.seguraOptimaCount);
        Assert.AreEqual(0, s.inseguraCount);

        Object.DestroyImmediate(go);
    }

    [UnityTest]
    public IEnumerator ZoneSummary_PatronNecesitaRefuerzo_CuandoMayoriaInsegura()
    {
        var go  = new GameObject("DHM");
        var dhm = go.AddComponent<DecisionHistoryManager>();

        yield return null;
        dhm.RecordDecision("Q01", "insegura", "desconocidos");
        dhm.RecordDecision("Q02", "insegura", "desconocidos");
        dhm.RecordDecision("Q03", "segura_basica", "desconocidos");

        var s = dhm.GetZoneSummary("desconocidos");
        Assert.AreEqual("necesita_refuerzo", s.pattern);
        Assert.AreEqual(2, s.inseguraCount);

        Object.DestroyImmediate(go);
    }

    // ── CA1 — reacción positiva se dispara dentro de 2 segundos ──────────────

    [UnityTest]
    public IEnumerator NarrativeController_DecisionSeguraOptima_DispatchesPositiveReaction()
    {
        var dhmGo = new GameObject("DHM");
        dhmGo.AddComponent<DecisionHistoryManager>();
        var ncGo = new GameObject("NC");
        var nc   = ncGo.AddComponent<NarrativeController>();

        string reaction = null;
        nc.OnPositiveReaction += msg => reaction = msg;

        yield return null;
        nc.HandleDecision("Q01", "segura_optima", "NPC_01", "desconocidos");
        yield return new WaitForSeconds(1.5f); // ≤ 2 s (CA1)

        Assert.IsNotNull(reaction);
        StringAssert.Contains("Otto", reaction);

        Object.DestroyImmediate(dhmGo);
        Object.DestroyImmediate(ncGo);
    }

    // ── CA2 — reacción de consecuencia para decisión insegura ─────────────────

    [UnityTest]
    public IEnumerator NarrativeController_DecisionInsegura_DispatchesConsequenceReaction()
    {
        var dhmGo = new GameObject("DHM");
        dhmGo.AddComponent<DecisionHistoryManager>();
        var ncGo = new GameObject("NC");
        var nc   = ncGo.AddComponent<NarrativeController>();

        string consequence = null;
        nc.OnConsequenceReaction += msg => consequence = msg;

        yield return null;
        nc.HandleDecision("Q01", "insegura", "NPC_01", "ciberacoso");
        yield return new WaitForSeconds(1.5f);

        Assert.IsNotNull(consequence);
        StringAssert.Contains("Otto", consequence);

        Object.DestroyImmediate(dhmGo);
        Object.DestroyImmediate(ncGo);
    }

    // ── CA4 — reacción positiva diferente cuando hubo insegura previa ─────────

    [UnityTest]
    public IEnumerator NarrativeController_PositiveTrasPriorInsegura_UsaMensajeCorreccion()
    {
        var dhmGo = new GameObject("DHM");
        var dhm   = dhmGo.AddComponent<DecisionHistoryManager>();
        var ncGo  = new GameObject("NC");
        var nc    = ncGo.AddComponent<NarrativeController>();

        yield return null;
        dhm.RecordDecision("Q01", "insegura", "desconocidos"); // registra riesgo previo

        string reaction = null;
        nc.OnPositiveReaction += msg => reaction = msg;

        nc.HandleDecision("Q02", "segura_optima", "NPC_01", "desconocidos");
        yield return new WaitForSeconds(1.5f);

        Assert.IsNotNull(reaction);
        // CA4: el mensaje menciona que puede seguir mejorando tras el riesgo previo
        StringAssert.Contains("elegiste bien", reaction);

        Object.DestroyImmediate(dhmGo);
        Object.DestroyImmediate(ncGo);
    }

    // ── CA3 — FinalizeZone dispara resumen de zona ────────────────────────────

    [UnityTest]
    public IEnumerator NarrativeController_FinalizeZone_DispatchesZoneSummary()
    {
        var dhmGo = new GameObject("DHM");
        var dhm   = dhmGo.AddComponent<DecisionHistoryManager>();
        var ncGo  = new GameObject("NC");
        var nc    = ncGo.AddComponent<NarrativeController>();

        yield return null;
        dhm.RecordDecision("Q01", "segura_optima", "ciberacoso");
        dhm.RecordDecision("Q02", "segura_optima", "ciberacoso");

        DecisionHistoryManager.ZoneSummary captured = null;
        nc.OnZoneComplete += s => captured = s;
        nc.FinalizeZone("ciberacoso");

        Assert.IsNotNull(captured);
        Assert.AreEqual("ciberacoso", captured.zone);
        Assert.AreEqual("excelente", captured.pattern);

        Object.DestroyImmediate(dhmGo);
        Object.DestroyImmediate(ncGo);
    }
}
