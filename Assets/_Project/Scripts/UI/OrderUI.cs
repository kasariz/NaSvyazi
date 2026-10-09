using System.Collections.Generic;
using NaSvyazi.Core;
using NaSvyazi.Economy;
using NaSvyazi.Products;
using NaSvyazi.Store;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NaSvyazi.UI
{
    /// <summary>
    /// Окно заказа товара у поставщика: список товаров каталога, количество коробок,
    /// итоговая сумма и оформление заказа. Окно собирается из кода при запуске.
    /// </summary>
    public class OrderUI : ModalWindow
    {
        [SerializeField] private ProductCatalog catalog;
        [SerializeField] private DeliveryService delivery;
        [SerializeField] private int maxBoxesPerProduct = 9;

        private readonly Dictionary<ProductData, int> cart = new Dictionary<ProductData, int>();
        private readonly Dictionary<ProductData, TextMeshProUGUI> quantityTexts =
            new Dictionary<ProductData, TextMeshProUGUI>();

        /// <summary>Элементы строки, которые меняются при открытии категории.</summary>
        private class RowLock
        {
            public GameObject controls;
            public Button unlockButton;
            public TextMeshProUGUI unlockLabel;
        }

        private readonly Dictionary<ProductData, RowLock> rowLocks = new Dictionary<ProductData, RowLock>();

        private GameObject root;
        private TextMeshProUGUI balanceText;
        private TextMeshProUGUI totalText;
        private Button orderButton;

        private void Awake()
        {
            Build();
            SetVisible(false);
        }

        private void OnEnable()
        {
            GameEvents.MoneyChanged += OnMoneyChanged;
            GameEvents.CategoryUnlocked += OnCategoryUnlocked;
            GameEvents.ReputationChanged += OnReputationChanged;
        }

        private void OnDisable()
        {
            GameEvents.MoneyChanged -= OnMoneyChanged;
            GameEvents.CategoryUnlocked -= OnCategoryUnlocked;
            GameEvents.ReputationChanged -= OnReputationChanged;
        }

        private void OnCategoryUnlocked(ProductCategory category)
        {
            if (IsOpen) Refresh();
        }

        private void OnReputationChanged(int value)
        {
            if (IsOpen) Refresh();
        }

        protected override void SetVisible(bool visible) => root.SetActive(visible);
        protected override void OnOpened() => Refresh();

        private void OnMoneyChanged(int balance, int delta)
        {
            if (IsOpen) Refresh();
        }

        // --- Логика корзины ---

        private int TotalCost
        {
            get
            {
                int sum = 0;
                foreach (KeyValuePair<ProductData, int> pair in cart)
                    sum += pair.Key.purchasePrice * pair.Key.unitsPerBox * pair.Value;
                return sum;
            }
        }

        private int TotalBoxes
        {
            get
            {
                int sum = 0;
                foreach (int boxes in cart.Values) sum += boxes;
                return sum;
            }
        }

        private void ChangeQuantity(ProductData product, int delta)
        {
            int current;
            cart.TryGetValue(product, out current);
            cart[product] = Mathf.Clamp(current + delta, 0, maxBoxesPerProduct);
            quantityTexts[product].text = cart[product].ToString();
            Refresh();
        }

        private void ClearCart()
        {
            List<ProductData> keys = new List<ProductData>(cart.Keys);
            foreach (ProductData p in keys)
            {
                cart[p] = 0;
                quantityTexts[p].text = "0";
            }
            Refresh();
        }

        private void PlaceOrder()
        {
            EconomyManager economy = EconomyManager.Instance;
            int total = TotalCost;
            if (total <= 0 || economy == null || delivery == null) return;

            if (!economy.TrySpend(total, "Закупка товара"))
            {
                GameEvents.Notify("Недостаточно денег для заказа");
                Audio.SoundManager.Play(Audio.Sfx.Error);
                return;
            }

            List<DeliveryItem> items = new List<DeliveryItem>();
            foreach (KeyValuePair<ProductData, int> pair in cart)
                if (pair.Value > 0) items.Add(new DeliveryItem(pair.Key, pair.Value));

            delivery.ScheduleDelivery(items);
            ClearCart();
            Close();
        }

        private void Refresh()
        {
            EconomyManager economy = EconomyManager.Instance;
            int balance = economy != null ? economy.Balance : 0;
            int total = TotalCost;

            balanceText.text = $"Баланс: {EconomyManager.Format(balance)}";
            totalText.text = $"Итого: {EconomyManager.Format(total)}   ·   коробок: {TotalBoxes}";
            totalText.color = total > balance ? UIFactory.NegativeColor : UIFactory.TextColor;
            orderButton.interactable = total > 0 && total <= balance;

            RefreshLocks();
        }

        /// <summary>Закрытые категории: вместо выбора количества — кнопка открытия.</summary>
        private void RefreshLocks()
        {
            UnlockManager unlocks = UnlockManager.Instance;
            int reputation = ReputationManager.Instance != null ? ReputationManager.Instance.Value : 100;

            foreach (KeyValuePair<ProductData, RowLock> pair in rowLocks)
            {
                bool open = UnlockManager.IsUnlocked(pair.Key.category);
                pair.Value.controls.SetActive(open);
                pair.Value.unlockButton.gameObject.SetActive(!open);
                if (open || unlocks == null) continue;

                CategoryUnlock rule = unlocks.GetUnlock(pair.Key.category);
                if (rule == null)
                {
                    pair.Value.unlockLabel.text = "Недоступно";
                    pair.Value.unlockButton.interactable = false;
                }
                else if (reputation < rule.minReputation)
                {
                    pair.Value.unlockLabel.text = $"Репутация {rule.minReputation}+";
                    pair.Value.unlockButton.interactable = false;
                }
                else
                {
                    pair.Value.unlockLabel.text = $"Открыть: {EconomyManager.Format(rule.cost)}";
                    pair.Value.unlockButton.interactable = true;
                }
            }
        }

        private void Unlock(ProductData product)
        {
            if (UnlockManager.Instance != null && UnlockManager.Instance.TryUnlock(product.category))
                Refresh();
        }

        // --- Построение окна ---

        private void Build()
        {
            Canvas canvas = UIFactory.CreateCanvas("Order Canvas", 20, transform);
            root = canvas.gameObject;

            Image dim = UIFactory.CreatePanel("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.55f));
            UIFactory.Stretch(dim.rectTransform);

            Image window = UIFactory.CreatePanel("Window", canvas.transform, UIFactory.PanelColor);
            UIFactory.Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1240f, 780f));
            UIFactory.AddVerticalLayout(window.gameObject, 10f, 32);

            TextMeshProUGUI title = UIFactory.CreateText("Title", window.transform,
                "Терминал заказа  ·  поставщик «СвязьОпт»", 34f, UIFactory.TextColor);
            title.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(title, height: 50f);

            balanceText = UIFactory.CreateText("Balance", window.transform, "", 24f, UIFactory.MutedTextColor);
            UIFactory.SetSize(balanceText, height: 36f);

            CreateRow(window.transform, null, true);

            List<ProductData> products = catalog != null ? catalog.products : new List<ProductData>();
            foreach (ProductData product in products)
            {
                if (product == null) continue;
                cart[product] = 0;
                CreateRow(window.transform, product, false);
            }

            RectTransform spacer = UIFactory.CreateRect("Spacer", window.transform);
            UIFactory.SetSize(spacer, flexibleHeight: 1f);

            RectTransform footer = UIFactory.CreateRect("Footer", window.transform);
            UIFactory.AddHorizontalLayout(footer.gameObject, 12f, 0);
            UIFactory.SetSize(footer, height: 64f);

            totalText = UIFactory.CreateText("Total", footer, "", 28f, UIFactory.TextColor);
            UIFactory.SetSize(totalText, flexibleWidth: 1f);

            UIFactory.SetSize(UIFactory.CreateButton("Clear", footer, "Очистить", ClearCart,
                UIFactory.ButtonColor), width: 180f);
            UIFactory.SetSize(UIFactory.CreateButton("Close", footer, "Закрыть (Esc)", Close,
                UIFactory.ButtonColor), width: 210f);
            orderButton = UIFactory.CreateButton("Order", footer, "Оформить заказ", PlaceOrder,
                UIFactory.AccentColor);
            UIFactory.SetSize(orderButton, width: 260f);
        }

        /// <summary>Строка таблицы: заголовок (product == null) или товар.</summary>
        private void CreateRow(Transform parent, ProductData product, bool header)
        {
            Image row = UIFactory.CreatePanel(header ? "Header" : $"Row_{product.id}", parent,
                header ? Color.clear : UIFactory.RowColor);
            UIFactory.AddHorizontalLayout(row.gameObject, 12f, 16);
            UIFactory.SetSize(row, height: header ? 34f : 58f);

            float size = header ? 20f : 24f;
            Color main = header ? UIFactory.MutedTextColor : UIFactory.TextColor;

            AddCell(row.transform, header ? "Товар" : product.displayName, size, main, 340f);
            AddCell(row.transform, header ? "Категория" : product.category.ToDisplayName(),
                header ? size : 20f, UIFactory.MutedTextColor, 190f);
            AddCell(row.transform, header ? "В коробке" : $"{product.unitsPerBox} шт. × {product.purchasePrice}",
                header ? size : 20f, UIFactory.MutedTextColor, 180f);
            AddCell(row.transform, header ? "Цена коробки" :
                EconomyManager.Format(product.purchasePrice * product.unitsPerBox), size, main, 170f);

            if (header)
            {
                AddCell(row.transform, "Коробок", size, main, 190f);
                return;
            }

            // Выбор количества коробок (для открытых категорий).
            RectTransform controls = UIFactory.CreateRect("Controls", row.transform);
            UIFactory.AddHorizontalLayout(controls.gameObject, 8f, 0);
            UIFactory.SetSize(controls, width: 196f);

            UIFactory.SetSize(UIFactory.CreateButton("Minus", controls, "−",
                () => ChangeQuantity(product, -1), UIFactory.ButtonColor, 30f), width: 56f);

            TextMeshProUGUI qty = UIFactory.CreateText("Qty", controls, "0", 26f, UIFactory.TextColor,
                TextAlignmentOptions.Center);
            UIFactory.SetSize(qty, width: 60f);
            quantityTexts[product] = qty;

            UIFactory.SetSize(UIFactory.CreateButton("Plus", controls, "+",
                () => ChangeQuantity(product, 1), UIFactory.ButtonColor, 30f), width: 56f);

            // Кнопка открытия категории (для закрытых категорий).
            Button unlock = UIFactory.CreateButton("Unlock", row.transform, "", () => Unlock(product),
                UIFactory.AccentColor, 20f);
            UIFactory.SetSize(unlock, width: 196f);

            rowLocks[product] = new RowLock
            {
                controls = controls.gameObject,
                unlockButton = unlock,
                unlockLabel = unlock.GetComponentInChildren<TextMeshProUGUI>()
            };
        }

        private static void AddCell(Transform row, string text, float size, Color color, float width)
        {
            TextMeshProUGUI cell = UIFactory.CreateText("Cell", row, text, size, color);
            UIFactory.SetSize(cell, width: width);
        }
    }
}
