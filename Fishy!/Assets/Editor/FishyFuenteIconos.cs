#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Fishy.Detective;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Fishy.EditorTools
{
    /// <summary>
    /// Genera el TMP Font Asset de los símbolos (la lupa del header, por ahora) a
    /// partir de NotoSansSymbols2 y lo deja en Resources, donde el Modo Detective
    /// lo carga por ruta.
    ///
    /// Hace falta porque Mango no trae esos glifos: el carácter 🔍 salía como un
    /// cuadro roto. Noto Sans Symbols 2 sí los tiene y es monocromática, así que el
    /// símbolo se puede teñir con el color de la paleta (una a color, como
    /// NotoColorEmoji, no serviría: el color viene quemado en el bitmap).
    ///
    /// La fuente se hornea en modo STATIC, no Dynamic. La diferencia importa:
    ///
    ///   • una fuente Dynamic nace con la tabla de caracteres vacía y rasteriza
    ///     cada glifo recién cuando alguien lo pide, o sea que desde disco no hay
    ///     forma de comprobar si el símbolo va a aparecer;
    ///   • y peor: TMP_PreBuildProcessor le BORRA el atlas a las fuentes Dynamic al
    ///     compilar, así que un icono que se veía en el editor desaparecía en el
    ///     build.
    ///
    /// Horneada, el glifo queda dentro del .asset, se puede verificar sin abrir
    /// Unity y en runtime no interviene el motor de fuentes.
    ///
    /// Uso: Fishy → Generar fuente de iconos. Hay que volver a correrlo si se
    /// cambian los símbolos del theme (DetectiveUITheme.Textos).
    /// </summary>
    public static class FishyFuenteIconos
    {
        private const string RutaTtf = "Assets/TextMesh Pro/Fonts/NotoSansSymbols2-Regular.ttf";
        private const string CarpetaDestino = "Assets/TextMesh Pro/Resources/Fonts & Materials";
        private const string NombreAsset = "NotoSymbols2 SDF";

        /// <summary>
        /// Los símbolos a hornear salen del theme, no de una copia local: si se
        /// listaran acá, cambiar el icono en DetectiveUITheme dejaría la fuente
        /// horneada con el glifo viejo y nadie se enteraría.
        /// </summary>
        private static string[] Simbolos => new[] { DetectiveUITheme.Textos.IconoHeader };

        [MenuItem("Fishy/Generar fuente de iconos")]
        public static void Generar()
        {
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(RutaTtf);
            if (ttf == null)
            {
                Avisar($"No encuentro '{RutaTtf}'.\n\nSi lo moviste, actualiza RutaTtf en este script.");
                return;
            }

            uint[] puntos = PuntosDeCodigo(Simbolos);
            if (puntos.Length == 0)
            {
                Avisar("DetectiveUITheme.Textos no tiene ningún símbolo que hornear.");
                return;
            }

            // Se crea Dynamic para poder agregarle glifos; queda Static más abajo.
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
                Avisar("TMP no pudo crear la fuente. Revisa la consola.");
                return;
            }

            fuente.name = NombreAsset;

            // Acá se rasterizan los glifos y entran a la tabla de caracteres.
            fuente.TryAddCharacters(puntos, out uint[] faltantes);

            var ausentes = new List<uint>(faltantes ?? new uint[0]);
            if (ausentes.Count > 0)
            {
                // Sin el glifo la fuente no sirve para nada, así que no se escribe:
                // un asset a medias solo hace perder el tiempo después.
                Object.DestroyImmediate(fuente);
                Avisar($"'{Path.GetFileName(RutaTtf)}' no tiene {Describir(ausentes)}.\n\n" +
                       "No generé nada. Cambia el símbolo en DetectiveUITheme.Textos " +
                       "o usa un TTF que sí lo traiga.");
                return;
            }

            // Horneada: el setter suelta la referencia al TTF, que ya no hace falta
            // (y de paso el TTF deja de arrastrarse al build).
            fuente.atlasPopulationMode = AtlasPopulationMode.Static;

            string destino = $"{CarpetaDestino}/{NombreAsset}.asset";

            // Si ya existía se sobreescribe, para no dejar dos versiones sueltas y
            // que nadie sepa cuál es la buena (ya pasó con Mango).
            if (File.Exists(destino)) AssetDatabase.DeleteAsset(destino);
            AssetDatabase.CreateAsset(fuente, destino);

            // El material y los atlas van DENTRO del asset: sueltos, mover el .asset
            // los deja huérfanos y la fuente se ve en blanco.
            if (fuente.material != null)
            {
                fuente.material.name = NombreAsset + " Material";
                AssetDatabase.AddObjectToAsset(fuente.material, fuente);
            }
            foreach (Texture2D atlas in fuente.atlasTextures ?? new Texture2D[0])
            {
                if (atlas == null) continue;
                atlas.name = NombreAsset + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, fuente);
            }

            EditorUtility.SetDirty(fuente);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = fuente;
            EditorGUIUtility.PingObject(fuente);

            var resumen = new StringBuilder();
            foreach (uint p in puntos)
                resumen.Append($"\n  {char.ConvertFromUtf32((int)p)}  U+{p:X4}  " +
                               (fuente.HasCharacter((int)p) ? "ok" : "NO QUEDÓ"));

            Debug.Log($"[Fishy] Fuente de iconos horneada en {destino} " +
                      $"({fuente.characterTable.Count} glifo(s)).{resumen}", fuente);

            Avisar($"Listo: '{destino}'.\n\nGlifos horneados:{resumen}\n\n" +
                   "El Modo Detective ya la carga solo para la lupa del header.");
        }

        /// <summary>
        /// Pasa cada símbolo a su code point. Los emoji suelen estar fuera del BMP
        /// (🔍 es U+1F50D), y ahí en C# ocupan dos char: hay que recomponer el par
        /// subrogado o se manda media letra y no calza con nada.
        /// </summary>
        private static uint[] PuntosDeCodigo(IEnumerable<string> simbolos)
        {
            var puntos = new List<uint>();

            foreach (string s in simbolos)
            {
                if (string.IsNullOrEmpty(s)) continue;

                uint punto = s.Length >= 2 && char.IsSurrogatePair(s[0], s[1])
                    ? (uint)char.ConvertToUtf32(s[0], s[1])
                    : s[0];

                if (!puntos.Contains(punto)) puntos.Add(punto);
            }

            return puntos.ToArray();
        }

        private static string Describir(IEnumerable<uint> puntos)
        {
            return string.Join(", ", puntos.Select(p => $"'{char.ConvertFromUtf32((int)p)}' (U+{p:X4})"));
        }

        private static void Avisar(string mensaje)
        {
            EditorUtility.DisplayDialog("Fishy — Fuente de iconos", mensaje, "OK");
        }
    }
}
#endif
