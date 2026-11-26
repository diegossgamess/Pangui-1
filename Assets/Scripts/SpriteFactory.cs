using UnityEngine;

namespace Komikan
{
    /// <summary>
    /// Creates simple 2D sprites entirely in code so the project needs no
    /// binary art assets. Every sprite is a filled circle drawn into a runtime
    /// <see cref="Texture2D"/>, matching the user's request for "simple colours
    /// and shapes". Pangui (the puma) and the Trewas (the dogs) get different
    /// colours so they are easy to tell apart.
    /// </summary>
    public static class SpriteFactory
    {
        // Puma orange/red for the human-controlled Pangui.
        public static readonly Color PanguiColor = new Color(0.90f, 0.35f, 0.12f, 1f);

        // Blue for the AI-controlled Trewas.
        public static readonly Color TrewaColor = new Color(0.20f, 0.45f, 0.85f, 1f);

        // Soft grey marker drawn on empty board nodes so clickable points show.
        public static readonly Color NodeColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        /// <summary>
        /// Build a filled-circle <see cref="Sprite"/> of the given colour. The
        /// texture is <paramref name="pixelSize"/> pixels square; the circle is
        /// anti-aliased at the rim by fading alpha over the last pixel.
        /// </summary>
        /// <param name="color">Fill colour of the circle.</param>
        /// <param name="pixelSize">Texture width/height in pixels.</param>
        /// <param name="pixelsPerUnit">How many pixels map to one world unit.</param>
        public static Sprite CreateCircleSprite(Color color, int pixelSize = 128, float pixelsPerUnit = 128f)
        {
            if (pixelSize < 4)
            {
                pixelSize = 4;
            }

            var texture = new Texture2D(pixelSize, pixelSize, TextureFormat.RGBA32, false)
            {
                // Simple, crisp look; no tiling needed for a single sprite.
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "KomikanCircle"
            };

            float center = (pixelSize - 1) * 0.5f;
            float radius = pixelSize * 0.5f;
            var pixels = new Color[pixelSize * pixelSize];
            Color transparent = new Color(color.r, color.g, color.b, 0f);

            for (int y = 0; y < pixelSize; y++)
            {
                for (int x = 0; x < pixelSize; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // Fade alpha over the outermost pixel for a smooth edge.
                    float edge = radius - distance;
                    float alpha = Mathf.Clamp01(edge);
                    pixels[y * pixelSize + x] = Color.Lerp(transparent, color, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            var rect = new Rect(0f, 0f, pixelSize, pixelSize);
            var pivot = new Vector2(0.5f, 0.5f);
            var sprite = Sprite.Create(texture, rect, pivot, pixelsPerUnit);
            sprite.name = "KomikanCircleSprite";
            return sprite;
        }
    }
}
