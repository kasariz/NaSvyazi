using NaSvyazi.Economy;
using NaSvyazi.Player;
using NaSvyazi.Products;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NaSvyazi.UI
{
    /// <summary>
    /// Окно установки цены товара. Показывает закупочную и рыночную цену,
    /// наценку, прибыль с единицы и ожидаемую вероятность покупки
    /// (по модели спроса DemandModel), чтобы игрок видел последствия решения.
    /// </summary>
    public class PriceUI : ModalWindow
    {
        public static PriceUI Instance { get; private set; }

        private GameObject root;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI infoText;
        private TextMeshProUGUI priceText;
        private TextMeshProUGUI marginText;
        private TextMeshProUGUI demandText;

        private ProductData product;

        private void Awake()
        {
            Instance = this;
            Build();
            SetVisible(false);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this) Instance = null;
        }

        /// <summary>Открыть окно для товара.</summary>
        public void OpenFor(ProductData target, PlayerController player)
        {
            if (target == null) return;
            product = target;
            Open(player);
        }

        protected override void SetVisible(bool visible) => root.SetActive(visible);
        protected override void OnOpened()
        {
            Refresh();
            Core.GameEvents.RaisePriceWindowOpened();
        }

        private int CurrentPrice => PriceManager.PriceOf(product);

        private void ChangePrice(int delta) => SetPrice(CurrentPrice + delta);

        private void SetPrice(int price)
        {
            if (PriceManager.Instance == null || product == null) return;
            PriceManager.Instance.SetPrice(product, price);
            Refresh();
        }

        private void Refresh()
        {
            if (product == null) return;

            int price = CurrentPrice;
            int profit = price - product.purchasePrice;
            float margin = product.purchasePrice > 0 ? (float)profit / product.purchasePrice * 100f : 0f;
            float chance = DemandModel.AverageBuyProbability(price, product.marketPrice);

            titleText.text = product.displayName;
            infoText.text = $"Закупка: {EconomyManager.Format(product.purchasePrice)}    ·    " +
                            $"Рыночная цена: {EconomyManager.Format(product.marketPrice)}";
            priceText.text = EconomyManager.Format(price);

            marginText.text = $"Наценка: {margin:0} %    ·    Прибыль с 1 шт.: {EconomyManager.Format(profit)}";
            marginText.color = profit > 0 ? UIFactory.TextColor : UIFactory.NegativeColor;

            string verdict = chance >= 0.8f ? "охотно купят" : chance >= 0.4f ? "купят не все" : "почти никто не купит";
            demandText.text = $"Вероятность покупки: {chance * 100f:0} % — {verdict}";
            demandText.color = chance >= 0.8f ? UIFactory.PositiveColor
                : chance >= 0.4f ? new Color(0.95f, 0.8f, 0.3f) : UIFactory.NegativeColor;
        }

        private void Build()
        {
            Canvas canvas = UIFactory.CreateCanvas("Price Canvas", 20, transform);
            root = canvas.gameObject;

            Image dim = UIFactory.CreatePanel("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.45f));
            UIFactory.Stretch(dim.rectTransform);

            Image window = UIFactory.CreatePanel("Window", canvas.transform, UIFactory.PanelColor);
            UIFactory.Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(820f, 560f));
            UIFactory.AddVerticalLayout(window.gameObject, 12f, 32);

            titleText = UIFactory.CreateText("Title", window.transform, "", 34f, UIFactory.TextColor);
            titleText.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(titleText, height: 48f);

            infoText = UIFactory.CreateText("Info", window.transform, "", 22f, UIFactory.MutedTextColor);
            UIFactory.SetSize(infoText, height: 34f);

            priceText = UIFactory.CreateText("Price", window.transform, "", 64f, UIFactory.TextColor,
                TextAlignmentOptions.Center);
            priceText.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(priceText, height: 96f);

            RectTransform buttons = UIFactory.CreateRect("Buttons", window.transform);
            HorizontalLayoutGroup layout = UIFactory.AddHorizontalLayout(buttons.gameObject, 10f, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            UIFactory.SetSize(buttons, height: 60f);

            AddPriceButton(buttons, "−100", -100);
            AddPriceButton(buttons, "−10", -10);
            AddPriceButton(buttons, "+10", 10);
            AddPriceButton(buttons, "+100", 100);
            UIFactory.SetSize(UIFactory.CreateButton("Market", buttons, "По рынку",
                () => SetPrice(product.marketPrice), UIFactory.ButtonColor), width: 180f);

            marginText = UIFactory.CreateText("Margin", window.transform, "", 24f, UIFactory.TextColor,
                TextAlignmentOptions.Center);
            UIFactory.SetSize(marginText, height: 40f);

            demandText = UIFactory.CreateText("Demand", window.transform, "", 24f, UIFactory.TextColor,
                TextAlignmentOptions.Center);
            UIFactory.SetSize(demandText, height: 40f);

            RectTransform spacer = UIFactory.CreateRect("Spacer", window.transform);
            UIFactory.SetSize(spacer, flexibleHeight: 1f);

            Button done = UIFactory.CreateButton("Done", window.transform, "Готово (Esc)", Close,
                UIFactory.AccentColor);
            UIFactory.SetSize(done, height: 60f);
        }

        private void AddPriceButton(Transform parent, string label, int delta)
        {
            Button b = UIFactory.CreateButton($"Btn{label}", parent, label, () => ChangePrice(delta),
                UIFactory.ButtonColor, 26f);
            UIFactory.SetSize(b, width: 110f);
        }
    }
}
