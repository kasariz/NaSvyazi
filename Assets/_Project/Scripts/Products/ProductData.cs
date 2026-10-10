using UnityEngine;

namespace NaSvyazi.Products
{
    /// <summary>
    /// Описание товара (ScriptableObject). Один файл-ассет на каждый вид товара:
    /// название, категория, закупочная и рыночная цена, размер и внешний вид.
    /// Данные отделены от логики: чтобы добавить товар, не нужно менять код.
    /// Создаётся через меню Assets → Create → На связи → Товар.
    /// </summary>
    [CreateAssetMenu(fileName = "Product_", menuName = "На связи/Товар", order = 0)]
    public class ProductData : ScriptableObject
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Описание")]
        public string id;
        public string displayName;
        public ProductCategory category;
        [TextArea] public string description;

        [Header("Экономика (руб.)")]
        [Tooltip("Цена закупки у поставщика за 1 шт.")]
        [Min(0)] public int purchasePrice = 100;

        /// <summary>Фактическая цена закупки с учётом навыка «Оптовые скидки».</summary>
        public int EffectivePurchasePrice =>
            Mathf.Max(1, Mathf.RoundToInt(purchasePrice * Progression.SkillManager.PurchasePriceMultiplier));

        /// <summary>Стоимость одной коробки у поставщика.</summary>
        public int BoxCost => EffectivePurchasePrice * unitsPerBox;

        [Tooltip("Средняя рыночная цена. От неё покупатели оценивают, дорого или нет")]
        [Min(0)] public int marketPrice = 250;

        [Tooltip("Сколько штук приходит в одной коробке")]
        [Min(1)] public int unitsPerBox = 10;

        [Header("Внешний вид")]
        [Tooltip("Модель товара. Если не задана, рисуется цветной параллелепипед")]
        public GameObject modelPrefab;

        [Tooltip("Габариты 1 шт. в метрах (ширина, высота, глубина) — по ним раскладывается полка")]
        public Vector3 size = new Vector3(0.08f, 0.15f, 0.02f);

        public Color color = Color.white;
        public Sprite icon;

        /// <summary>
        /// Создаёт визуальное представление 1 шт. товара. Опорная точка — центр нижней грани,
        /// поэтому объект можно ставить прямо на поверхность полки.
        /// </summary>
        public GameObject CreateVisual(Transform parent)
        {
            GameObject root = new GameObject(displayName);
            root.transform.SetParent(parent, false);

            GameObject model;
            if (modelPrefab != null)
            {
                model = Instantiate(modelPrefab, root.transform);
            }
            else
            {
                model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                model.transform.SetParent(root.transform, false);
                model.transform.localScale = size;
                model.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);

                MaterialPropertyBlock block = new MaterialPropertyBlock();
                block.SetColor(BaseColorId, color);
                model.GetComponent<Renderer>().SetPropertyBlock(block);
            }

            // Товар на полке не должен перехватывать луч взаимодействия.
            foreach (Collider c in model.GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
                Destroy(c);
            }

            return root;
        }
    }
}
