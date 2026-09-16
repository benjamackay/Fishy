#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Fishy.EditorTools
{
    /// <summary>Uso único: importa los 4 PNG nuevos como Sprite y los engancha
    /// en su ItemData. Se borra después de correrlo (ver traspaso HDU-11).</summary>
    public static class HerramientaTemporalImportarIconosHDU11
    {
        private static readonly (string png, string asset)[] Pares =
        {
            ("Assets/Sprites/Objetos/pin_vigia_silencioso.png", "Assets/Resources/Items/pin_vigia_silencioso.asset"),
            ("Assets/Sprites/Objetos/pin_testigo_solidario.png", "Assets/Resources/Items/pin_testigo_solidario.asset"),
            ("Assets/Sprites/Objetos/pin_analista_corrientes.png", "Assets/Resources/Items/pin_analista_corrientes.asset"),
            ("Assets/Sprites/Objetos/album_evidencias.png", "Assets/Resources/Items/album_evidencias.asset"),
        };

        [MenuItem("Fishy/HDU-11: importar íconos placeholder")]
        public static void Ejecutar()
        {
            foreach (var (png, assetPath) in Pares)
            {
                AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceUpdate);

                var importer = (TextureImporter)AssetImporter.GetAtPath(png);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
                if (sprite == null)
                {
                    Debug.LogError($"[HDU-11] No se pudo cargar el Sprite de {png}");
                    continue;
                }

                var itemData = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
                if (itemData == null)
                {
                    Debug.LogError($"[HDU-11] No se encontró el ItemData en {assetPath}");
                    continue;
                }

                var so = new SerializedObject(itemData);
                so.FindProperty("itemIcon").objectReferenceValue = sprite;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(itemData);

                Debug.Log($"[HDU-11] {assetPath} <- {png}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }
    }
}
#endif
