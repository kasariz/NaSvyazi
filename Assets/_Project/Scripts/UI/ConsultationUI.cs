using System;
using System.Collections.Generic;
using NaSvyazi.Economy;
using NaSvyazi.Player;
using NaSvyazi.Progression;
using NaSvyazi.Tariffs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NaSvyazi.UI
{
    /// <summary>
    /// Окно консультации: запрос клиента и карточки тарифов.
    /// Игрок выбирает тариф, который предложит клиенту. Лучший выбор —
    /// самый дешёвый тариф, полностью закрывающий потребность (см. TariffCatalog).
    /// </summary>
    public class ConsultationUI : ModalWindow
    {
        private GameObject root;
        private TextMeshProUGUI requestText;
        private RectTransform cardsRow;
        private readonly List<GameObject> cards = new List<GameObject>();

        private Action<TariffData> onChosen;

        private void Awake()
        {
            Build();
            SetVisible(false);
        }

        public void OpenFor(TariffNeed need, TariffCatalog catalog, Action<TariffData> callback, PlayerController player)
        {
            onChosen = callback;
            requestText.text = need != null ? $"«{need.request}»" : "«Подберите мне тариф»";
            // Навык «Знаток тарифов» подсвечивает самый выгодный для клиента тариф.
            TariffData hint = SkillManager.TariffExpert && catalog != null && need != null ? catalog.BestFor(need) : null;
            RebuildCards(catalog, hint);
            Open(player);
        }

        protected override void SetVisible(bool visible) => root.SetActive(visible);

        private void Choose(TariffData tariff)
        {
            Action<TariffData> callback = onChosen;
            onChosen = null;
            Close();
            callback?.Invoke(tariff);
        }

        private void RebuildCards(TariffCatalog catalog, TariffData hint)
        {
            foreach (GameObject c in cards) Destroy(c);
            cards.Clear();
            if (catalog == null) return;

            foreach (TariffData tariff in catalog.tariffs)
            {
                if (tariff == null) continue;
                cards.Add(CreateCard(tariff, tariff == hint));
            }
        }

        private GameObject CreateCard(TariffData tariff, bool recommended)
        {
            Image card = UIFactory.CreatePanel($"Card_{tariff.id}", cardsRow,
                recommended ? new Color(0.86f, 0.18f, 0.55f, 0.28f) : new Color(1f, 1f, 1f, 0.06f));
            UIFactory.AddVerticalLayout(card.gameObject, 8f, 18);
            UIFactory.SetSize(card, width: 250f);

            TextMeshProUGUI name = UIFactory.CreateText("Name", card.transform, tariff.displayName, 28f,
                UIFactory.TextColor, TextAlignmentOptions.Center);
            name.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(name, height: 44f);

            TextMeshProUGUI fee = UIFactory.CreateText("Fee", card.transform,
                $"{EconomyManager.Format(tariff.monthlyFee)}/мес", 26f, UIFactory.AccentColor,
                TextAlignmentOptions.Center);
            UIFactory.SetSize(fee, height: 40f);

            TextMeshProUGUI internet = UIFactory.CreateText("Internet", card.transform, tariff.InternetText, 21f,
                UIFactory.TextColor, TextAlignmentOptions.Center);
            FitText(internet, 14f);
            UIFactory.SetSize(internet, height: 34f);

            TextMeshProUGUI minutes = UIFactory.CreateText("Minutes", card.transform, tariff.MinutesText, 21f,
                UIFactory.TextColor, TextAlignmentOptions.Center);
            UIFactory.SetSize(minutes, height: 32f);

            TextMeshProUGUI desc = UIFactory.CreateText("Desc", card.transform, tariff.description, 17f,
                UIFactory.MutedTextColor, TextAlignmentOptions.Top);
            FitText(desc, 12f);
            UIFactory.SetSize(desc, height: 110f, flexibleHeight: 1f);

            Button offer = UIFactory.CreateButton("Offer", card.transform, recommended ? "★ Предложить" : "Предложить",
                () => Choose(tariff), recommended ? UIFactory.AccentColor : UIFactory.ButtonColor);
            UIFactory.SetSize(offer, height: 52f);

            return card.gameObject;
        }

        /// <summary>Уменьшать шрифт, если текст не помещается в отведённое место.</summary>
        private static void FitText(TextMeshProUGUI text, float minSize)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = minSize;
            text.fontSizeMax = text.fontSize;
        }

        private void Build()
        {
            Canvas canvas = UIFactory.CreateCanvas("Consultation Canvas", 20, transform);
            root = canvas.gameObject;

            Image dim = UIFactory.CreatePanel("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.5f));
            UIFactory.Stretch(dim.rectTransform);

            Image window = UIFactory.CreatePanel("Window", canvas.transform, UIFactory.PanelColor);
            UIFactory.Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1440f, 900f));
            UIFactory.AddVerticalLayout(window.gameObject, 14f, 32);

            TextMeshProUGUI title = UIFactory.CreateText("Title", window.transform, "Консультация по тарифу", 34f,
                UIFactory.TextColor);
            title.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(title, height: 48f);

            TextMeshProUGUI hint = UIFactory.CreateText("Hint", window.transform,
                "Клиент говорит:", 22f, UIFactory.MutedTextColor);
            UIFactory.SetSize(hint, height: 30f);

            requestText = UIFactory.CreateText("Request", window.transform, "", 30f, UIFactory.TextColor);
            requestText.fontStyle = FontStyles.Italic;
            FitText(requestText, 20f);
            UIFactory.SetSize(requestText, height: 80f);

            TextMeshProUGUI rule = UIFactory.CreateText("Rule", window.transform,
                "Подберите тариф, который закрывает потребность клиента и не заставляет переплачивать. " +
                "Для подключения нужен стартовый пакет SIM на полке.",
                20f, UIFactory.MutedTextColor);
            UIFactory.SetSize(rule, height: 34f);

            cardsRow = UIFactory.CreateRect("Cards", window.transform);
            HorizontalLayoutGroup layout = UIFactory.AddHorizontalLayout(cardsRow.gameObject, 14f, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            UIFactory.SetSize(cardsRow, flexibleHeight: 1f);

            RectTransform footer = UIFactory.CreateRect("Footer", window.transform);
            UIFactory.AddHorizontalLayout(footer.gameObject, 12f, 0);
            UIFactory.SetSize(footer, height: 56f);

            RectTransform footerSpacer = UIFactory.CreateRect("FooterSpacer", footer);
            UIFactory.SetSize(footerSpacer, flexibleWidth: 1f);
            UIFactory.SetSize(UIFactory.CreateButton("Close", footer, "Отложить (Esc)", Close,
                UIFactory.ButtonColor), width: 240f);
        }
    }
}
