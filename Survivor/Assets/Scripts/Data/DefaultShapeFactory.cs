using UnityEngine;

namespace Survivor.Data
{
    /// Runtime-generated fallback shape for unknown asset keys: a 64x64 white circle sprite (1 world unit wide) tinted magenta.
    public static class DefaultShapeFactory
    {
        /// Texture width and height in pixels.
        public const int TextureSize = 64;

        /// Tint applied to the fallback circle so missing keys stand out.
        public static readonly Color FallbackColor = Color.magenta;

        private static Sprite _circle;

        /// The shared fallback circle (created on first use and again if it was destroyed).
        public static Sprite Circle
        {
            get
            {
                if (_circle == null)
                {
                    _circle = CreateCircle();
                }

                return _circle;
            }
        }

        private static Sprite CreateCircle()
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "DefaultShapeCircle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[TextureSize * TextureSize];
            float center = (TextureSize - 1) * 0.5f;
            float radius = TextureSize * 0.5f;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    // One-pixel soft edge.
                    float alpha = Mathf.Clamp01(radius - distance);
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
            sprite.name = "DefaultShapeCircle";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
