using UnityEngine;

namespace NaSvyazi.Products
{
    /// <summary>
    /// Модели товаров, собранные из простых фигур: чехол в блистере, защитное стекло,
    /// моток кабеля на картонке, зарядка, SIM-карта, наушники, колонка, смартфоны на подставке.
    /// Каждая модель вписана в габариты товара (ProductData.size), опорная точка — центр нижней грани.
    /// Так товары на полках различимы без отдельных 3D-моделей.
    /// </summary>
    public static class ProductModels
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private static readonly Color White = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color Card = new Color(0.92f, 0.92f, 0.9f);
        private static readonly Color Dark = new Color(0.08f, 0.08f, 0.09f);
        private static readonly Color Screen = new Color(0.05f, 0.06f, 0.08f);
        private static readonly Color Gold = new Color(0.85f, 0.7f, 0.3f);
        private static readonly Color Clear = new Color(0.75f, 0.9f, 1f);
        private static readonly Color Metal = new Color(0.7f, 0.72f, 0.75f);

        /// <summary>Собрать модель товара внутри root. false — для товара нет модели.</summary>
        public static bool Build(ProductData p, Transform parent)
        {
            // Модели строятся «лицом» к −Z; на полке покупатель смотрит со стороны +Z — разворачиваем.
            GameObject pivot = new GameObject("Model");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Transform root = pivot.transform;

            Vector3 s = p.size;
            switch (p.id)
            {
                case "case": BuildCase(root, s, p.color); return true;
                case "glass": BuildGlass(root, s, p.color); return true;
                case "cable": BuildCable(root, s); return true;
                case "charger": BuildCharger(root, s, p.color); return true;
                case "sim": BuildSim(root, s, p.color); return true;
                case "earbuds": BuildEarbuds(root, s, p.color); return true;
                case "speaker": BuildSpeaker(root, s, p.color); return true;
                case "phone_basic":
                case "phone_mid": BuildPhone(root, s, p.color, p.id == "phone_mid"); return true;
            }

            // Неизвестный товар — по категории.
            switch (p.category)
            {
                case ProductCategory.Phones: BuildPhone(root, s, p.color, false); return true;
                case ProductCategory.Accessories: BuildCase(root, s, p.color); return true;
                default:
                    if (Application.isPlaying) Object.Destroy(pivot);
                    else Object.DestroyImmediate(pivot);
                    return false;
            }
        }

        // ---------------- Модели ----------------

        /// <summary>Чехол в блистере: белая картонка с подвесом, цветной чехол, вырез камеры.</summary>
        private static void BuildCase(Transform r, Vector3 s, Color color)
        {
            CardBacking(r, s, Card);
            Box(r, new Vector3(0f, s.y * 0.45f, -s.z * 0.1f), new Vector3(s.x * 0.78f, s.y * 0.72f, s.z * 0.55f), color);
            Box(r, new Vector3(-s.x * 0.2f, s.y * 0.7f, -s.z * 0.4f), new Vector3(s.x * 0.22f, s.y * 0.14f, 0.004f), Dark);
        }

        /// <summary>Защитное стекло: конверт цвета товара с прозрачным «окном» и тёмной полосой.</summary>
        private static void BuildGlass(Transform r, Vector3 s, Color color)
        {
            Box(r, new Vector3(0f, s.y * 0.5f, 0f), s, color);
            Box(r, new Vector3(0f, s.y * 0.55f, -s.z * 0.52f), new Vector3(s.x * 0.7f, s.y * 0.55f, 0.002f), Clear);
            Box(r, new Vector3(0f, s.y * 0.15f, -s.z * 0.52f), new Vector3(s.x * 0.85f, s.y * 0.08f, 0.002f), Dark);
        }

        /// <summary>Кабель: моток на картонке (диск с отверстием) и разъём сверху.</summary>
        private static void BuildCable(Transform r, Vector3 s)
        {
            CardBacking(r, s, new Color(0.25f, 0.45f, 0.85f));
            float d = Mathf.Min(s.x, s.y) * 0.8f;
            Cylinder(r, new Vector3(0f, s.y * 0.42f, -s.z * 0.2f), new Vector3(d, s.z * 0.25f, d), White, 90f);
            Cylinder(r, new Vector3(0f, s.y * 0.42f, -s.z * 0.45f), new Vector3(d * 0.45f, 0.002f, d * 0.45f), new Color(0.25f, 0.45f, 0.85f), 90f);
            Box(r, new Vector3(0f, s.y * 0.82f, -s.z * 0.2f), new Vector3(s.x * 0.12f, s.y * 0.12f, s.z * 0.25f), Metal);
        }

        /// <summary>Зарядное устройство: корпус, вилка и порт USB-C.</summary>
        private static void BuildCharger(Transform r, Vector3 s, Color color)
        {
            Box(r, new Vector3(0f, s.y * 0.35f, 0f), new Vector3(s.x * 0.8f, s.y * 0.7f, s.z * 0.85f), White);
            Box(r, new Vector3(0f, s.y * 0.35f, -s.z * 0.43f), new Vector3(s.x * 0.8f, s.y * 0.12f, 0.003f), color);
            Box(r, new Vector3(0f, s.y * 0.55f, -s.z * 0.43f), new Vector3(s.x * 0.18f, s.y * 0.05f, 0.003f), Dark);
            foreach (float x in new[] { -s.x * 0.15f, s.x * 0.15f })
                Cylinder(r, new Vector3(x, s.y * 0.85f, 0f), new Vector3(s.x * 0.08f, s.y * 0.15f, s.x * 0.08f), Metal, 0f);
        }

