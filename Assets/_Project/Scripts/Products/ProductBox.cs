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

        protected override void Awake()
        {
            base.Awake();
            if (product != null && count < 0) count = product.unitsPerBox;
            if (count < 0) count = 0;
            UpdateLabel();
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
            if (label == null) return;
            label.text = IsEmpty ? "ПУСТО" : $"{product.displayName}\n{count}/{Capacity}";
        }
    }
}
