#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Fishy.UI;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Configura el flujo de arranque: escena Boot (Login + carga) → escena del juego.
    ///
    /// Menú: Fishy → Configurar arranque (Boot + Juego).
    /// - Crea Assets/Scenes/Boot.unity con un GameObject "Bootstrap"
    ///   (AuthScreen + LoadingScreen) si no existe.
    /// - Registra Boot (índice 0) y la escena del juego en Build Settings,
    ///   para que LoadSceneAsync pueda cargarla.
    /// </summary>
    public static class FishySceneSetup
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string BootScenePath = "Assets/Scenes/Boot.unity";
        private const string GameScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Fishy/Configurar arranque (Boot + Juego)")]
        public static void Setup()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (!File.Exists(BootScenePath))
                CreateBootScene();
            else
                EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            RegisterBuildScenes();

            EditorUtility.DisplayDialog(
                "Fishy",
                "Arranque configurado.\n\n" +
                "• Boot está en Build Settings (índice 0).\n" +
                "• La escena del juego también está registrada.\n\n" +
                "Abre la escena 'Boot' y pulsa Play: tras la carga se cambiará a la escena del juego.",
                "OK");
        }

        private static void CreateBootScene()
        {
            EnsureFolder(ScenesFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var bootstrap = new GameObject("Bootstrap");
            var auth = bootstrap.AddComponent<AuthScreen>();
            bootstrap.AddComponent<LoadingScreen>();

            // Nombre de la escena de juego a cargar tras el login + carga.
            auth.gameSceneName = Path.GetFileNameWithoutExtension(GameScenePath);

            EditorSceneManager.SaveScene(scene, BootScenePath);
            Debug.Log($"[Fishy] Escena de arranque creada en {BootScenePath}.");
        }

        private static void RegisterBuildScenes()
        {
            var list = new List<EditorBuildSettingsScene>();

            if (File.Exists(BootScenePath))
                list.Add(new EditorBuildSettingsScene(BootScenePath, true)); // índice 0

            if (File.Exists(GameScenePath))
                list.Add(new EditorBuildSettingsScene(GameScenePath, true));

            EditorBuildSettings.scenes = list.ToArray();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            var name = Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
