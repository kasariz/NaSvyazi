using System.Collections.Generic;
using NaSvyazi.Building;
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
        [SerializeField] private FurnitureCatalog furnitureCatalog;

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
        private readonly Dictionary<ProductData, TextMeshProUGUI[]> priceCells =
            new Dictionary<ProductData, TextMeshProUGUI[]>();

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
            GameEvents.LevelChanged += OnLevelChanged;
        }

        private void OnDisable()
        {
            GameEvents.MoneyChanged -= OnMoneyChanged;
            GameEvents.CategoryUnlocked -= OnCategoryUnlocked;
            GameEvents.LevelChanged -= OnLevelChanged;
        }

        private void OnCategoryUnlocked(ProductCategory category)
        {
            if (IsOpen) Refresh();
        }

        private void OnLevelChanged(int level, int xp, int next)
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
                    sum += pair.Key.BoxCost * pair.Value;
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
            GameEvents.RaiseOrderPlaced();
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

            RefreshPrices();
            RefreshLocks();
            RefreshFurnitureAndDevelopment();
        }

        /// <summary>Закрытые категории: вместо выбора количества — кнопка открытия.</summary>
        private void RefreshLocks()
        {
            UnlockManager unlocks = UnlockManager.Instance;
            int level = LevelManager.CurrentLevel;

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
                else if (level < rule.minLevel)
                {
                    pair.Value.unlockLabel.text = $"Уровень салона {rule.minLevel}";
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

        // --- Вкладки «Мебель» и «Развитие» ---

        private enum Tab { Products, Furniture, Development }

        private readonly List<GameObject> tabPanels = new List<GameObject>();
        private readonly List<Button> tabButtons = new List<Button>();
        private Tab currentTab = Tab.Products;

        private class ActionRow
        {
            public Button button;
            public TextMeshProUGUI label;
            public TextMeshProUGUI title;
            public TextMeshProUGUI description;
        }

        private readonly Dictionary<FurnitureData, ActionRow> furnitureRows = new Dictionary<FurnitureData, ActionRow>();
        private ActionRow salonRow;
        private ActionRow storageRow;
        private ActionRow cashierRow;
        private TextMeshProUGUI developmentInfo;

        private void ShowTab(Tab tab)
        {
            currentTab = tab;
            for (int i = 0; i < tabPanels.Count; i++)
            {
                tabPanels[i].SetActive(i == (int)tab);
                tabButtons[i].GetComponent<Image>().color = i == (int)tab ? UIFactory.AccentColor : UIFactory.ButtonColor;
            }
            Refresh();
        }

        private void RefreshFurnitureAndDevelopment()
        {
            int level = LevelManager.CurrentLevel;

            foreach (KeyValuePair<FurnitureData, ActionRow> pair in furnitureRows)
            {
                FurnitureData f = pair.Key;
                bool levelOk = level >= f.minLevel;
                pair.Value.label.text = levelOk ? $"Купить: {EconomyManager.Format(f.price)}" : $"Уровень салона {f.minLevel}";
                pair.Value.button.interactable = levelOk && EconomyManager.Instance != null
                                                 && EconomyManager.Instance.CanAfford(f.price);
            }

            RefreshStageRow(salonRow, LayoutZone.Salon, level);
            RefreshStageRow(storageRow, LayoutZone.Storage, level);

            StaffManager staff = StaffManager.Instance;
            if (cashierRow != null && staff != null)
            {
                if (staff.CashierHired)
                {
                    cashierRow.label.text = "Уволить";
                    cashierRow.button.interactable = true;
                }
                else if (level < staff.CashierMinLevel)
                {
                    cashierRow.label.text = $"Уровень салона {staff.CashierMinLevel}";
                    cashierRow.button.interactable = false;
                }
                else
                {
                    cashierRow.label.text = "Нанять";
                    cashierRow.button.interactable = true;
                }
            }

            if (developmentInfo != null)
            {
                LevelManager lm = LevelManager.Instance;
                string levelText = lm == null ? "" : lm.IsMaxLevel
                    ? $"Уровень салона: {lm.Level} (максимальный)"
                    : $"Уровень салона: {lm.Level}   ·   опыт {lm.Xp} из {lm.XpForNextLevel}";
                int rent = DayManager.Instance != null ? DayManager.Instance.DailyRent : 0;
                int wages = DayManager.Instance != null ? DayManager.Instance.DailyWages : 0;
                developmentInfo.text = $"{levelText}\nРасходы в день: аренда {EconomyManager.Format(rent)}" +
                                       (wages > 0 ? $", зарплата {EconomyManager.Format(wages)}" : "");
            }
        }

        private void BuyFurniture(FurnitureData data)
        {
            EconomyManager economy = EconomyManager.Instance;
            if (economy == null || FurniturePlacer.Instance == null) return;

            if (!economy.TrySpend(data.price, $"Покупка мебели: {data.displayName}"))
            {
                GameEvents.Notify("Недостаточно денег");
                Audio.SoundManager.Play(Audio.Sfx.Error);
                return;
            }

            Close();
            FurniturePlacer.Instance.BeginPlaceNew(data);
            GameEvents.Notify("Выберите место: ЛКМ — поставить, R — повернуть, ПКМ — отмена");
        }

        private void ToggleCashier()
        {
            StaffManager staff = StaffManager.Instance;
            if (staff == null) return;
            if (staff.CashierHired) staff.FireCashier();
            else staff.TryHireCashier();
            Refresh();
        }

        /// <summary>Строка «следующее расширение» для зала или склада.</summary>
        private static void RefreshStageRow(ActionRow row, LayoutZone zone, int level)
        {
            ExpansionManager expansion = ExpansionManager.Instance;
            if (row == null || expansion == null) return;

            LayoutStage next = expansion.NextStage(zone);
            if (next == null)
            {
                row.title.text = zone == LayoutZone.Salon ? "Торговый зал" : "Склад";
                row.description.text = "Все расширения уже открыты.";
                row.label.text = "Готово";
                row.button.interactable = false;
                return;
            }

            int total = LayoutPlan.Stages(zone).Length - 1;
            row.title.text = $"{next.name}  ({expansion.StageOf(zone) + 1}/{total})";
            row.description.text = $"{next.description} Аренда +{EconomyManager.Format(next.extraRent)} в день.";

            if (level < next.minLevel)
            {
                row.label.text = $"Уровень салона {next.minLevel}";
                row.button.interactable = false;
            }
            else
            {
                row.label.text = $"Купить: {EconomyManager.Format(next.price)}";
                row.button.interactable = EconomyManager.Instance != null && EconomyManager.Instance.CanAfford(next.price);
            }
        }

        private void BuyExpansion(LayoutZone zone)
        {
            if (ExpansionManager.Instance != null) ExpansionManager.Instance.TryUpgrade(zone);
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
                Vector2.zero, new Vector2(1280f, 940f));
            UIFactory.AddVerticalLayout(window.gameObject, 10f, 32);

            TextMeshProUGUI title = UIFactory.CreateText("Title", window.transform,
                "Терминал салона  ·  поставщик «СвязьОпт»", 34f, UIFactory.TextColor);
            title.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(title, height: 50f);

            balanceText = UIFactory.CreateText("Balance", window.transform, "", 24f, UIFactory.MutedTextColor);
            UIFactory.SetSize(balanceText, height: 34f);

            // Вкладки.
            RectTransform tabs = UIFactory.CreateRect("Tabs", window.transform);
            UIFactory.AddHorizontalLayout(tabs.gameObject, 10f, 0);
            UIFactory.SetSize(tabs, height: 52f);
            AddTabButton(tabs, "Товары", Tab.Products);
            AddTabButton(tabs, "Мебель", Tab.Furniture);
            AddTabButton(tabs, "Развитие", Tab.Development);
            RectTransform tabsSpacer = UIFactory.CreateRect("Spacer", tabs);
            UIFactory.SetSize(tabsSpacer, flexibleWidth: 1f);
            UIFactory.SetSize(UIFactory.CreateButton("Close", tabs, "Закрыть (Esc)", Close,
                UIFactory.ButtonColor), width: 210f);

            RectTransform content = UIFactory.CreateRect("Content", window.transform);
            UIFactory.SetSize(content, flexibleHeight: 1f);

            BuildProductsPanel(CreateTabPanel(content, "Products"));
            BuildFurniturePanel(CreateTabPanel(content, "Furniture"));
            BuildDevelopmentPanel(CreateTabPanel(content, "Development"));

            ShowTab(Tab.Products);
        }

        private RectTransform CreateTabPanel(RectTransform content, string name)
        {
            RectTransform panel = UIFactory.CreateRect(name, content);
            UIFactory.Stretch(panel);
            UIFactory.AddVerticalLayout(panel.gameObject, 8f, 0);
            tabPanels.Add(panel.gameObject);
            return panel;
        }

        private void AddTabButton(Transform parent, string label, Tab tab)
        {
            Button b = UIFactory.CreateButton($"Tab_{tab}", parent, label, () => ShowTab(tab), UIFactory.ButtonColor);
            UIFactory.SetSize(b, width: 200f);
            tabButtons.Add(b);
        }

        private void BuildProductsPanel(RectTransform panel)
        {
            CreateRow(panel, null, true);

            List<ProductData> products = catalog != null ? catalog.products : new List<ProductData>();
            foreach (ProductData product in products)
            {
                if (product == null) continue;
                cart[product] = 0;
                CreateRow(panel, product, false);
            }

            RectTransform spacer = UIFactory.CreateRect("Spacer", panel);
            UIFactory.SetSize(spacer, flexibleHeight: 1f);

            RectTransform footer = UIFactory.CreateRect("Footer", panel);
            UIFactory.AddHorizontalLayout(footer.gameObject, 12f, 0);
            UIFactory.SetSize(footer, height: 64f);

            totalText = UIFactory.CreateText("Total", footer, "", 28f, UIFactory.TextColor);
            UIFactory.SetSize(totalText, flexibleWidth: 1f);

            UIFactory.SetSize(UIFactory.CreateButton("Clear", footer, "Очистить", ClearCart,
                UIFactory.ButtonColor), width: 180f);
            orderButton = UIFactory.CreateButton("Order", footer, "Оформить заказ", PlaceOrder,
                UIFactory.AccentColor);
            UIFactory.SetSize(orderButton, width: 260f);
        }

        private void BuildFurniturePanel(RectTransform panel)
        {
            TextMeshProUGUI hint = UIFactory.CreateText("Hint", panel,
                "Купленная мебель сразу появляется перед вами: выберите место прицелом, R — повернуть, ЛКМ — поставить. " +
                "Стоящую мебель можно переставить: наведите прицел на каркас и нажмите E. Список прокручивается колесом мыши.",
                20f, UIFactory.MutedTextColor, TextAlignmentOptions.TopLeft);
            UIFactory.SetSize(hint, height: 50f);

            if (furnitureCatalog == null)
            {
                TextMeshProUGUI missing = UIFactory.CreateText("Missing", panel,
                    "Каталог мебели не подключён: выполните «На связи → Подготовить сцену (итерация 8)».",
                    22f, UIFactory.NegativeColor);
                UIFactory.SetSize(missing, height: 40f);
            }
            else
            {
                // Мебели много — список прокручивается колесом мыши.
                RectTransform list = UIFactory.CreateScrollList("List", panel, 8f);
                foreach (FurnitureData f in furnitureCatalog.items)
                {
                    if (f == null) continue;
                    FurnitureData data = f;
                    furnitureRows[f] = CreateActionRow(list, f.displayName, f.description, () => BuyFurniture(data), 64f);
                }
            }
        }

        private void BuildDevelopmentPanel(RectTransform panel)
        {
            developmentInfo = UIFactory.CreateText("Info", panel, "", 22f, UIFactory.TextColor, TextAlignmentOptions.TopLeft);
            UIFactory.SetSize(developmentInfo, height: 70f);

            salonRow = CreateActionRow(panel, "Торговый зал", "", () => BuyExpansion(LayoutZone.Salon));
            storageRow = CreateActionRow(panel, "Склад", "", () => BuyExpansion(LayoutZone.Storage));

            int wage = StaffManager.Instance != null ? StaffManager.Instance.CashierWage : 1500;
            cashierRow = CreateActionRow(panel, "Кассир",
                $"Сам сканирует товар, принимает оплату и консультирует (не всегда идеально). Зарплата {EconomyManager.Format(wage)} в день.",
                ToggleCashier);

            RectTransform spacer = UIFactory.CreateRect("Spacer", panel);
            UIFactory.SetSize(spacer, flexibleHeight: 1f);
        }

        private ActionRow CreateActionRow(Transform parent, string name, string description, System.Action onClick,
            float height = 84f)
        {
            Image row = UIFactory.CreatePanel($"Row_{name}", parent, UIFactory.RowColor);
            UIFactory.AddHorizontalLayout(row.gameObject, 16f, 16);
            UIFactory.SetSize(row, height: height);

            TextMeshProUGUI title = UIFactory.CreateText("Name", row.transform, name, 26f, UIFactory.TextColor);
            title.fontStyle = FontStyles.Bold;
            title.enableAutoSizing = true;
            title.fontSizeMin = 16f;
            title.fontSizeMax = 26f;
            UIFactory.SetSize(title, width: 300f);

            TextMeshProUGUI desc = UIFactory.CreateText("Desc", row.transform, description, 19f, UIFactory.MutedTextColor);
            desc.enableAutoSizing = true;
            desc.fontSizeMin = 14f;
            desc.fontSizeMax = 19f;
            UIFactory.SetSize(desc, flexibleWidth: 1f);

            Button button = UIFactory.CreateButton("Action", row.transform, "", onClick, UIFactory.AccentColor, 22f);
            UIFactory.SetSize(button, width: 280f);

            return new ActionRow
            {
                button = button,
                label = button.GetComponentInChildren<TextMeshProUGUI>(),
                title = title,
                description = desc
            };
        }

        /// <summary>Строка таблицы: заголовок (product == null) или товар.</summary>
        private void CreateRow(Transform parent, ProductData product, bool header)
        {
            Image row = UIFactory.CreatePanel(header ? "Header" : $"Row_{product.id}", parent,
                header ? Color.clear : UIFactory.RowColor);
            UIFactory.AddHorizontalLayout(row.gameObject, 12f, 16);
            UIFactory.SetSize(row, height: header ? 30f : 46f);

            // Цветной значок товара (цвет совпадает с товаром на полке).
            Image icon = UIFactory.CreatePanel("Icon", row.transform, header ? Color.clear : product.color);
            UIFactory.SetSize(icon, width: 22f);

            float size = header ? 20f : 24f;
            Color main = header ? UIFactory.MutedTextColor : UIFactory.TextColor;

            AddCell(row.transform, header ? "Товар" : product.displayName, size, main, 330f);
            AddCell(row.transform, header ? "Категория" : product.category.ToDisplayName(),
                header ? size : 20f, UIFactory.MutedTextColor, 190f);
            TextMeshProUGUI unitCell = AddCell(row.transform,
                header ? "В коробке" : $"{product.unitsPerBox} шт. × {product.EffectivePurchasePrice}",
                header ? size : 20f, UIFactory.MutedTextColor, 180f);
            TextMeshProUGUI boxCell = AddCell(row.transform, header ? "Цена коробки" :
                EconomyManager.Format(product.BoxCost), size, main, 170f);
            if (!header) priceCells[product] = new[] { unitCell, boxCell };

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

        private static TextMeshProUGUI AddCell(Transform row, string text, float size, Color color, float width)
        {
            TextMeshProUGUI cell = UIFactory.CreateText("Cell", row, text, size, color);
            UIFactory.SetSize(cell, width: width);
            return cell;
        }

        /// <summary>Обновить закупочные цены (меняются навыком «Оптовые скидки»).</summary>
        private void RefreshPrices()
        {
            foreach (KeyValuePair<ProductData, TextMeshProUGUI[]> pair in priceCells)
            {
                pair.Value[0].text = $"{pair.Key.unitsPerBox} шт. × {pair.Key.EffectivePurchasePrice}";
                pair.Value[1].text = EconomyManager.Format(pair.Key.BoxCost);
            }
        }
    }
}
