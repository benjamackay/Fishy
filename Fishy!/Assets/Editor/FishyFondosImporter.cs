#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Importa como Sprite entero cualquier imagen que caiga en Resources/Fondos.
    ///
    /// Existe porque el fallo es silencioso y desconcertante: Unity trae varias de
    /// estas imágenes como "Multiple" (una hoja de sprites recortada), y entonces
    /// Resources.LoadAll&lt;Sprite&gt; no devuelve la imagen completa —devuelve los
    /// recortes, que no existen—. El fondo simplemente no aparece y no hay ningún
    /// error que lo explique. Los archivos sin extensión ni siquiera se importan
    /// como imagen.
    ///
    /// Solo actúa en la primera importación (cuando todavía no hay .meta), así que
    /// si después ajustas algo a mano en el Inspector, se respeta.
    /// </summary>
    public class FishyFondosImporter : AssetPostprocessor
    {
        private const string Carpeta = "/Resources/Fondos/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains(Carpeta)) return;

            // Con .meta ya existente manda lo que haya puesto la persona.
            if (!assetImporter.importSettingsMissing) return;

            var importador = (TextureImporter)assetImporter;
            importador.textureType      = TextureImporterType.Sprite;
            importador.spriteImportMode = SpriteImportMode.Single;

            // Un fondo se dibuja a tamaño fijo en la UI: los mipmaps solo lo verían
            // borroso y ocupan un tercio más de memoria.
            importador.mipmapEnabled = false;
            importador.wrapMode      = TextureWrapMode.Repeat;   // por si se usa en mosaico
        }
    }
}
#endif
