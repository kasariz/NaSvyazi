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
    /// Консультация — конструктор тарифа. Слева клиент рассказывает, что ему нужно, и собирается тариф:
    /// пакет интернета, пакет минут (кнопки − / +) и опции (переключатели). Справа — итоговая цена,
    /// шкала довольства клиента в процентах и подсказки, что не так («не хватает минут», «переплата»).
    /// Чем точнее тариф под запрос, тем выше довольство и репутация; дороже — больше выручка,
    /// но довольство падает (см. TariffBuilder).
    /// </summary>
    public class ConsultationUI : ModalWindow
    {
        private static readonly Color Good = new Color(0.35f, 0.85f, 0.45f);
        private static readonly Color Okay = new Color(0.98f, 0.75f, 0.25f);
        private static readonly Color Bad = new Color(0.95f, 0.35f, 0.35f);

        private GameObject root;
        private TextMeshProUGUI requestText;
        private TextMeshProUGUI expertText;
        private TextMeshProUGUI gbValue, gbPrice, minValue, minPrice;
        private readonly Dictionary<TariffAddon, Button> addonButtons = new Dictionary<TariffAddon, Button>();
        private TextMeshProUGUI feeText;
        private TextMeshProUGUI satisfactionText;
        private TextMeshProUGUI verdictText;
        private Image satisfactionFill;
        private TextMeshProUGUI remarksText;
        private TextMeshProUGUI breakdownText;

        private TariffNeed need;
        private TariffPlan plan;
        private Action<TariffPlan> onChosen;

        private void Awake()
        {
            Build();
            SetVisible(false);
        }

        public void OpenFor(TariffNeed clientNeed, Action<TariffPlan> callback, PlayerController player)
        {
            need = clientNeed ?? new TariffNeed { request = "Подберите мне что-нибудь недорогое", minGigabytes = 5, minMinutes = 100 };
            onChosen = callback;
            plan = new TariffPlan { gbStep = 2, minutesStep = 2 }; // стартовая заготовка: 10 ГБ, 300 мин

            requestText.text = $"«{need.request}»";
            // Навык «Знаток тарифов» раскрывает точные потребности клиента.
            expertText.gameObject.SetActive(SkillManager.TariffExpert);
            expertText.text = "Знаток тарифов: " + TariffBuilder.Describe(need);

            Refresh();
            Open(player);
        }

        protected override void SetVisible(bool visible) => root.SetActive(visible);

        // --- Изменение тарифа ---

        private void StepGb(int delta)
        {
            plan.gbStep = Mathf.Clamp(plan.gbStep + delta, 0, TariffBuilder.GbSteps.Length - 1);
            Refresh();
        }

        private void StepMinutes(int delta)
        {
            plan.minutesStep = Mathf.Clamp(plan.minutesStep + delta, 0, TariffBuilder.MinuteSteps.Length - 1);
            Refresh();
        }

        private void ToggleAddon(TariffAddon addon)
        {
            plan.addons ^= addon;
            Refresh();
        }

        private void Offer()
        {
            Action<TariffPlan> callback = onChosen;
            onChosen = null;
            Close();
            callback?.Invoke(plan);
        }

        private void Refresh()
        {
            gbValue.text = TariffBuilder.GbText(plan.Gigabytes);
            gbPrice.text = $"+{TariffBuilder.GbPrices[plan.gbStep]} руб.";
            minValue.text = TariffBuilder.MinutesText(plan.Minutes);
            minPrice.text = $"+{TariffBuilder.MinutePrices[plan.minutesStep]} руб.";

            foreach (KeyValuePair<TariffAddon, Button> pair in addonButtons)
            {
                bool on = plan.Has(pair.Key);
                UIFactory.SetButtonColor(pair.Value, on ? UIFactory.AccentColor : UIFactory.ButtonColor);
                pair.Value.GetComponentInChildren<TextMeshProUGUI>().text =
                    $"{(on ? "• " : "")}{TariffBuilder.AddonName(pair.Key)}\n<size=75%>+{TariffBuilder.AddonPrice(pair.Key)} руб./мес</size>";
            }

            int fee = plan.MonthlyFee;
            feeText.text = $"{fee} руб./мес";
            breakdownText.text = $"База {TariffBuilder.BaseFee} руб. + интернет + минуты + опции.\nКлиент платит за первый месяц и SIM-карту.";

            TariffEvaluation e = TariffBuilder.Evaluate(need, plan);
            Color c = e.result == ConsultationResult.Best ? Good : e.result == ConsultationResult.Acceptable ? Okay : Bad;
            satisfactionText.text = $"{e.satisfaction}%";
            satisfactionText.color = c;
            satisfactionFill.color = c;
            satisfactionFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(e.satisfaction / 100f), 1f);
            verdictText.text = e.result == ConsultationResult.Best ? "Клиент будет в восторге"
                : e.result == ConsultationResult.Acceptable ? "Подключит, но без радости"
                : "Клиент откажется";
            verdictText.color = c;
            remarksText.text = "• " + string.Join("\n• ", e.remarks);
        }

        // ----------------------------------------------------------------
        // Построение окна
        // ----------------------------------------------------------------

        private void Build()
        {
            Canvas canvas = UIFactory.CreateCanvas("Consultation Canvas", 20, transform);
            root = canvas.gameObject;

            Image dim = UIFactory.CreatePanel("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.6f));
            UIFactory.Stretch(dim.rectTransform);

            Image window = UIFactory.CreatePanel("Window", canvas.transform, UIFactory.PanelColor);
            UIFactory.Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1400f, 860f));
            UIFactory.AddVerticalLayout(window.gameObject, 12f, 32);

            TextMeshProUGUI title = UIFactory.CreateText("Title", window.transform, "Подбор тарифа", 34f, UIFactory.TextColor);
            title.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(title, height: 46f);

            // Реплика клиента — «облачко».
            Image bubble = UIFactory.CreatePanel("Request", window.transform, new Color(1f, 1f, 1f, 0.07f));
            UIFactory.AddVerticalLayout(bubble.gameObject, 4f, 16);
            UIFactory.SetSize(bubble, height: 120f);
            TextMeshProUGUI who = UIFactory.CreateText("Who", bubble.transform, "Клиент:", 18f, UIFactory.MutedTextColor);
            UIFactory.SetSize(who, height: 24f);
            requestText = UIFactory.CreateText("Text", bubble.transform, "", 28f, UIFactory.TextColor);
            requestText.fontStyle = FontStyles.Italic;
            requestText.enableAutoSizing = true;
            requestText.fontSizeMin = 18f;
            requestText.fontSizeMax = 28f;
            UIFactory.SetSize(requestText, height: 40f);
            expertText = UIFactory.CreateText("Expert", bubble.transform, "", 17f, UIFactory.AccentColor);
            UIFactory.SetSize(expertText, height: 22f);

            RectTransform columns = UIFactory.CreateRect("Columns", window.transform);
            HorizontalLayoutGroup cols = UIFactory.AddHorizontalLayout(columns.gameObject, 24f, 0);
            cols.childForceExpandHeight = true;
            UIFactory.SetSize(columns, flexibleHeight: 1f);

            BuildConstructor(columns);
            BuildSummary(columns);

            RectTransform footer = UIFactory.CreateRect("Footer", window.transform);
            UIFactory.AddHorizontalLayout(footer.gameObject, 12f, 0);
            UIFactory.SetSize(footer, height: 60f);
            UIFactory.SetSize(UIFactory.CreateButton("Close", footer, "Отложить (Esc)", Close, UIFactory.ButtonColor, 22f),
                width: 240f);
            RectTransform spacer = UIFactory.CreateRect("Spacer", footer);
            UIFactory.SetSize(spacer, flexibleWidth: 1f);
            UIFactory.SetSize(UIFactory.CreateButton("Offer", footer, "Предложить тариф", Offer, UIFactory.AccentColor, 24f),
                width: 340f);
        }

        /// <summary>Левая колонка: пакеты интернета и минут, опции.</summary>
        private void BuildConstructor(Transform parent)
        {
            RectTransform left = UIFactory.CreateRect("Constructor", parent);
            UIFactory.AddVerticalLayout(left.gameObject, 12f, 0);
            UIFactory.SetSize(left, flexibleWidth: 1f);

            Section(left, "ПАКЕТЫ");
            BuildStepper(left, "Интернет", () => StepGb(-1), () => StepGb(1), out gbValue, out gbPrice);
            BuildStepper(left, "Минуты", () => StepMinutes(-1), () => StepMinutes(1), out minValue, out minPrice);

            Section(left, "ОПЦИИ");
            RectTransform grid = UIFactory.CreateRect("Addons", left);
            GridLayoutGroup g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(370f, 78f);
            g.spacing = new Vector2(12f, 12f);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 2;
            UIFactory.SetSize(grid, height: 168f);

            foreach (TariffAddon addon in TariffBuilder.Addons)
            {
                TariffAddon a = addon;
                Button b = UIFactory.CreateButton($"Addon_{a}", grid, "", () => ToggleAddon(a), UIFactory.ButtonColor, 20f);
                TextMeshProUGUI label = b.GetComponentInChildren<TextMeshProUGUI>();
                label.enableAutoSizing = true;
                label.fontSizeMin = 14f;
                label.fontSizeMax = 20f;
                addonButtons[a] = b;
            }

            RectTransform fill = UIFactory.CreateRect("Fill", left);
            UIFactory.SetSize(fill, flexibleHeight: 1f);
        }

        private static void Section(Transform parent, string title)
        {
            TextMeshProUGUI t = UIFactory.CreateText("Section", parent, title, 16f, UIFactory.AccentColor);
            t.fontStyle = FontStyles.Bold;
            t.characterSpacing = 4f;
            UIFactory.SetSize(t, height: 24f);
        }

        /// <summary>Строка «Название   [−]  значение  [+]   цена».</summary>
        private static void BuildStepper(Transform parent, string title, Action minus, Action plus,
            out TextMeshProUGUI value, out TextMeshProUGUI price)
        {
            RectTransform row = UIFactory.CreateRow($"Stepper_{title}", parent, 72f, 14f, 18, UIFactory.RowColor);

            TextMeshProUGUI name = UIFactory.CreateText("Name", row, title, 24f, UIFactory.TextColor);
            name.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(name, width: 170f, height: 40f);

            UIFactory.SetSize(UIFactory.CreateButton("Minus", row, "–", minus, UIFactory.ButtonColor, 30f), width: 60f, height: 52f);
            value = UIFactory.CreateText("Value", row, "", 26f, UIFactory.TextColor, TextAlignmentOptions.Center);
            UIFactory.SetSize(value, width: 200f, height: 40f);
            UIFactory.SetSize(UIFactory.CreateButton("Plus", row, "+", plus, UIFactory.ButtonColor, 30f), width: 60f, height: 52f);

            RectTransform spacer = UIFactory.CreateRect("Spacer", row);
            UIFactory.SetSize(spacer, flexibleWidth: 1f);
            price = UIFactory.CreateText("Price", row, "", 20f, UIFactory.MutedTextColor, TextAlignmentOptions.MidlineRight);
            UIFactory.SetSize(price, width: 110f, height: 40f);
        }

        /// <summary>Правая колонка: цена, довольство клиента и подсказки.</summary>
        private void BuildSummary(Transform parent)
        {
            Image right = UIFactory.CreatePanel("Summary", parent, new Color(1f, 1f, 1f, 0.05f));
            UIFactory.AddVerticalLayout(right.gameObject, 10f, 24);
            UIFactory.SetSize(right, width: 470f);

            TextMeshProUGUI feeTitle = UIFactory.CreateText("FeeTitle", right.transform, "Абонентская плата", 18f, UIFactory.MutedTextColor);
            UIFactory.SetSize(feeTitle, height: 24f);
            feeText = UIFactory.CreateText("Fee", right.transform, "", 46f, UIFactory.TextColor);
            feeText.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(feeText, height: 60f);
            breakdownText = UIFactory.CreateText("Breakdown", right.transform, "", 15f, UIFactory.MutedTextColor, TextAlignmentOptions.TopLeft);
            UIFactory.SetSize(breakdownText, height: 42f);

            TextMeshProUGUI satTitle = UIFactory.CreateText("SatTitle", right.transform, "Довольство клиента", 18f, UIFactory.MutedTextColor);
            UIFactory.SetSize(satTitle, height: 28f);

            RectTransform satRow = UIFactory.CreateRow("SatRow", right.transform, 56f, 14f, 0);
            Image barBack = UIFactory.CreatePanel("Bar", satRow, new Color(1f, 1f, 1f, 0.1f));
            UIFactory.SetSize(barBack, height: 18f, flexibleWidth: 1f);
            satisfactionFill = UIFactory.CreatePanel("Fill", barBack.transform, Good);
            satisfactionFill.rectTransform.anchorMin = Vector2.zero;
            satisfactionFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            satisfactionFill.rectTransform.offsetMin = Vector2.zero;
            satisfactionFill.rectTransform.offsetMax = Vector2.zero;
            satisfactionText = UIFactory.CreateText("Percent", satRow, "", 38f, Good, TextAlignmentOptions.MidlineRight);
            satisfactionText.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(satisfactionText, width: 110f, height: 50f);

            verdictText = UIFactory.CreateText("Verdict", right.transform, "", 22f, Good);
            verdictText.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(verdictText, height: 32f);

            remarksText = UIFactory.CreateText("Remarks", right.transform, "", 18f, UIFactory.TextColor, TextAlignmentOptions.TopLeft);
            remarksText.lineSpacing = 6f;
            UIFactory.SetSize(remarksText, flexibleHeight: 1f);

            TextMeshProUGUI rule = UIFactory.CreateText("Rule", right.transform,
                "90 % и выше — клиент рад (+репутация). 65–89 % — подключит, но недоволен. Ниже — уйдёт. " +
                "Для подключения нужна SIM-карта на полке.", 14f, UIFactory.MutedTextColor, TextAlignmentOptions.BottomLeft);
            UIFactory.SetSize(rule, height: 60f);
        }
    }
}
