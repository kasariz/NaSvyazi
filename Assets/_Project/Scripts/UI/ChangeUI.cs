using System;
using NaSvyazi.Economy;
using NaSvyazi.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NaSvyazi.UI
{
    /// <summary>
    /// Окно оплаты наличными: игрок отсчитывает сдачу купюрами и монетами.
    /// Выдать меньше нужного нельзя, лишняя сдача — убыток салона.
    /// </summary>
    public class ChangeUI : ModalWindow
    {
        private static readonly int[] Denominations = { 5000, 1000, 500, 100, 50, 10, 5, 1 };

        private GameObject root;
        private TextMeshProUGUI totalText;
        private TextMeshProUGUI givenText;
        private TextMeshProUGUI dueText;
        private TextMeshProUGUI changeText;
        private Button giveButton;

        private int total;
        private int given;
        private int change;
        private Action<int> onDone;

        private int Due => given - total;

        private void Awake()
        {
            Build();
            SetVisible(false);
        }

        /// <summary>Открыть окно: сумма покупки, сколько дал покупатель, что сделать по завершении.</summary>
        public void OpenFor(int totalAmount, int givenAmount, Action<int> callback, PlayerController player)
        {
            total = totalAmount;
            given = givenAmount;
            change = 0;
            onDone = callback;
            Open(player);
        }

        protected override void SetVisible(bool visible) => root.SetActive(visible);
        protected override void OnOpened() => Refresh();

        private void Add(int amount)
        {
            change += amount;
            Refresh();
        }

        private void ResetChange()
        {
            change = 0;
            Refresh();
        }

        private void Give()
        {
            if (change < Due) return;
            Action<int> callback = onDone;
            int result = change;
            onDone = null;
            Close();
            callback?.Invoke(result);
        }

        private void Refresh()
        {
            totalText.text = $"К оплате: {EconomyManager.Format(total)}";
            givenText.text = $"Покупатель дал: {EconomyManager.Format(given)}";
            dueText.text = $"Сдача: {EconomyManager.Format(Due)}";
            changeText.text = $"Вы выдаёте: {EconomyManager.Format(change)}";

            changeText.color = change == Due ? UIFactory.PositiveColor
                : change < Due ? new Color(0.95f, 0.8f, 0.3f) : UIFactory.NegativeColor;
            giveButton.interactable = change >= Due;
        }

        private void Build()
        {
            Canvas canvas = UIFactory.CreateCanvas("Change Canvas", 20, transform);
            root = canvas.gameObject;

            Image dim = UIFactory.CreatePanel("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.45f));
            UIFactory.Stretch(dim.rectTransform);

            Image window = UIFactory.CreatePanel("Window", canvas.transform, UIFactory.PanelColor);
            UIFactory.Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1000f, 600f));
            UIFactory.AddVerticalLayout(window.gameObject, 10f, 32);

            TextMeshProUGUI title = UIFactory.CreateText("Title", window.transform, "Оплата наличными", 34f,
                UIFactory.TextColor);
            title.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(title, height: 48f);

            totalText = AddLine(window.transform, 26f, UIFactory.MutedTextColor);
            givenText = AddLine(window.transform, 26f, UIFactory.MutedTextColor);
            dueText = AddLine(window.transform, 32f, UIFactory.TextColor);
            changeText = AddLine(window.transform, 32f, UIFactory.TextColor);
            changeText.fontStyle = FontStyles.Bold;

            RectTransform money = UIFactory.CreateRect("Money", window.transform);
            HorizontalLayoutGroup layout = UIFactory.AddHorizontalLayout(money.gameObject, 8f, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            UIFactory.SetSize(money, height: 80f);

            foreach (int value in Denominations)
            {
                int v = value;
                Color color = v >= 50 ? new Color(0.2f, 0.36f, 0.3f) : new Color(0.42f, 0.38f, 0.26f);
                Button b = UIFactory.CreateButton($"Money_{v}", money, v.ToString(), () => Add(v), color, 26f);
                UIFactory.SetSize(b, width: 104f);
            }

            RectTransform spacer = UIFactory.CreateRect("Spacer", window.transform);
            UIFactory.SetSize(spacer, flexibleHeight: 1f);

            RectTransform footer = UIFactory.CreateRect("Footer", window.transform);
            UIFactory.AddHorizontalLayout(footer.gameObject, 12f, 0);
            UIFactory.SetSize(footer, height: 60f);

            RectTransform footerSpacer = UIFactory.CreateRect("FooterSpacer", footer);
            UIFactory.SetSize(footerSpacer, flexibleWidth: 1f);

            UIFactory.SetSize(UIFactory.CreateButton("Reset", footer, "Сбросить", ResetChange,
                UIFactory.ButtonColor), width: 200f);
            giveButton = UIFactory.CreateButton("Give", footer, "Выдать сдачу", Give, UIFactory.AccentColor);
            UIFactory.SetSize(giveButton, width: 260f);
        }

        private static TextMeshProUGUI AddLine(Transform parent, float size, Color color)
        {
            TextMeshProUGUI t = UIFactory.CreateText("Line", parent, "", size, color);
            UIFactory.SetSize(t, height: size + 14f);
            return t;
        }
    }
}
