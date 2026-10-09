using NaSvyazi.Core;
using NaSvyazi.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NaSvyazi.UI
{
    /// <summary>
    /// Постоянный интерфейс поверх игры: баланс в правом верхнем углу
    /// и всплывающие уведомления сверху по центру. Собирается из кода.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [SerializeField] private float notificationTime = 3f;
        [SerializeField] private float flashTime = 0.6f;

        private TextMeshProUGUI moneyText;
        private Image notificationPanel;
        private TextMeshProUGUI notificationText;
        private float notificationTimer;
        private float flashTimer;

        private void Awake() => Build();

        private void OnEnable()
        {
            GameEvents.MoneyChanged += OnMoneyChanged;
            GameEvents.Notification += OnNotification;
        }

        private void OnDisable()
        {
            GameEvents.MoneyChanged -= OnMoneyChanged;
            GameEvents.Notification -= OnNotification;
        }

        private void Update()
        {
            if (notificationTimer > 0f)
            {
                notificationTimer -= Time.deltaTime;
                if (notificationTimer <= 0f) notificationPanel.gameObject.SetActive(false);
            }

            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                if (flashTimer <= 0f) moneyText.color = UIFactory.TextColor;
            }
        }

        private void OnMoneyChanged(int balance, int delta)
        {
            moneyText.text = EconomyManager.Format(balance);
            if (delta == 0) return;

            moneyText.color = delta > 0 ? UIFactory.PositiveColor : UIFactory.NegativeColor;
            flashTimer = flashTime;
        }

        private void OnNotification(string message)
        {
            notificationText.text = message;
            notificationPanel.gameObject.SetActive(true);
            notificationTimer = notificationTime;
        }

        private void Build()
        {
            // Высокий порядок отрисовки — уведомления видны и поверх окон.
            Canvas canvas = UIFactory.CreateCanvas("HUD Canvas", 30, transform);

            Image moneyPanel = UIFactory.CreatePanel("Money", canvas.transform,
                new Color(0.08f, 0.09f, 0.11f, 0.8f));
            UIFactory.Place(moneyPanel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -24f), new Vector2(300f, 60f));
            moneyText = UIFactory.CreateText("MoneyText", moneyPanel.transform, "—", 30f,
                UIFactory.TextColor, TextAlignmentOptions.MidlineRight);
            UIFactory.Stretch(moneyText.rectTransform, 14f);

            notificationPanel = UIFactory.CreatePanel("Notification", canvas.transform,
                new Color(0.08f, 0.09f, 0.11f, 0.9f));
            UIFactory.Place(notificationPanel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(760f, 56f));
            notificationText = UIFactory.CreateText("Text", notificationPanel.transform, "", 24f,
                UIFactory.TextColor, TextAlignmentOptions.Center);
            UIFactory.Stretch(notificationText.rectTransform, 8f);
            notificationPanel.gameObject.SetActive(false);
        }
    }
}
