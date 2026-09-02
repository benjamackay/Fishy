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
    /// - Si Boot.unity YA existe pero no tiene AuthScreen (p. ej. alguien la creó
    ///   a mano para otra prueba), lo agrega igual — antes se saltaba este paso
    ///   con solo abrir la escena, dejando el ApiManager sin forma de crearse.
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

            bool escenaNueva = !File.Exists(BootScenePath);
            var scene = escenaNueva
                ? CrearEscenaVacia()
                : EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            bool authAgregado = EnsureAuthScreen();

            if (escenaNueva || authAgregado)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, BootScenePath);
                Debug.Log($"[Fishy] Boot.unity actualizado en {BootScenePath}.");
            }

            RegisterBuildScenes();

            EditorUtility.DisplayDialog(
                "Fishy",
                "Arranque configurado.\n\n" +
                "• Boot está en Build Settings (índice 0).\n" +
                "• La escena del juego también está registrada.\n" +
                (authAgregado ? "• Se agregó AuthScreen (faltaba).\n" : "") +
                "\nAbre la escena 'Boot' y pulsa Play: tras la carga se cambiará a la escena del juego.",
                "OK");
        }

        private static UnityEngine.SceneManagement.Scene CrearEscenaVacia()
        {
            EnsureFolder(ScenesFolder);
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        /// <summary>Agrega AuthScreen (+ LoadingScreen) si la escena abierta todavía
        /// no tiene uno. Devuelve true si tuvo que crearlo.</summary>
        private static bool EnsureAuthScreen()
        {
            if (Object.FindAnyObjectByType<AuthScreen>() != null) return false;

            var bootstrap = new GameObject("Bootstrap");
            var auth = bootstrap.AddComponent<AuthScreen>();
            bootstrap.AddComponent<LoadingScreen>();

            // Nombre de la escena de juego a cargar tras el login + carga.
            auth.gameSceneName = Path.GetFileNameWithoutExtension(GameScenePath);
            return true;
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
