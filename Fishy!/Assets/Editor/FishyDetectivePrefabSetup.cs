#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Fishy.Detective;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Arma el prefab base de un NPC del modo Detective (HDU-10): Collider2D
    /// trigger + DetectiveLauncher, listo para que cada instancia solo tenga que
    /// configurar su <c>casoId</c> y arrastrar un sprite.
    ///
    /// Después de crear: asigna el sprite del NPC, ajusta el tamaño del
    /// BoxCollider2D al radio de cercanía deseado, y si corresponde arrastra un
    /// DesafioData en "Desafío Asociado". Guarda con Ctrl+S.
    /// </summary>
    public static class FishyDetectivePrefabSetup
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string PrefabPath = PrefabFolder + "/Detective_NPC.prefab";
        private static readonly Vector2 DefaultTriggerSize = new Vector2(1.5f, 1.5f);

        [MenuItem("Fishy/Crear Prefab Detective NPC")]
        public static void CrearPrefabDetectiveNPC()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            var go = new GameObject("Detective_NPC");
            Undo.RegisterCreatedObjectUndo(go, "Crear Detective_NPC");

            go.AddComponent<SpriteRenderer>();

            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = DefaultTriggerSize;

            go.AddComponent<DetectiveLauncher>();
            go.AddComponent<DetectiveCaseManager>();

            bool existiaAntes = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, PrefabPath, InteractionMode.UserAction);

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            EditorSceneManager.MarkSceneDirty(go.scene);

            EditorUtility.DisplayDialog(
                "Fishy — Prefab Detective creado",
                (existiaAntes
                    ? $"Se actualizó '{PrefabPath}'.\n\n"
                    : $"Se creó '{PrefabPath}' y se dejó una instancia en la escena actual.\n\n") +
                "Ahora, en la instancia:\n" +
                "1. Asigna el sprite del NPC en el SpriteRenderer.\n" +
                "2. Ajusta el tamaño del BoxCollider2D (radio de cercanía).\n" +
                "3. En DetectiveLauncher: 'Caso Id' (ej. DC_CASO_01, tal como está en la base) " +
                "y, si corresponde, 'Desafío Asociado'.\n" +
                "4. GUARDA LA ESCENA (Ctrl+S).",
                "OK");

            Debug.Log($"[Fishy] Prefab Detective NPC listo en {PrefabPath}.");
        }
    }
}
#endif
