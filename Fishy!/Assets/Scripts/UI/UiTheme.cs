using UnityEngine;
using UnityEngine.UI;

namespace Fishy.UI
{
    /// <summary>
    /// Paleta y helpers compartidos para las pantallas de UI generadas en runtime
    /// (AuthScreen, MainMenuScreen, LoadingScreen), para que todas compartan el
    /// mismo look moderno sin depender de sprites importados.
    /// </summary>
    public static class UiTheme
    {
        // ── Paleta ───────────────────────────────────────────────────────────────
        public static readonly Color Background = new Color(0.043f, 0.055f, 0.086f, 1f);
        public static readonly Color CardBg      = new Color(0.09f, 0.11f, 0.16f, 1f);
        public static readonly Color CardShadow  = new Color(0f, 0f, 0f, 0.35f);
        public static readonly Color Accent      = new Color(0.18f, 0.62f, 0.44f, 1f);
        public static readonly Color AccentHover = new Color(0.22f, 0.72f, 0.51f, 1f);
        public static readonly Color Secondary   = new Color(1f, 1f, 1f, 0.06f);
        public static readonly Color InputBg     = new Color(0.98f, 0.98f, 1f, 1f);
        public static readonly Color InputText   = new Color(0.08f, 0.09f, 0.12f, 1f);
        public static readonly Color TextPrimary = new Color(0.96f, 0.97f, 1f, 1f);
        public static readonly Color TextMuted   = new Color(0.62f, 0.68f, 0.8f, 1f);
        public static readonly Color ErrorColor  = new Color(1f, 0.45f, 0.45f, 1f);
        public static readonly Color OkColor     = new Color(0.55f, 0.85f, 0.65f, 1f);

        // ── Sprite redondeado (9-slice) reutilizable en cards, botones e inputs ──
        private static Sprite _rounded;
        public static Sprite Rounded => _rounded != null ? _rounded : (_rounded = BuildRoundedSprite(64, 22));

        private static Sprite _roundedSoft;
        /// <summary>Radio de esquina mas suave, para elementos chicos (badges, inputs).</summary>
        public static Sprite RoundedSoft => _roundedSoft != null ? _roundedSoft : (_roundedSoft = BuildRoundedSprite(48, 14));

        private static Sprite _circle;
        /// <summary>Circulo solido (para puntos/marcadores, como el jugador en el mapa).</summary>
        public static Sprite Circle => _circle != null ? _circle : (_circle = BuildCircleSprite(64));

        private static Sprite BuildRoundedSprite(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = $"UiTheme_Rounded_{size}_{radius}"
            };

            var colors = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = 1f;
                    bool nearLeft = x < radius, nearRight = x >= size - radius;
                    bool nearBottom = y < radius, nearTop = y >= size - radius;

                    if ((nearLeft || nearRight) && (nearBottom || nearTop))
                    {
                        float cx = nearLeft ? radius : size - radius - 1;
                        float cy = nearBottom ? radius : size - radius - 1;
                        float dx = x - cx;
                        float dy = y - cy;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    }

                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels32(colors);
            tex.Apply();

            return Sprite.Create(
                tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        private static Sprite BuildCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = $"UiTheme_Circle_{size}"
            };

            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f;
            var colors = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    // Antialias de 1px en el borde.
                    float alpha = Mathf.Clamp01(radius - dist);
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels32(colors);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Aplica el sprite redondeado (9-slice) a una Image existente.</summary>
        public static void MakeRounded(Image image, bool soft = false)
        {
            image.sprite = soft ? RoundedSoft : Rounded;
            image.type = Image.Type.Sliced;
        }
    }
}
