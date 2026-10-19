#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Saca la paleta de colores de cualquier imagen del proyecto y la deja lista
    /// para pegar en código (formato Hex(0xRRGGBB), el que usa DetectiveUITheme).
    ///
    /// Uso: selecciona una textura en la ventana Project y llama
    /// Fishy → Extraer paleta de la imagen seleccionada. El resultado sale por
    /// consola y además queda copiado en el portapapeles.
    ///
    /// Lee el archivo del disco en vez de la textura importada, así que funciona
    /// aunque el asset NO tenga "Read/Write Enabled" — que es el error típico al
    /// intentar GetPixels() sobre un sprite normal.
    /// </summary>
    public static class FishyPaletaExtractor
    {
        /// <summary>Tope de seguridad: una foto tiene miles de colores y no sirve
        /// como paleta. Con pixel art nunca se llega a este número.</summary>
        private const int MaxColores = 512;

        private const string MenuRuta = "Fishy/Extraer paleta de la imagen seleccionada";

        [MenuItem(MenuRuta)]
        public static void Extraer()
        {
            var textura = Selection.activeObject as Texture2D;
            string ruta = AssetDatabase.GetAssetPath(textura);

            if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta))
            {
                EditorUtility.DisplayDialog("Fishy — Paleta",
                    "No pude leer el archivo de la textura seleccionada.", "OK");
                return;
            }

            var copia = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!copia.LoadImage(File.ReadAllBytes(ruta)))
            {
                EditorUtility.DisplayDialog("Fishy — Paleta",
                    $"'{Path.GetFileName(ruta)}' no es una imagen que Unity pueda leer " +
                    "(sirven PNG y JPG; los .aseprite no).", "OK");
                Object.DestroyImmediate(copia);
                return;
            }

            // Orden de aparición, no por frecuencia: en una lámina de paleta eso
            // conserva las rampas de tonos tal como las dibujó el artista.
            var orden = new List<Color32>();
            var conteo = new Dictionary<int, int>();

            foreach (Color32 p in copia.GetPixels32())
            {
                if (p.a < 128) continue;                  // transparente: no es paleta
                int clave = (p.r << 16) | (p.g << 8) | p.b;

                if (conteo.ContainsKey(clave)) { conteo[clave]++; continue; }
                conteo[clave] = 1;
                orden.Add(p);
                if (orden.Count >= MaxColores) break;
            }

            int ancho = copia.width, alto = copia.height;
            Object.DestroyImmediate(copia);

            if (orden.Count == 0)
            {
                EditorUtility.DisplayDialog("Fishy — Paleta",
                    "La imagen no tiene píxeles opacos.", "OK");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"// Paleta de {Path.GetFileName(ruta)}  ({ancho}x{alto}, {orden.Count} colores)");
            foreach (Color32 c in orden)
                sb.AppendLine($"Hex(0x{c.r:X2}{c.g:X2}{c.b:X2}),   // #{c.r:X2}{c.g:X2}{c.b:X2}");

            string resultado = sb.ToString();
            EditorGUIUtility.systemCopyBuffer = resultado;
            Debug.Log($"[Fishy] Paleta extraída de '{Path.GetFileName(ruta)}':\n\n{resultado}");

            bool tope = orden.Count >= MaxColores;
            EditorUtility.DisplayDialog("Fishy — Paleta",
                $"{orden.Count} colores extraídos de '{Path.GetFileName(ruta)}'.\n\n" +
                "Están en la consola y copiados al portapapeles, en formato " +
                "Hex(0xRRGGBB) listo para pegar en DetectiveUITheme." +
                (tope ? $"\n\nOJO: se cortó en {MaxColores}. Esta imagen tiene demasiados " +
                        "colores para ser una paleta (¿es una foto o tiene degradados?)." : ""),
                "OK");
        }

        [MenuItem(MenuRuta, true)]
        private static bool ValidarExtraer() => Selection.activeObject is Texture2D;
    }
}
#endif
