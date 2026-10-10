using NaSvyazi.Audio;
using NaSvyazi.Core;
using NaSvyazi.Economy;
using NaSvyazi.Events;
using NaSvyazi.Progression;
using NaSvyazi.Store;
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
        private Image eventPanel;
        private TextMeshProUGUI eventText;
        private Image skillPanel;
        private TextMeshProUGUI skillText;
        private string shownEvent;
        private int shownPoints = -1;
        private TextMeshProUGUI reputationText;
        private TextMeshProUGUI clockText;
        private TextMeshProUGUI levelText;
        private Image levelBar;
        private Image notificationPanel;
        private TextMeshProUGUI notificationText;
        private float notificationTimer;
        private float flashTimer;

        private void Awake() => Build();

        private void OnEnable()
        {
            GameEvents.MoneyChanged += OnMoneyChanged;
            GameEvents.Notification += OnNotification;
            GameEvents.ReputationChanged += OnReputationChanged;
            GameEvents.DayTimeChanged += OnDayTimeChanged;
            GameEvents.LevelChanged += OnLevelChanged;
        }

        private void OnDisable()
        {
            GameEvents.MoneyChanged -= OnMoneyChanged;
            GameEvents.Notification -= OnNotification;
            GameEvents.ReputationChanged -= OnReputationChanged;
            GameEvents.DayTimeChanged -= OnDayTimeChanged;
            GameEvents.LevelChanged -= OnLevelChanged;
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

            RefreshEventAndSkills();
        }

        /// <summary>Плашки «событие дня» и «есть свободные очки навыков» (обновляются при изменении).</summary>
        private void RefreshEventAndSkills()
        {
            string current = StoreEventManager.CurrentName;
            if (current != shownEvent)
            {
                shownEvent = current;
                eventPanel.gameObject.SetActive(!string.IsNullOrEmpty(current));
                eventText.text = $"Событие дня: {current}";
            }

            int points = SkillManager.Instance != null ? SkillManager.Instance.AvailablePoints : 0;
            if (points != shownPoints)
            {
                shownPoints = points;
                skillPanel.gameObject.SetActive(points > 0);
                skillText.text = $"Очки навыков: {points}  ·  клавиша K";
            }
        }

        private void OnMoneyChanged(int balance, int delta)
        {
            moneyText.text = EconomyManager.Format(balance);
            if (delta == 0) return;

            moneyText.color = delta > 0 ? UIFactory.PositiveColor : UIFactory.NegativeColor;
            flashTimer = flashTime;
        }

        private void OnReputationChanged(int value)
        {
            reputationText.text = $"Репутация: {value}/100";
            reputationText.color = value >= 60 ? UIFactory.PositiveColor
                : value >= 35 ? UIFactory.TextColor : UIFactory.NegativeColor;
        }

        private void OnLevelChanged(int level, int xp, int next)
        {
            levelText.text = $"Уровень {level}   ·   {xp}/{next} опыта";
            float fill = next > 0 ? Mathf.Clamp01((float)xp / next) : 1f;
            levelBar.rectTransform.anchorMax = new Vector2(fill, 1f);
        }

        private void OnDayTimeChanged(int day, int minutes, DayPhase phase)
        {
            string phaseText;
            switch (phase)
            {
                case DayPhase.Preparing: phaseText = "подготовка"; break;
                case DayPhase.Open: phaseText = "открыто"; break;
                case DayPhase.Closing: phaseText = "закрываемся"; break;
                default: phaseText = "итоги дня"; break;
            }
            clockText.text = $"День {day}  ·  {DayManager.FormatTime(minutes)}  ·  {phaseText}";
        }

        private void OnNotification(string message)
        {
            notificationText.text = message;
            SoundManager.Play(Sfx.Notify, 0.35f);
            notificationPanel.gameObject.SetActive(true);

            // Длинные сообщения (объявление события дня) — в две строки и висят дольше.
            bool longMessage = message.Length > 60;
            notificationPanel.rectTransform.sizeDelta = new Vector2(longMessage ? 900f : 760f, longMessage ? 96f : 56f);
            notificationTimer = Mathf.Max(notificationTime, message.Length / 20f);
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

            Image reputationPanel = UIFactory.CreatePanel("Reputation", canvas.transform,
                new Color(0.08f, 0.09f, 0.11f, 0.8f));
            UIFactory.Place(reputationPanel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -92f), new Vector2(300f, 46f));
            reputationText = UIFactory.CreateText("ReputationText", reputationPanel.transform, "", 22f,
                UIFactory.TextColor, TextAlignmentOptions.MidlineRight);
            UIFactory.Stretch(reputationText.rectTransform, 14f);

            Image clockPanel = UIFactory.CreatePanel("Clock", canvas.transform,
                new Color(0.08f, 0.09f, 0.11f, 0.8f));
            UIFactory.Place(clockPanel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -24f), new Vector2(460f, 60f));
            clockText = UIFactory.CreateText("ClockText", clockPanel.transform, "", 26f, UIFactory.TextColor);
            UIFactory.Stretch(clockText.rectTransform, 14f);

            Image levelPanel = UIFactory.CreatePanel("Level", canvas.transform,
                new Color(0.08f, 0.09f, 0.11f, 0.8f));
            UIFactory.Place(levelPanel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -146f), new Vector2(300f, 46f));

            Image barBack = UIFactory.CreatePanel("Bar", levelPanel.transform, new Color(1f, 1f, 1f, 0.08f));
            barBack.rectTransform.anchorMin = new Vector2(0f, 0f);
            barBack.rectTransform.anchorMax = new Vector2(1f, 0f);
            barBack.rectTransform.pivot = new Vector2(0.5f, 0f);
            barBack.rectTransform.sizeDelta = new Vector2(0f, 5f);

            levelBar = UIFactory.CreatePanel("Fill", barBack.transform, UIFactory.AccentColor);
            levelBar.rectTransform.anchorMin = Vector2.zero;
            levelBar.rectTransform.anchorMax = new Vector2(0f, 1f);
            levelBar.rectTransform.offsetMin = Vector2.zero;
            levelBar.rectTransform.offsetMax = Vector2.zero;

            levelText = UIFactory.CreateText("LevelText", levelPanel.transform, "", 20f,
                UIFactory.TextColor, TextAlignmentOptions.MidlineRight);
            UIFactory.Stretch(levelText.rectTransform, 12f);

            eventPanel = UIFactory.CreatePanel("Event", canvas.transform, new Color(0.86f, 0.18f, 0.55f, 0.85f));
            UIFactory.Place(eventPanel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -92f), new Vector2(460f, 46f));
            eventText = UIFactory.CreateText("EventText", eventPanel.transform, "", 22f, Color.white);
            UIFactory.Stretch(eventText.rectTransform, 14f);
            eventPanel.gameObject.SetActive(false);

            skillPanel = UIFactory.CreatePanel("Skills", canvas.transform, new Color(0.08f, 0.09f, 0.11f, 0.8f));
            UIFactory.Place(skillPanel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -200f), new Vector2(300f, 42f));
            skillText = UIFactory.CreateText("SkillsText", skillPanel.transform, "", 20f,
                UIFactory.AccentColor, TextAlignmentOptions.MidlineRight);
            UIFactory.Stretch(skillText.rectTransform, 12f);
            skillPanel.gameObject.SetActive(false);

            notificationPanel = UIFactory.CreatePanel("Notification", canvas.transform,
                new Color(0.08f, 0.09f, 0.11f, 0.9f));
            UIFactory.Place(notificationPanel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(760f, 56f));
            notificationText = UIFactory.CreateText("Text", notificationPanel.transform, "", 24f,
                UIFactory.TextColor, TextAlignmentOptions.Center);
            notificationText.enableAutoSizing = true;
            notificationText.fontSizeMin = 16f;
            notificationText.fontSizeMax = 24f;
            UIFactory.Stretch(notificationText.rectTransform, 8f);
            notificationPanel.gameObject.SetActive(false);
        }
    }
}
