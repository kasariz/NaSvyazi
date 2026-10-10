using NaSvyazi.Interaction;
using TMPro;
using UnityEngine;

namespace NaSvyazi.Products
{
    /// <summary>
    /// Коробка с товаром. Её можно переносить (наследует Carryable),
    /// выкладывать из неё товар на полку и собирать товар обратно.
    /// </summary>
    public class ProductBox : Carryable
    {
        [Header("Содержимое")]
        [SerializeField] private ProductData product;

        [Tooltip("Количество товара. -1 — полная коробка")]
        [SerializeField] private int count = -1;

        [Tooltip("Необязательная надпись на коробке")]
        [SerializeField] private TMP_Text label;

        public ProductData Product => product;
        public int Count => count;
        public int Capacity => product != null ? product.unitsPerBox : 0;
        public bool IsEmpty => product == null || count <= 0;
        public bool IsFull => product != null && count >= Capacity;

        public override string DisplayName =>
            IsEmpty ? "Пустая коробка" : $"{product.displayName} ({count} шт.)";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private Renderer boxRenderer;
        private Renderer bandRenderer;
        private ProductData paintedFor;
        private bool painted;

        protected override void Awake()
        {
            base.Awake();
            if (product != null && count < 0) count = product.unitsPerBox;
            if (count < 0) count = 0;
            CreateBand();
            UpdateLabel();
        }

        /// <summary>Цветная полоса вокруг коробки — цвет товара, видна издалека.</summary>
        private void CreateBand()
        {
            Transform mesh = transform.Find("Mesh");
            if (mesh == null) return;
            boxRenderer = mesh.GetComponent<Renderer>();

            Vector3 size = mesh.localScale;
            GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cube);
            band.name = "Band";
            Destroy(band.GetComponent<Collider>());
            band.transform.SetParent(transform, false);
            band.transform.localPosition = new Vector3(0f, size.y * 0.25f, 0f);
            band.transform.localScale = new Vector3(size.x + 0.004f, size.y * 0.18f, size.z + 0.004f);
            bandRenderer = band.GetComponent<Renderer>();
        }

        /// <summary>
        /// Внешний вид по содержимому: картон своего оттенка для каждой категории
        /// (крафт, серый, белый, чёрный) и полоса цвета товара. Пустая коробка — без полосы.
        /// </summary>
        private void Paint()
        {
            if (painted && paintedFor == (IsEmpty ? null : product)) return;
            painted = true;
            paintedFor = IsEmpty ? null : product;

            Color cardboard = new Color(0.78f, 0.62f, 0.43f);
            bool darkBox = false;
            if (paintedFor != null)
            {
                switch (paintedFor.category)
                {
                    case ProductCategory.Cables: cardboard = new Color(0.66f, 0.64f, 0.6f); break;
                    case ProductCategory.SimCards: cardboard = new Color(0.93f, 0.93f, 0.92f); break;
                    case ProductCategory.Audio: cardboard = new Color(0.2f, 0.21f, 0.24f); darkBox = true; break;
                    case ProductCategory.Phones: cardboard = new Color(0.96f, 0.96f, 0.97f); break;
                }
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            if (boxRenderer != null)
            {
                block.SetColor(BaseColorId, cardboard);
                boxRenderer.SetPropertyBlock(block);
            }
            if (bandRenderer != null)
            {
                bandRenderer.enabled = paintedFor != null;
                if (paintedFor != null)
                {
                    block.SetColor(BaseColorId, paintedFor.color);
                    bandRenderer.SetPropertyBlock(block);
                }
            }
            if (label != null)
                label.color = darkBox ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.15f, 0.1f, 0.05f);
        }

        /// <summary>Заполнить коробку (используется при доставке заказа).</summary>
        public void Fill(ProductData newProduct, int amount)
        {
            product = newProduct;
            count = newProduct != null ? Mathf.Clamp(amount, 0, newProduct.unitsPerBox) : 0;
            UpdateLabel();
        }

        /// <summary>Достать 1 шт. товара из коробки.</summary>
        public bool TryTakeOne(out ProductData taken)
        {
            if (IsEmpty)
            {
                taken = null;
                return false;
            }

            taken = product;
            count--;
            UpdateLabel();
            return true;
        }

        /// <summary>Можно ли положить в коробку 1 шт. этого товара.</summary>
        public bool CanAccept(ProductData item) =>
            item != null && (IsEmpty || (item == product && !IsFull));

        /// <summary>Положить 1 шт. товара в коробку.</summary>
        public bool TryPutOne(ProductData item)
        {
            if (!CanAccept(item)) return false;

            if (IsEmpty)
            {
                product = item;
                count = 0;
            }

            count++;
            UpdateLabel();
            return true;
        }

        private void UpdateLabel()
        {
            Paint();
            if (label == null) return;
            label.text = IsEmpty ? "ПУСТО" : $"{product.displayName}\n{count}/{Capacity}";
        }
    }
}
