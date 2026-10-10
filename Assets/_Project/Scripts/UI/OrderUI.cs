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
    /// Терминал салона: заказ товара у поставщика, покупка мебели, расширение помещений и персонал.
    /// Окно собирается из кода. Все кнопки одного размера (ширина ActionWidth, высота ButtonHeight),
    /// строки и карточки — с одинаковыми отступами.
    /// </summary>
    public class OrderUI : ModalWindow
    {
        [SerializeField] private ProductCatalog catalog;
        [SerializeField] private DeliveryService delivery;
        [SerializeField] private int maxBoxesPerProduct = 9;
        [SerializeField] private FurnitureCatalog furnitureCatalog;

        // --- Размеры ---
        private const float WindowWidth = 1360f;
        private const float WindowHeight = 900f;
        private const int Padding = 28;
        private const float ActionWidth = 230f;     // ширина всех кнопок действий
        private const float QtyColumn = 212f;       // колонка «количество» в таблице товаров
        private const float CardHeight = 184f;

        // --- Корзина ---
        private readonly Dictionary<ProductData, int> cart = new Dictionary<ProductData, int>();
        private readonly Dictionary<ProductData, TextMeshProUGUI> quantityTexts =
            new Dictionary<ProductData, TextMeshProUGUI>();

        /// <summary>Строка товара: цены и переключение «количество / открыть категорию».</summary>
        private class ProductRow
        {
            public GameObject controls;
            public Button unlockButton;
            public TextMeshProUGUI unitText;
            public TextMeshProUGUI boxText;
        }

        private readonly Dictionary<ProductData, ProductRow> productRows = new Dictionary<ProductData, ProductRow>();

        /// <summary>Карточка мебели или строка развития: кнопка и подписи, которые обновляются.</summary>
        private class ActionItem
        {
            public Button button;
            public TextMeshProUGUI note;    // цена / требование
            public Image stripe;            // цветная метка состояния (для строк развития)
            public TextMeshProUGUI title;
            public TextMeshProUGUI description;
        }

        private readonly Dictionary<FurnitureData, ActionItem> furnitureCards = new Dictionary<FurnitureData, ActionItem>();
        private readonly List<KeyValuePair<LayoutZone, int>> stageKeys = new List<KeyValuePair<LayoutZone, int>>();
        private readonly List<ActionItem> stageRows = new List<ActionItem>();
        private readonly Dictionary<StaffRole, ActionItem> staffRows = new Dictionary<StaffRole, ActionItem>();
        private ActionItem shelvesRow;
        private ActionItem cartRow;

        private GameObject root;
        private TextMeshProUGUI balanceText;
        private TextMeshProUGUI totalText;
        private Button orderButton;
        private TextMeshProUGUI levelText;
        private TextMeshProUGUI expensesText;
        private Image levelBar;

        private enum Tab { Products, Furniture, Development }

        private readonly List<GameObject> tabPanels = new List<GameObject>();
        private readonly List<Button> tabButtons = new List<Button>();

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

        private void OnMoneyChanged(int balance, int delta)
        {
            if (IsOpen) Refresh();
        }

        protected override void SetVisible(bool visible) => root.SetActive(visible);
        protected override void OnOpened() => Refresh();

        // ----------------------------------------------------------------
        // Корзина
        // ----------------------------------------------------------------

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
            cart.TryGetValue(product, out int current);
            cart[product] = Mathf.Clamp(current + delta, 0, maxBoxesPerProduct);
            quantityTexts[product].text = cart[product].ToString();
            Refresh();
        }

        private void ClearCart()
        {
            foreach (ProductData p in new List<ProductData>(cart.Keys))
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

        private void Unlock(ProductData product)
        {
            if (UnlockManager.Instance != null && UnlockManager.Instance.TryUnlock(product.category))
                Refresh();
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

        private void BuyExpansion(LayoutZone zone)
        {
            if (ExpansionManager.Instance != null) ExpansionManager.Instance.TryUpgrade(zone);
            Refresh();
        }

        private void ToggleStaff(StaffRole role)
        {
            StaffManager staff = StaffManager.Instance;
            if (staff == null) return;
            if (staff.IsHired(role)) staff.Fire(role);
            else staff.TryHire(role);
            Refresh();
        }

        // ----------------------------------------------------------------
        // Обновление
        // ----------------------------------------------------------------

        private static int Balance => EconomyManager.Instance != null ? EconomyManager.Instance.Balance : 0;

        private void Refresh()
        {
            int balance = Balance;
            int total = TotalCost;

            balanceText.text = $"Баланс: <b>{EconomyManager.Format(balance)}</b>";
            totalText.text = total > 0
                ? $"Итого: <b>{EconomyManager.Format(total)}</b>   ·   коробок: {TotalBoxes}"
                : "Выберите товар: «+» добавляет коробку в заказ";
            totalText.color = total > balance ? UIFactory.NegativeColor : UIFactory.TextColor;
            UIFactory.SetButtonState(orderButton, total > balance ? "Не хватает денег" : "Оформить заказ",
                total > 0 && total <= balance);

            RefreshProducts();
            RefreshFurniture();
            RefreshDevelopment();
        }

        private void RefreshProducts()
        {
            UnlockManager unlocks = UnlockManager.Instance;
            int level = LevelManager.CurrentLevel;

            foreach (KeyValuePair<ProductData, ProductRow> pair in productRows)
            {
                ProductData product = pair.Key;
                ProductRow row = pair.Value;

                row.unitText.text = $"{product.unitsPerBox} шт. × {product.EffectivePurchasePrice}";
                row.boxText.text = EconomyManager.Format(product.BoxCost);

                bool open = UnlockManager.IsUnlocked(product.category);
                row.controls.SetActive(open);
                row.unlockButton.gameObject.SetActive(!open);
                if (open) continue;

                CategoryUnlock rule = unlocks != null ? unlocks.GetUnlock(product.category) : null;
                if (rule == null) UIFactory.SetButtonState(row.unlockButton, "Недоступно", false);
                else if (level < rule.minLevel) UIFactory.SetButtonState(row.unlockButton, $"Уровень {rule.minLevel}", false);
                else UIFactory.SetButtonState(row.unlockButton, $"Открыть · {EconomyManager.Format(rule.cost)}",
                    balanceOk(rule.cost));
            }
        }

        private static bool balanceOk(int price) => Balance >= price;

        private void RefreshFurniture()
        {
            int level = LevelManager.CurrentLevel;
            foreach (KeyValuePair<FurnitureData, ActionItem> pair in furnitureCards)
            {
                FurnitureData f = pair.Key;
                ActionItem item = pair.Value;
                bool levelOk = level >= f.minLevel;

                item.note.text = levelOk ? EconomyManager.Format(f.price) : $"Нужен уровень салона {f.minLevel}";
                item.note.color = !levelOk ? UIFactory.MutedTextColor
                    : balanceOk(f.price) ? UIFactory.TextColor : UIFactory.NegativeColor;
                UIFactory.SetButtonState(item.button, levelOk ? "Купить" : "Закрыто", levelOk && balanceOk(f.price));
            }
        }

        private void RefreshDevelopment()
        {
            LevelManager lm = LevelManager.Instance;
            if (lm != null)
            {
                levelText.text = lm.IsMaxLevel
                    ? $"<b>Уровень салона {lm.Level}</b>   ·   максимальный   ·   мастерство {lm.MasteryProgress} из {lm.MasteryStep} (очко навыка)"
                    : $"<b>Уровень салона {lm.Level}</b>   ·   опыт {lm.Xp} из {lm.XpForNextLevel}";
                float fill = lm.IsMaxLevel || lm.XpForNextLevel <= 0 ? 1f : Mathf.Clamp01((float)lm.Xp / lm.XpForNextLevel);
                levelBar.rectTransform.anchorMax = new Vector2(fill, 1f);
            }

            int rent = DayManager.Instance != null ? DayManager.Instance.DailyRent : 0;
            int wages = DayManager.Instance != null ? DayManager.Instance.DailyWages : 0;
            expensesText.text = $"Расходы в день: аренда <b>{EconomyManager.Format(rent)}</b>" +
                                (wages > 0 ? $", зарплата <b>{EconomyManager.Format(wages)}</b>" : "");

            ExpansionManager expansion = ExpansionManager.Instance;
            int level = LevelManager.CurrentLevel;
            for (int i = 0; i < stageRows.Count; i++)
            {
                LayoutZone zone = stageKeys[i].Key;
                int stageIndex = stageKeys[i].Value;
                LayoutStage stage = LayoutPlan.Stages(zone)[stageIndex];
                ActionItem item = stageRows[i];
                int current = expansion != null ? expansion.StageOf(zone) : 0;

                if (stageIndex <= current)
                {
                    SetStageState(item, "Открыто", UIFactory.PositiveColor, false, "Открыто");
                }
                else if (stageIndex > current + 1)
                {
                    string previous = LayoutPlan.Stages(zone)[stageIndex - 1].name;
                    SetStageState(item, $"Сначала: {previous}", UIFactory.MutedTextColor, false, "Закрыто");
                }
                else if (level < stage.minLevel)
                {
                    SetStageState(item, $"Нужен уровень салона {stage.minLevel}", UIFactory.MutedTextColor, false,
                        "Закрыто");
                }
                else
                {
                    bool afford = balanceOk(stage.Price);
                    SetStageState(item, $"{EconomyManager.Format(stage.Price)}  ·  аренда +{EconomyManager.Format(stage.extraRent)}/день",
                        afford ? UIFactory.AccentColor : UIFactory.NegativeColor, afford, "Купить");
                }
            }

            RefreshEquipment(level);

            StaffManager staff = StaffManager.Instance;
            if (staff != null)
            {
                foreach (KeyValuePair<StaffRole, ActionItem> pair in staffRows)
                {
                    StaffRole role = pair.Key;
                    ActionItem row = pair.Value;
                    int wage = staff.WageOf(role);
                    int minLevel = StaffManager.Info(role).minLevel;
                    if (staff.IsHired(role))
                        SetStageState(row, $"Работает · {EconomyManager.Format(wage)} в день", UIFactory.PositiveColor, true, "Уволить");
                    else if (level < minLevel)
                        SetStageState(row, $"Нужен уровень салона {minLevel}", UIFactory.MutedTextColor, false, "Закрыто");
                    else
                        SetStageState(row, $"Зарплата {EconomyManager.Format(wage)} в день", UIFactory.AccentColor, true, "Нанять");
                }
            }
        }

        private void RefreshEquipment(int level)
        {
            UpgradeManager upgrades = UpgradeManager.Instance;
            if (upgrades == null || shelvesRow == null) return;

            UpgradeManager.ShelfUpgrade? next = upgrades.NextShelfUpgrade;
            if (next == null)
                SetStageState(shelvesRow, "Все ярусы открыты", UIFactory.PositiveColor, false, "Открыто");
            else
            {
                UpgradeManager.ShelfUpgrade u = next.Value;
                SetRowTexts(shelvesRow, u.name, u.description);
                if (level < u.minLevel)
                    SetStageState(shelvesRow, $"Нужен уровень салона {u.minLevel}", UIFactory.MutedTextColor, false, "Закрыто");
                else
                    SetStageState(shelvesRow, EconomyManager.Format(Core.GameDifficulty.ScalePrice(u.price)),
                        balanceOk(Core.GameDifficulty.ScalePrice(u.price)) ? UIFactory.AccentColor : UIFactory.NegativeColor, balanceOk(Core.GameDifficulty.ScalePrice(u.price)), "Купить");
            }

            if (upgrades.HasCart)
                SetStageState(cartRow, "Куплена — стоит там, где её оставили", UIFactory.PositiveColor, false, "Куплено");
            else if (level < upgrades.CartMinLevel)
                SetStageState(cartRow, $"Нужен уровень салона {upgrades.CartMinLevel}", UIFactory.MutedTextColor, false, "Закрыто");
            else
                SetStageState(cartRow, EconomyManager.Format(upgrades.CartPrice),
                    balanceOk(upgrades.CartPrice) ? UIFactory.AccentColor : UIFactory.NegativeColor,
                    balanceOk(upgrades.CartPrice), "Купить");
        }

        private static void SetRowTexts(ActionItem item, string title, string description)
        {
            if (item.title != null) item.title.text = title;
            if (item.description != null) item.description.text = description;
        }

        private void BuyShelfUpgrade()
        {
            if (UpgradeManager.Instance != null) UpgradeManager.Instance.TryUpgradeShelves();
            Refresh();
        }

        private void BuyCart()
        {
            if (UpgradeManager.Instance != null) UpgradeManager.Instance.TryBuyCart();
            Refresh();
        }

        private static void SetStageState(ActionItem item, string note, Color color, bool interactable, string label)
        {
            item.note.text = note;
            item.note.color = color == UIFactory.AccentColor ? UIFactory.TextColor : color;
            item.stripe.color = color;
            UIFactory.SetButtonState(item.button, label, interactable);
        }

        // ----------------------------------------------------------------
        // Построение окна
        // ----------------------------------------------------------------

        private void Build()
        {
            Canvas canvas = UIFactory.CreateCanvas("Order Canvas", 20, transform);
            root = canvas.gameObject;

            Image dim = UIFactory.CreatePanel("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.55f));
            UIFactory.Stretch(dim.rectTransform);

            Image window = UIFactory.CreatePanel("Window", canvas.transform, UIFactory.PanelColor);
            UIFactory.Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(WindowWidth, WindowHeight));
            UIFactory.AddVerticalLayout(window.gameObject, 16f, Padding);

            // Заголовок: название, баланс, закрыть.
            RectTransform header = UIFactory.CreateRow("Header", window.transform, 48f, 16f, 0);
            TextMeshProUGUI title = UIFactory.CreateText("Title", header, "Терминал салона", 32f, UIFactory.TextColor);
            title.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(title, flexibleWidth: 1f);
            balanceText = UIFactory.CreateText("Balance", header, "", 22f, UIFactory.TextColor,
                TextAlignmentOptions.MidlineRight);
            UIFactory.SetSize(balanceText, width: 320f);
            Button close = UIFactory.CreateButton("Close", header, "×", Close, UIFactory.ButtonColor, 34f);
            UIFactory.SetSize(close, width: UIFactory.ButtonHeight, height: UIFactory.ButtonHeight);

            // Вкладки — одинаковой ширины.
            RectTransform tabs = UIFactory.CreateRow("Tabs", window.transform, UIFactory.ButtonHeight, 8f, 0);
            AddTabButton(tabs, "Товары", Tab.Products);
            AddTabButton(tabs, "Мебель", Tab.Furniture);
            AddTabButton(tabs, "Развитие", Tab.Development);
            RectTransform tabsSpacer = UIFactory.CreateRect("Spacer", tabs);
            UIFactory.SetSize(tabsSpacer, flexibleWidth: 1f);
            TextMeshProUGUI hint = UIFactory.CreateText("Hint", tabs, "Esc — закрыть", 16f, UIFactory.MutedTextColor,
                TextAlignmentOptions.MidlineRight);
            UIFactory.SetSize(hint, width: 200f);

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
            Button b = UIFactory.CreateButton($"Tab_{tab}", parent, label, () => ShowTab(tab), UIFactory.ButtonColor, 22f);
            UIFactory.SetSize(b, width: 200f, height: UIFactory.ButtonHeight);
            tabButtons.Add(b);
        }

        private void ShowTab(Tab tab)
        {
            for (int i = 0; i < tabPanels.Count; i++)
            {
                tabPanels[i].SetActive(i == (int)tab);
                UIFactory.SetButtonColor(tabButtons[i], i == (int)tab ? UIFactory.AccentColor : UIFactory.ButtonColor);
            }
            Refresh();
        }

        // --- Товары: таблица ---

        private void BuildProductsPanel(RectTransform panel)
        {
            // Шапка таблицы — те же колонки, что и у строк.
            RectTransform head = UIFactory.CreateRow("TableHeader", panel, 28f, 16f, 16);
            UIFactory.SetSize(UIFactory.CreateRect("IconSpace", head), width: 6f);
            AddCell(head, "ТОВАР", 15f, UIFactory.MutedTextColor, -1f);
            AddCell(head, "КАТЕГОРИЯ", 15f, UIFactory.MutedTextColor, 190f);
            AddCell(head, "В КОРОБКЕ", 15f, UIFactory.MutedTextColor, 170f);
            AddCell(head, "ЦЕНА КОРОБКИ", 15f, UIFactory.MutedTextColor, 160f);
            AddCell(head, "КОРОБОК", 15f, UIFactory.MutedTextColor, QtyColumn, TextAlignmentOptions.Center);

            RectTransform list = UIFactory.CreateScrollList("Rows", panel, 6f);
            List<ProductData> products = catalog != null ? catalog.products : new List<ProductData>();
            foreach (ProductData product in products)
            {
                if (product == null) continue;
                cart[product] = 0;
                CreateProductRow(list, product);
            }

            // Итог и кнопки заказа.
            Image divider = UIFactory.CreatePanel("Divider", panel, new Color(1f, 1f, 1f, 0.08f));
            UIFactory.SetSize(divider, height: 1f);

            RectTransform footer = UIFactory.CreateRow("Footer", panel, 60f, 12f, 0);
            totalText = UIFactory.CreateText("Total", footer, "", 24f, UIFactory.TextColor);
            UIFactory.SetSize(totalText, flexibleWidth: 1f);

            Button clear = UIFactory.CreateButton("Clear", footer, "Очистить", ClearCart, UIFactory.ButtonColor, 22f);
            UIFactory.SetSize(clear, width: 180f, height: UIFactory.ButtonHeight);
            orderButton = UIFactory.CreateButton("Order", footer, "Оформить заказ", PlaceOrder, UIFactory.AccentColor, 22f);
            UIFactory.SetSize(orderButton, width: ActionWidth, height: UIFactory.ButtonHeight);
        }

        private void CreateProductRow(Transform parent, ProductData product)
        {
            RectTransform row = UIFactory.CreateRow($"Row_{product.id}", parent, UIFactory.RowHeight, 16f, 16,
                UIFactory.RowColor);

            // Цветная метка товара (совпадает с цветом товара на полке).
            Image icon = UIFactory.CreatePanel("Icon", row, product.color);
            UIFactory.SetSize(icon, width: 6f, height: 32f);

            AddCell(row, product.displayName, 21f, UIFactory.TextColor, -1f);
            AddCell(row, product.category.ToDisplayName(), 18f, UIFactory.MutedTextColor, 190f);
            TextMeshProUGUI unit = AddCell(row, "", 18f, UIFactory.MutedTextColor, 170f);
            TextMeshProUGUI box = AddCell(row, "", 21f, UIFactory.TextColor, 160f);

            // Ячейка «количество»: либо − 0 +, либо кнопка открытия категории — одинаковой ширины.
            RectTransform cell = UIFactory.CreateRect("QtyCell", row);
            UIFactory.SetSize(cell, width: QtyColumn, height: UIFactory.ButtonHeight);

            RectTransform controls = UIFactory.CreateRow("Controls", cell, UIFactory.ButtonHeight, 8f, 0);
            UIFactory.Stretch(controls);
            controls.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

            Button minus = UIFactory.CreateButton("Minus", controls, "−", () => ChangeQuantity(product, -1),
                UIFactory.ButtonColor, 28f);
            UIFactory.SetSize(minus, width: UIFactory.ButtonHeight, height: UIFactory.ButtonHeight);
            TextMeshProUGUI qty = UIFactory.CreateText("Qty", controls, "0", 24f, UIFactory.TextColor,
                TextAlignmentOptions.Center);
            UIFactory.SetSize(qty, width: 64f, height: UIFactory.ButtonHeight);
            quantityTexts[product] = qty;
            Button plus = UIFactory.CreateButton("Plus", controls, "+", () => ChangeQuantity(product, 1),
                UIFactory.ButtonColor, 28f);
            UIFactory.SetSize(plus, width: UIFactory.ButtonHeight, height: UIFactory.ButtonHeight);

            Button unlock = UIFactory.CreateButton("Unlock", cell, "", () => Unlock(product), UIFactory.AccentColor, 18f);
            UIFactory.Stretch(unlock.GetComponent<RectTransform>());

            productRows[product] = new ProductRow
            {
                controls = controls.gameObject,
                unlockButton = unlock,
                unitText = unit,
                boxText = box
            };
        }

        /// <summary>Ячейка таблицы; width &lt; 0 — растягивается на свободное место.</summary>
        private static TextMeshProUGUI AddCell(Transform row, string text, float size, Color color, float width,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            TextMeshProUGUI cell = UIFactory.CreateText("Cell", row, text, size, color, alignment);
            cell.textWrappingMode = TextWrappingModes.NoWrap;
            cell.overflowMode = TextOverflowModes.Ellipsis;
            if (width >= 0f) UIFactory.SetSize(cell, width: width);
            else UIFactory.SetSize(cell, flexibleWidth: 1f);
            return cell;
        }

        // --- Мебель: сетка карточек ---

        private void BuildFurniturePanel(RectTransform panel)
        {
            TextMeshProUGUI hint = UIFactory.CreateText("Hint", panel,
                "Мебель появляется перед вами: прицел — место, R — повернуть, ЛКМ — поставить. " +
                "Переставить стоящую мебель — E по каркасу.",
                18f, UIFactory.MutedTextColor);
            UIFactory.SetSize(hint, height: 30f);

            if (furnitureCatalog == null)
            {
                TextMeshProUGUI missing = UIFactory.CreateText("Missing", panel,
                    "Каталог мебели не подключён: выполните «На связи → Подготовить сцену (итерация 8)».",
                    20f, UIFactory.NegativeColor);
                UIFactory.SetSize(missing, height: 40f);
                return;
            }

            float width = WindowWidth - Padding * 2;
            const float spacing = 14f;
            Vector2 cell = new Vector2(Mathf.Floor((width - spacing * 2f) / 3f), CardHeight);
            RectTransform grid = UIFactory.CreateScrollList("Cards", panel, spacing, 3, cell);

            foreach (FurnitureData f in furnitureCatalog.items)
            {
                if (f == null) continue;
                FurnitureData data = f;
                furnitureCards[f] = CreateCard(grid, f.displayName, f.description, () => BuyFurniture(data));
            }
        }

        private static ActionItem CreateCard(Transform parent, string title, string description, System.Action onClick)
        {
            Image card = UIFactory.CreatePanel($"Card_{title}", parent, UIFactory.RowColor);
            UIFactory.AddVerticalLayout(card.gameObject, 6f, 18);

            TextMeshProUGUI name = UIFactory.CreateText("Title", card.transform, title, 22f, UIFactory.TextColor);
            name.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(name, height: 30f);

            TextMeshProUGUI desc = UIFactory.CreateText("Desc", card.transform, description, 16f,
                UIFactory.MutedTextColor, TextAlignmentOptions.TopLeft);
            desc.overflowMode = TextOverflowModes.Ellipsis;
            UIFactory.SetSize(desc, flexibleHeight: 1f);

            RectTransform bottom = UIFactory.CreateRow("Bottom", card.transform, UIFactory.ButtonHeight, 12f, 0);
            TextMeshProUGUI note = UIFactory.CreateText("Price", bottom, "", 20f, UIFactory.TextColor);
            note.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(note, flexibleWidth: 1f);
            Button button = UIFactory.CreateButton("Buy", bottom, "Купить", onClick, UIFactory.AccentColor, 20f);
            UIFactory.SetSize(button, width: 150f, height: UIFactory.ButtonHeight);

            return new ActionItem { button = button, note = note };
        }

        // --- Развитие: уровень, расширения, персонал ---

        private void BuildDevelopmentPanel(RectTransform panel)
        {
            RectTransform list = UIFactory.CreateScrollList("List", panel, 8f);

            // Сводка: уровень с полосой опыта и расходы.
            Image summary = UIFactory.CreatePanel("Summary", list, UIFactory.RowColor);
            UIFactory.AddVerticalLayout(summary.gameObject, 8f, 18);
            UIFactory.SetSize(summary, height: 112f);

            levelText = UIFactory.CreateText("Level", summary.transform, "", 22f, UIFactory.TextColor);
            UIFactory.SetSize(levelText, height: 28f);

            Image barBack = UIFactory.CreatePanel("Bar", summary.transform, new Color(1f, 1f, 1f, 0.08f));
            UIFactory.SetSize(barBack, height: 6f);
            levelBar = UIFactory.CreatePanel("Fill", barBack.transform, UIFactory.AccentColor);
            levelBar.rectTransform.anchorMin = Vector2.zero;
            levelBar.rectTransform.anchorMax = new Vector2(0f, 1f);
            levelBar.rectTransform.offsetMin = Vector2.zero;
            levelBar.rectTransform.offsetMax = Vector2.zero;

            expensesText = UIFactory.CreateText("Expenses", summary.transform, "", 18f, UIFactory.MutedTextColor);
            UIFactory.SetSize(expensesText, height: 26f);

            AddSection(list, "ТОРГОВЫЙ ЗАЛ", "Сначала зал расширяется дважды, затем открывается второй зал за аркой.");
            for (int i = 1; i < LayoutPlan.Salon.Length; i++)
                AddStageRow(list, LayoutZone.Salon, i);

            AddSection(list, "СКЛАД", "Больше места под складские стеллажи и коробки.");
            for (int i = 1; i < LayoutPlan.Storage.Length; i++)
                AddStageRow(list, LayoutZone.Storage, i);

            AddSection(list, "ОБОРУДОВАНИЕ", "Улучшения, которые экономят время.");
            shelvesRow = CreateDevRow(list, UpgradeManager.ShelfUpgrades[0].name, UpgradeManager.ShelfUpgrades[0].description,
                BuyShelfUpgrade);
            cartRow = CreateDevRow(list, "Тележка для коробок",
                "До 8 коробок за раз: E — катить, ЛКМ с коробкой — поставить на тележку.", BuyCart);

            AddSection(list, "ПЕРСОНАЛ", "Сотрудники работают сами; навыки ветки «Персонал» (K) их улучшают.");
            foreach (StaffManager.RoleInfo info in StaffManager.Roles)
            {
                StaffRole role = info.role;
                staffRows[role] = CreateDevRow(list, info.name, info.description, () => ToggleStaff(role));
            }
        }

        private static void AddSection(Transform parent, string title, string subtitle)
        {
            RectTransform row = UIFactory.CreateRow($"Section_{title}", parent, 44f, 16f, 4);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.LowerLeft;
            TextMeshProUGUI t = UIFactory.CreateText("Title", row, title, 16f, UIFactory.AccentColor);
            t.fontStyle = FontStyles.Bold;
            t.characterSpacing = 4f;
            UIFactory.SetSize(t, width: 220f, height: 26f);
            TextMeshProUGUI s = UIFactory.CreateText("Subtitle", row, subtitle, 16f, UIFactory.MutedTextColor);
            UIFactory.SetSize(s, flexibleWidth: 1f);
            s.alignment = TextAlignmentOptions.BottomLeft;
        }

        private void AddStageRow(Transform parent, LayoutZone zone, int index)
        {
            LayoutStage stage = LayoutPlan.Stages(zone)[index];
            stageKeys.Add(new KeyValuePair<LayoutZone, int>(zone, index));
            stageRows.Add(CreateDevRow(parent, stage.name, stage.description, () => BuyExpansion(zone)));
        }

        /// <summary>Строка развития: цветная метка, название с описанием, состояние и кнопка.</summary>
        private static ActionItem CreateDevRow(Transform parent, string title, string description, System.Action onClick)
        {
            RectTransform row = UIFactory.CreateRow($"Dev_{title}", parent, 76f, 18f, 0, UIFactory.RowColor);
            row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 18, 0, 0);

            Image stripe = UIFactory.CreatePanel("Stripe", row, UIFactory.MutedTextColor);
            UIFactory.SetSize(stripe, width: 6f, height: 76f);

            RectTransform texts = UIFactory.CreateRect("Texts", row);
            UIFactory.AddVerticalLayout(texts.gameObject, 2f, 0).childAlignment = TextAnchor.MiddleLeft;
            UIFactory.SetSize(texts, flexibleWidth: 1f, height: 60f);
            TextMeshProUGUI name = UIFactory.CreateText("Title", texts, title, 21f, UIFactory.TextColor);
            name.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(name, height: 28f);
            TextMeshProUGUI desc = UIFactory.CreateText("Desc", texts, description, 16f, UIFactory.MutedTextColor);
            desc.textWrappingMode = TextWrappingModes.NoWrap;
            desc.overflowMode = TextOverflowModes.Ellipsis;
            UIFactory.SetSize(desc, height: 24f);

            TextMeshProUGUI note = UIFactory.CreateText("State", row, "", 17f, UIFactory.TextColor,
                TextAlignmentOptions.MidlineRight);
            note.enableAutoSizing = true;
            note.fontSizeMin = 13f;
            note.fontSizeMax = 17f;
            UIFactory.SetSize(note, width: 330f);

            Button button = UIFactory.CreateButton("Action", row, "", onClick, UIFactory.AccentColor, 20f);
            UIFactory.SetSize(button, width: 150f, height: UIFactory.ButtonHeight);

            return new ActionItem { button = button, note = note, stripe = stripe, title = name, description = desc };
        }
    }
}