        /// <summary>Стартовый пакет: картонка цвета товара, белая SIM-карта с золотым чипом.</summary>
        private static void BuildSim(Transform r, Vector3 s, Color color)
        {
            Box(r, new Vector3(0f, s.y * 0.5f, 0f), s, color);
            Box(r, new Vector3(0f, s.y * 0.45f, -s.z * 0.55f), new Vector3(s.x * 0.6f, s.y * 0.4f, 0.002f), White);
            Box(r, new Vector3(-s.x * 0.08f, s.y * 0.42f, -s.z * 0.65f), new Vector3(s.x * 0.18f, s.y * 0.14f, 0.002f), Gold);
            Box(r, new Vector3(0f, s.y * 0.85f, -s.z * 0.55f), new Vector3(s.x * 0.8f, s.y * 0.08f, 0.002f), White);
        }

        /// <summary>Наушники: коробка цвета товара, на лицевой стороне — белый кейс с наушниками.</summary>
        private static void BuildEarbuds(Transform r, Vector3 s, Color color)
        {
            Box(r, new Vector3(0f, s.y * 0.5f, 0f), s, color);
            Cylinder(r, new Vector3(0f, s.y * 0.45f, -s.z * 0.5f), new Vector3(s.x * 0.55f, 0.004f, s.y * 0.38f), White, 90f);
            foreach (float x in new[] { -s.x * 0.12f, s.x * 0.12f })
                Cylinder(r, new Vector3(x, s.y * 0.48f, -s.z * 0.55f), new Vector3(s.x * 0.12f, 0.004f, s.x * 0.12f), Metal, 90f);
            Box(r, new Vector3(0f, s.y * 0.85f, -s.z * 0.51f), new Vector3(s.x * 0.6f, s.y * 0.06f, 0.002f), White);
        }

        /// <summary>Колонка: вертикальный цилиндр с тёмными торцами и решёткой.</summary>
        private static void BuildSpeaker(Transform r, Vector3 s, Color color)
        {
            float d = Mathf.Min(s.x, s.z) * 0.95f;
            Cylinder(r, new Vector3(0f, s.y * 0.5f, 0f), new Vector3(d, s.y * 0.42f, d), color, 0f);
            Cylinder(r, new Vector3(0f, s.y * 0.96f, 0f), new Vector3(d * 1.02f, s.y * 0.04f, d * 1.02f), Dark, 0f);
            Cylinder(r, new Vector3(0f, s.y * 0.04f, 0f), new Vector3(d * 1.02f, s.y * 0.04f, d * 1.02f), Dark, 0f);
            Box(r, new Vector3(0f, s.y * 0.5f, -d * 0.49f), new Vector3(d * 0.45f, s.y * 0.6f, 0.004f), Dark);
        }

        /// <summary>Смартфон на прозрачной подставке: корпус, экран с «обоями», камера.</summary>
        private static void BuildPhone(Transform r, Vector3 s, Color color, bool premium)
        {
            Box(r, new Vector3(0f, s.y * 0.06f, s.z * 0.1f), new Vector3(s.x * 0.8f, s.y * 0.12f, s.z * 0.8f), Clear);

            GameObject phone = new GameObject("Phone");
            phone.transform.SetParent(r, false);
            phone.transform.localPosition = new Vector3(0f, s.y * 0.08f, 0f);
            phone.transform.localRotation = Quaternion.Euler(12f, 0f, 0f); // откинут назад на подставке
            Transform t = phone.transform;

            float h = s.y * 0.9f, w = s.x * 0.85f, d = 0.009f;
            Box(t, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), premium ? color : Dark);
            Box(t, new Vector3(0f, h * 0.5f, -d * 0.55f), new Vector3(w * 0.9f, h * 0.93f, 0.001f), Screen);
            // «Обои» на экране — цвет модели.
            Box(t, new Vector3(0f, h * 0.38f, -d * 0.6f), new Vector3(w * 0.9f, h * 0.45f, 0.001f),
                Color.Lerp(color, White, 0.25f));
            Box(t, new Vector3(-w * 0.25f, h * 0.85f, d * 0.6f), new Vector3(w * 0.3f, h * 0.12f, 0.003f),
                premium ? Dark : Metal);
        }

        // ---------------- Детали ----------------

        /// <summary>Белая картонка-подложка с отверстием-подвесом сверху.</summary>
        private static void CardBacking(Transform r, Vector3 s, Color color)
        {
            Box(r, new Vector3(0f, s.y * 0.5f, s.z * 0.35f), new Vector3(s.x, s.y, s.z * 0.3f), color);
            Box(r, new Vector3(0f, s.y * 0.92f, s.z * 0.18f), new Vector3(s.x * 0.25f, s.y * 0.04f, 0.002f), Dark);
        }

        private static void Box(Transform parent, Vector3 position, Vector3 size, Color color) =>
            Part(PrimitiveType.Cube, parent, position, size, color, Quaternion.identity);

        /// <summary>Цилиндр; xRotation = 90 — лежит «лицом» к покупателю (ось вдоль Z).</summary>
        private static void Cylinder(Transform parent, Vector3 position, Vector3 size, Color color, float xRotation) =>
            Part(PrimitiveType.Cylinder, parent, position, size, color, Quaternion.Euler(xRotation, 0f, 0f));

        private static void Part(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color color,
            Quaternion rotation)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;

            Renderer renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // мелочь без теней — быстрее
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }
}
