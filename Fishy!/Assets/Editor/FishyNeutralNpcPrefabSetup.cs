#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Arma el prefab base de un NPC neutro (HDU-1): NPC (diálogo con typewriter) +
    /// MissionGiver (entrega una misión al cerrar el diálogo, opcional), listo para
    /// que cada instancia solo traiga su propio NPCDialogue y, si corresponde, un
    /// DesafioData.
    ///
    /// Reutiliza el sistema que ya existe (NPC.cs + InteractionDetector.cs — este
    /// último va en Otto, no en el NPC) en vez de duplicar un sistema de diálogo
    /// paralelo al de Chat/Detective.
    /// </summary>
    public static class FishyNeutralNpcPrefabSetup
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string PrefabPath = PrefabFolder + "/Neutral_NPC.prefab";
        private static readonly Vector2 DefaultTriggerSize = new Vector2(1.5f, 1.5f);

        [MenuItem("Fishy/Crear Prefab NPC Neutro")]
        public static void CrearPrefabNeutralNpc()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            var go = new GameObject("Neutral_NPC");
            Undo.RegisterCreatedObjectUndo(go, "Crear Neutral_NPC");

            go.AddComponent<SpriteRenderer>();

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;

            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = DefaultTriggerSize;

            go.AddComponent<NPC>();
            go.AddComponent<MissionGiver>();

            bool existiaAntes = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, PrefabPath, InteractionMode.UserAction);

            // Comodidad: dialoguePanel/dialogueText/nameText/portraitImage apuntan al
            // Canvas de diálogo de la escena (no se pueden guardar en el prefab asset).
            // Si ya hay otro NPC en la escena con eso cableado (p. ej. "Huemul"), se
            // copian a la instancia nueva para no tener que arrastrarlas a mano.
            var npcInstancia = go.GetComponent<NPC>();
            var npcExistente = Object.FindObjectsByType<NPC>(FindObjectsSortMode.None)
                .FirstOrDefault(n => n != npcInstancia && n.dialoguePanel != null);
            bool uiCopiada = npcExistente != null;
            if (uiCopiada)
            {
                npcInstancia.dialoguePanel = npcExistente.dialoguePanel;
                npcInstancia.dialogueText  = npcExistente.dialogueText;
                npcInstancia.nameText      = npcExistente.nameText;
                npcInstancia.portraitImage = npcExistente.portraitImage;
                EditorUtility.SetDirty(npcInstancia);
            }

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            EditorSceneManager.MarkSceneDirty(go.scene);

            EditorUtility.DisplayDialog(
                "Fishy — Prefab NPC Neutro creado",
                (existiaAntes
                    ? $"Se actualizó '{PrefabPath}'.\n\n"
                    : $"Se creó '{PrefabPath}' y se dejó una instancia en la escena actual.\n\n") +
                "Ahora, en la instancia:\n" +
                "1. Asigna el sprite del NPC en el SpriteRenderer.\n" +
                "2. Ajusta el tamaño del BoxCollider2D (radio de cercanía).\n" +
                "3. Crea (o reusa) un NPCDialogue: Assets → Create → NPC Dialogue, con " +
                "las líneas de este NPC, y asígnalo en 'Dialogue Data' (sirve de respaldo " +
                "si no hay backend). Si el diálogo ya está cargado en la tabla DialogoNPC " +
                "de la BD, pon su dialogo_id en 'Dialogo Id' y ese va a tener prioridad.\n" +
                (uiCopiada
                    ? "4. Las referencias del panel de diálogo (Dialogue Panel/Text/Name/Portrait) " +
                      "se copiaron automáticamente de otro NPC de la escena — no hace falta tocarlas.\n"
                    : "4. Asigna a mano 'Dialogue Panel', 'Dialogue Text', 'Name Text' y 'Portrait " +
                      "Image' (no se encontró otro NPC en la escena del cual copiarlas).\n") +
                "5. Si este NPC debe entregar una misión, asigna un DesafioData en " +
                "MissionGiver → 'Desafio' (si lo dejas vacío, el diálogo queda solo informativo).\n" +
                "6. GUARDA LA ESCENA (Ctrl+S).",
                "OK");

            Debug.Log($"[Fishy] Prefab NPC Neutro listo en {PrefabPath}.");
        }
    }
}
#endif
