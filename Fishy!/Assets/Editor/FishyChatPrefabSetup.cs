#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Fishy.Phone;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Arma el prefab base de un NPC de chat: Collider2D trigger + PhoneChatLauncher,
    /// listo para que cada instancia solo tenga que elegir su npcId (o conversaciones
    /// propias) y arrastrar un sprite.
    ///
    /// Por defecto abre el chat directo frente al NPC visible (modoTelefono = false).
    /// Si esa instancia representa una conversación por celular (como Alex/Sam),
    /// activa "Modo Telefono" en el Inspector para la secuencia diegética completa
    /// (vibración + notificación + zoom) — en ese caso el sprite es opcional.
    ///
    /// Después de crear: asigna el sprite del NPC (si no es modo teléfono), ajusta
    /// el tamaño del BoxCollider2D al radio de cercanía deseado, y en
    /// PhoneChatLauncher elige "Source" (SoloNpc + npcId, banco completo, o tus
    /// propios ChatConversation). Guarda con Ctrl+S.
    /// </summary>
    public static class FishyChatPrefabSetup
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string PrefabPath = PrefabFolder + "/Chat_NPC.prefab";
        private static readonly Vector2 DefaultTriggerSize = new Vector2(1.5f, 1.5f);

        [MenuItem("Fishy/Crear Prefab Chat NPC")]
        public static void CrearPrefabChatNPC()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            var go = new GameObject("Chat_NPC");
            Undo.RegisterCreatedObjectUndo(go, "Crear Chat_NPC");

            go.AddComponent<SpriteRenderer>();

            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = DefaultTriggerSize;

            var launcher = go.AddComponent<PhoneChatLauncher>();
            launcher.source = PhoneChatLauncher.Source.SoloNpc;
            launcher.npcId = "NPC_01";
            launcher.modoTelefono = false;   // por defecto: NPC visible, sin celular

            bool existiaAntes = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, PrefabPath, InteractionMode.UserAction);

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            EditorSceneManager.MarkSceneDirty(go.scene);

            EditorUtility.DisplayDialog(
                "Fishy — Prefab Chat creado",
                (existiaAntes
                    ? $"Se actualizó '{PrefabPath}'.\n\n"
                    : $"Se creó '{PrefabPath}' y se dejó una instancia en la escena actual.\n\n") +
                "Ahora, en la instancia:\n" +
                "1. Asigna el sprite del NPC en el SpriteRenderer (si es conversación " +
                "por celular sin NPC visible, puedes dejarlo vacío).\n" +
                "2. Ajusta el tamaño del BoxCollider2D (radio de cercanía).\n" +
                "3. En PhoneChatLauncher: 'npcId' (ej. \"NPC_01\"/\"NPC_02\"/\"NPC_03\"...) " +
                "si Source = SoloNpc, o cambia 'Source' a ConversacionesAsignadas para tus " +
                "propios ChatConversation.\n" +
                "4. Marca 'Modo Telefono' si esta instancia debe abrir como conversación de " +
                "celular (vibración + notificación + zoom) en vez de chat directo frente al NPC.\n" +
                "5. GUARDA LA ESCENA (Ctrl+S).",
                "OK");

            Debug.Log($"[Fishy] Prefab Chat NPC listo en {PrefabPath}.");
        }
    }
}
#endif
