#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Genera el TMP Font Asset de los símbolos (lupa, check, etc.) a partir de
    /// NotoSansSymbols2, y lo deja en Resources para que el Modo Detective pueda
    /// cargarlo por ruta.
    ///
    /// Hace falta porque Mango no tiene esos glifos: el carácter 🔍 se dibujaba
    /// como un cuadrito roto. Esta fuente sí los trae y es monocromática, así que
    /// el símbolo se puede teñir del color de la paleta (una de color, como
    /// NotoColorEmoji, no serviría: el color viene quemado en el bitmap).
    ///
    /// Se genera en modo Dynamic: el asset queda liviano y TMP rasteriza cada
    /// glifo la primera vez que se usa, así que agregar símbolos nuevos después
    /// no obliga a regenerar nada.
    ///
    /// Uso: Fishy → Generar fuente de iconos. Basta correrlo una vez; queda
    /// versionado en el repo.
    /// </summary>
    public static class FishyFuenteIconos
    {
        private const string RutaTtf = "Assets/TextMesh Pro/Fonts/NotoSansSymbols2-Regular.ttf";
        private const string CarpetaDestino = "Assets/TextMesh Pro/Resources/Fonts & Materials";
        private const string NombreAsset = "NotoSymbols2 SDF";

        [MenuItem("Fishy/Generar fuente de iconos")]
        public static void Generar()
        {
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(RutaTtf);
            if (ttf == null)
            {
                EditorUtility.DisplayDialog("Fishy — Fuente de iconos",
                    $"No encuentro '{RutaTtf}'.\n\nSi lo moviste, actualiza RutaTtf en este script.",
                    "OK");
                return;
            }

            string destino = $"{CarpetaDestino}/{NombreAsset}.asset";

            var fuente = TMP_FontAsset.CreateFontAsset(
                ttf,
                samplingPointSize: 90,
                atlasPadding: 9,
                renderMode: GlyphRenderMode.SDFAA,
                atlasWidth: 512, atlasHeight: 512,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (fuente == null)
            {
                EditorUtility.DisplayDialog("Fishy — Fuente de iconos",
                    "TMP no pudo generar la fuente. Revisa la consola.", "OK");
                return;
            }

            fuente.name = NombreAsset;

            // Si ya existía, se sobreescribe para no dejar dos versiones sueltas
            // que después nadie sabe cuál es la buena (ya pasó con Mango).
            if (File.Exists(destino)) AssetDatabase.DeleteAsset(destino);
            AssetDatabase.CreateAsset(fuente, destino);

            // El material y el atlas van DENTRO del asset: si quedan como archivos
            // sueltos, mover el .asset los deja huérfanos y la fuente se ve en blanco.
            if (fuente.material != null)
            {
                fuente.material.name = NombreAsset + " Material";
                AssetDatabase.AddObjectToAsset(fuente.material, fuente);
            }
            if (fuente.atlasTexture != null)
            {
                fuente.atlasTexture.name = NombreAsset + " Atlas";
                AssetDatabase.AddObjectToAsset(fuente.atlasTexture, fuente);
            }

            EditorUtility.SetDirty(fuente);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = fuente;
            EditorGUIUtility.PingObject(fuente);

            EditorUtility.DisplayDialog("Fishy — Fuente de iconos",
                $"Listo: '{destino}'.\n\n" +
                "El Modo Detective ya la carga sola para la lupa del header. " +
                "Símbolos disponibles en esta fuente: lupa, check, estrella y " +
                "advertencia, entre otros.",
                "OK");

            Debug.Log($"[Fishy] Fuente de iconos generada en {destino}.", fuente);
        }
    }
}
#endif
