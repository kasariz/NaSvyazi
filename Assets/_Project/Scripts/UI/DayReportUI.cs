using System;
using System.Text;
using NaSvyazi.Economy;
using NaSvyazi.Player;
using NaSvyazi.Store;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NaSvyazi.UI
{
    /// <summary>
    /// Отчёт за рабочий день: финансы, обслуживание покупателей, консультации.
    /// Закрыть отчёт можно только кнопкой «Следующий день» (Esc не работает).
    /// </summary>
    public class DayReportUI : ModalWindow
    {
        private GameObject root;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI financeText;
        private TextMeshProUGUI serviceText;
        private TextMeshProUGUI profitText;

        private Action onNext;

        private void Awake()
        {
            Build();
            SetVisible(false);
        }

        // Отчёт нельзя закрыть клавишей Esc — только кнопкой.
        protected override void Update() { }

        public void ShowReport(DayReport report, Action onNextDay, PlayerController player)
        {
            onNext = onNextDay;

            titleText.text = string.IsNullOrEmpty(report.eventName)
                ? $"День {report.day} завершён"
                : $"День {report.day} завершён  ·  {report.eventName}";

            StringBuilder f = new StringBuilder();
            f.AppendLine($"Выручка:  {EconomyManager.Format(report.income)}");
            f.AppendLine($"Расходы:  {EconomyManager.Format(report.expenses)}");
            f.AppendLine($"   в т. ч. аренда:  {EconomyManager.Format(report.rent)}");
            if (report.wages > 0)
                f.AppendLine($"   зарплата персонала:  {EconomyManager.Format(report.wages)}");
            if (report.changeLoss > 0)
                f.AppendLine($"   потери на сдаче:  {EconomyManager.Format(report.changeLoss)}");
            f.AppendLine();
            f.AppendLine($"Баланс:  {EconomyManager.Format(report.balance)}");
            if (report.debtDays > 0)
            {
                int left = report.maxDebtDays - report.debtDays;
                f.AppendLine(left > 0
                    ? $"<color=#F25959>Салон в долгах! До банкротства: {left} {Progression.GoalManager.DaysWord(left)}</color>"
                    : "<color=#F25959>Долг не погашен — салон банкрот</color>");
            }
            f.Append($"Репутация:  {report.reputation}/100");
            financeText.text = f.ToString();

            StringBuilder s = new StringBuilder();
            s.AppendLine($"Обслужено покупателей:  {report.customersServed}");
            s.AppendLine($"Продано товаров:  {report.itemsSold}" +
                         (report.impulseBuys > 0 ? $"  (со стойки у кассы: {report.impulseBuys})" : ""));
            s.AppendLine($"Оплат картой / наличными:  {report.cardPayments} / {report.cashPayments}");
            s.AppendLine($"Отказались из-за цены:  {report.priceRefusals}");
            s.AppendLine($"Не нашли нужный товар:  {report.outOfStock}");
            s.AppendLine($"Ушли из очереди:  {report.leftQueue}");
            s.Append($"Консультации (точно / дороже / мимо):  " +
                     $"{report.consultBest} / {report.consultAcceptable} / {report.consultFailed}");
            if (report.dirtPenalty > 0)
                s.Append($"\nГрязный пол к закрытию:  −{report.dirtPenalty} к репутации");
            if (report.theftItems > 0 || report.thievesCaught > 0)
                s.Append($"\nУкрадено товаров:  {report.theftItems} на {EconomyManager.Format(report.theftLoss)}" +
                         $"   ·   задержано воришек:  {report.thievesCaught}");
            int skillPoints = Progression.SkillManager.Instance != null ? Progression.SkillManager.Instance.AvailablePoints : 0;
            if (skillPoints > 0)
                s.Append($"\n\n<color=#DB2E8C><b>Не потрачено очков навыков: {skillPoints}</b> — нажмите K утром</color>");
            serviceText.text = s.ToString();

            profitText.text = $"Прибыль за день: {EconomyManager.Format(report.Profit)}";
            profitText.color = report.Profit >= 0 ? UIFactory.PositiveColor : UIFactory.NegativeColor;

            Open(player);
        }

        protected override void SetVisible(bool visible) => root.SetActive(visible);

        private void Next()
        {
            Action callback = onNext;
            onNext = null;
            Close();
            callback?.Invoke();
        }

        private void Build()
        {
            Canvas canvas = UIFactory.CreateCanvas("Report Canvas", 25, transform);
            root = canvas.gameObject;

            Image dim = UIFactory.CreatePanel("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.7f));
            UIFactory.Stretch(dim.rectTransform);

            Image window = UIFactory.CreatePanel("Window", canvas.transform, UIFactory.PanelColor);
            UIFactory.Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1300f, 760f));
            UIFactory.AddVerticalLayout(window.gameObject, 16f, 40);

            titleText = UIFactory.CreateText("Title", window.transform, "", 40f, UIFactory.TextColor);
            titleText.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(titleText, height: 56f);

            profitText = UIFactory.CreateText("Profit", window.transform, "", 34f, UIFactory.TextColor);
            UIFactory.SetSize(profitText, height: 50f);

            RectTransform columns = UIFactory.CreateRect("Columns", window.transform);
            HorizontalLayoutGroup layout = UIFactory.AddHorizontalLayout(columns.gameObject, 40f, 0);
            layout.childAlignment = TextAnchor.UpperLeft;
            UIFactory.SetSize(columns, flexibleHeight: 1f);

            financeText = UIFactory.CreateText("Finance", columns, "", 26f, UIFactory.TextColor,
                TextAlignmentOptions.TopLeft);
            financeText.lineSpacing = 12f;
            UIFactory.SetSize(financeText, width: 520f);

            serviceText = UIFactory.CreateText("Service", columns, "", 24f, UIFactory.MutedTextColor,
                TextAlignmentOptions.TopLeft);
            serviceText.lineSpacing = 12f;
            UIFactory.SetSize(serviceText, flexibleWidth: 1f);

            Button next = UIFactory.CreateButton("Next", window.transform, "Следующий день", Next,
                UIFactory.AccentColor, 28f);
            UIFactory.SetSize(next, height: 70f);
        }
    }
}
