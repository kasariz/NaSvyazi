using TMPro;
using UnityEngine;

namespace NaSvyazi.Customers
{
    /// <summary>
    /// Табличка под репликой над головой покупателя: тёмная плашка со светлой рамкой
    /// и скруглёнными углами, подгоняется под размер текста. Без неё светлый текст
    /// терялся на белых стенах салона.
    /// </summary>
    public class SpeechBubble
    {
        private static Sprite plateSprite;

        private readonly TMP_Text text;
        private readonly SpriteRenderer plate;
        private string shownText;

        private static readonly Vector2 Padding = new Vector2(0.16f, 0.09f);

        public SpeechBubble(TMP_Text text)
        {
            this.text = text;

            GameObject go = new GameObject("BubblePlate");
            go.transform.SetParent(text.transform, false);
            plate = go.AddComponent<SpriteRenderer>();
            plate.sprite = GetSprite();
            plate.drawMode = SpriteDrawMode.Sliced;
            plate.sortingOrder = 0;
            plate.enabled = false;

            // Текст рисуется после плашки (поверх неё).
            Renderer textRenderer = text.GetComponent<Renderer>();
            if (textRenderer != null) textRenderer.sortingOrder = 1;
        }

        /// <summary>Подогнать плашку под текущий текст (вызывать после смены текста).</summary>
        public void Refresh()
        {
            string current = text.text;
            if (current == shownText) return;
            shownText = current;

            if (string.IsNullOrEmpty(current))
            {
                plate.enabled = false;
                return;
            }

            text.ForceMeshUpdate();
            Bounds bounds = text.textBounds;
            plate.size = new Vector2(bounds.size.x + Padding.x * 2f, bounds.size.y + Padding.y * 2f);
            // Текст читается со стороны −Z, значит плашка — чуть дальше по +Z.
            plate.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, 0.01f);
            plate.enabled = true;
        }

        /// <summary>Скруглённый прямоугольник 64×64 для 9-slice: тёмная заливка, светлая рамка.</summary>
        private static Sprite GetSprite()
        {
            if (plateSprite != null) return plateSprite;

            const int size = 64, radius = 16, border = 3;
            Color fill = new Color(0.09f, 0.1f, 0.12f, 0.9f);
            Color frame = new Color(1f, 1f, 1f, 0.85f);
            Color clear = new Color(0f, 0f, 0f, 0f);

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = DistanceOutside(x + 0.5f, y + 0.5f, size, radius);
                    if (d > 0f) tex.SetPixel(x, y, clear);
                    else if (d > -border) tex.SetPixel(x, y, frame);
                    else tex.SetPixel(x, y, fill);
                }
            tex.Apply();

            // 400 пикселей на единицу: рамка ≈ 7,5 мм, скругление ≈ 4 см в мире.
            plateSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 400f, 0,
                SpriteMeshType.FullRect, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
            return plateSprite;
        }

        /// <summary>Расстояние от точки до края скруглённого квадрата (&gt; 0 — снаружи).</summary>
        private static float DistanceOutside(float x, float y, int size, int radius)
        {
            float cx = Mathf.Clamp(x, radius, size - radius);
            float cy = Mathf.Clamp(y, radius, size - radius);
            float dx = x - cx, dy = y - cy;
            float inner = Mathf.Sqrt(dx * dx + dy * dy) - radius;
            // Внутри прямой части — расстояние до ближайшей стороны.
            if (dx == 0f && dy == 0f)
                inner = -Mathf.Min(Mathf.Min(x, size - x), Mathf.Min(y, size - y));
            return inner;
        }
    }
}
