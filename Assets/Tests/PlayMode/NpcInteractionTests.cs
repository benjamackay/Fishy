using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Fishy;

// Tarea 5 — Validar coherencia basica (HDU01)
// Window > General > Test Runner > PlayMode
public class NpcInteractionTests
{
    private static NpcDialogueData BuildDialogue(List<string> lines, string hint = "", string missionId = "")
    {
        var d = ScriptableObject.CreateInstance<NpcDialogueData>();
        d.lines      = lines;
        d.missionHint = hint;
        d.missionId   = missionId;
        return d;
    }

    private static void SetDialogue(NpcInteractable npc, NpcDialogueData data)
    {
        typeof(NpcInteractable)
            .GetField("dialogueData", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(npc, data);
    }

    // ── CA3 — NPC despliega dialogo con pista de mision ───────────────────────

    [UnityTest]
    public IEnumerator NpcInteractable_Start_DispatchesPrimeraLinea()
    {
        var go  = new GameObject("NPC");
        var npc = go.AddComponent<NpcInteractable>();
        SetDialogue(npc, BuildDialogue(new List<string> { "Hola!", "Segunda." }));

        string recibida = null;
        npc.OnLineDisplayed += l => recibida = l;

        yield return null;
        npc.StartInteraction();

        Assert.AreEqual("Hola!", recibida);
        Object.DestroyImmediate(go);
    }

    [UnityTest]
    public IEnumerator NpcInteractable_FinDialogo_MuestraPistaMision()
    {
        var go  = new GameObject("NPC");
        var npc = go.AddComponent<NpcInteractable>();
        SetDialogue(npc, BuildDialogue(new List<string> { "Linea." }, "MISION ACTIVA: busca el objeto."));

        string ultima = null;
        npc.OnLineDisplayed += l => ultima = l;

        yield return null;
        npc.StartInteraction();
        npc.AdvanceDialogue();

        Assert.AreEqual("MISION ACTIVA: busca el objeto.", ultima);
        Assert.IsTrue(npc.IsComplete);
        Object.DestroyImmediate(go);
    }

    // ── CA4 — fin de interaccion registra mision como disponible ──────────────

    [UnityTest]
    public IEnumerator NpcInteractable_FinDialogo_RegistraMisionDisponible()
    {
        var mmGo = new GameObject("MM");
        var mm   = mmGo.AddComponent<MissionManager>();

        var go  = new GameObject("NPC");
        var npc = go.AddComponent<NpcInteractable>();
        SetDialogue(npc, BuildDialogue(new List<string> { "Info." }, missionId: "MISION_TEST_01"));

        yield return null;
        npc.StartInteraction();
        npc.AdvanceDialogue(); // fin → registra mision

        Assert.IsTrue(mm.IsMissionAvailable("MISION_TEST_01"));

        Object.DestroyImmediate(mmGo);
        Object.DestroyImmediate(go);
    }

    // ── CA5 — mision queda registrada como completada ─────────────────────────

    [UnityTest]
    public IEnumerator MissionManager_RegisterCompleted_MarcaCompletada()
    {
        var mmGo = new GameObject("MM");
        var mm   = mmGo.AddComponent<MissionManager>();

        yield return null;
        mm.RegisterMissionAvailable("MISION_TEST_02");
        mm.RegisterMissionCompleted("MISION_TEST_02");

        Assert.IsFalse(mm.IsMissionAvailable("MISION_TEST_02"));
        Assert.IsTrue(mm.IsMissionCompleted("MISION_TEST_02"));

        Object.DestroyImmediate(mmGo);
    }

    // ── CA6 — DisplayMissionUnlocked solo dispara si mision esta disponible ───

    [UnityTest]
    public IEnumerator MissionManager_DisplayUnlocked_SoloSiDisponible()
    {
        var mmGo = new GameObject("MM");
        var mm   = mmGo.AddComponent<MissionManager>();

        string unlocked = null;
        mm.OnMissionUnlockedDisplay += id => unlocked = id;

        yield return null;

        // Sin registrar → no dispara
        mm.DisplayMissionUnlocked("MISION_TEST_03");
        Assert.IsNull(unlocked);

        // Con mision disponible → dispara
        mm.RegisterMissionAvailable("MISION_TEST_03");
        mm.DisplayMissionUnlocked("MISION_TEST_03");
        Assert.AreEqual("MISION_TEST_03", unlocked);

        Object.DestroyImmediate(mmGo);
    }

    // ── CA3 borde — NPC completo no reinicia ─────────────────────────────────

    [UnityTest]
    public IEnumerator NpcInteractable_StartCuandoCompleto_NoDisparaNada()
    {
        var go  = new GameObject("NPC");
        var npc = go.AddComponent<NpcInteractable>();
        SetDialogue(npc, BuildDialogue(new List<string> { "L." }));

        yield return null;
        npc.StartInteraction();
        npc.AdvanceDialogue();

        int llamadas = 0;
        npc.OnLineDisplayed += _ => llamadas++;
        npc.StartInteraction();

        Assert.AreEqual(0, llamadas);
        Object.DestroyImmediate(go);
    }
}
