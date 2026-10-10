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
        private TextMeshProUGUI goalText;
        private Image levelUpBanner;
        private TextMeshProUGUI levelUpTitle;
        private TextMeshProUGUI levelUpText;
        private float levelUpTimer;
        private int shownLevel = -1;
        private float goalTimer;
        private string shownGoal;
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
            RefreshGoal();
            PlaceGoalPanel();
            UpdateLevelUpBanner();
        }

        private void ShowLevelUp(int level)
        {
            levelUpTitle.text = $"Уровень салона {level}!";
            levelUpText.text = "Получено очко навыка — нажмите <b>[K]</b>, чтобы его вложить.\n" +
                               "В терминале могли открыться новые товары, мебель и расширения.";
            levelUpBanner.gameObject.SetActive(true);
            levelUpTimer = 5f;
        }

        private void UpdateLevelUpBanner()
        {
            if (levelUpTimer <= 0f) return;
            levelUpTimer -= Time.unscaledDeltaTime;

            // Появление и исчезновение: первые и последние полсекунды — плавно.
            float alpha = Mathf.Clamp01(Mathf.Min((5f - levelUpTimer) / 0.4f, levelUpTimer / 0.6f));
            CanvasGroup group = levelUpBanner.GetComponent<CanvasGroup>();
            group.alpha = alpha;
            if (levelUpTimer <= 0f) levelUpBanner.gameObject.SetActive(false);
        }

        /// <summary>Плашка цели игры и предупреждение о долге (обновляется дважды в секунду).</summary>
        private void RefreshGoal()
        {
            goalTimer -= Time.deltaTime;
            if (goalTimer > 0f) return;
            goalTimer = 0.5f;

            string text = Progression.GoalManager.Instance != null ? Progression.GoalManager.Instance.BuildGoalText() : "";
            if (text == shownGoal) return;
            shownGoal = text;
            goalText.text = text;
            goalText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>Плашка цели под плашкой события (или под часами, если события нет).</summary>
        private void PlaceGoalPanel()
        {
            RectTransform goal = (RectTransform)goalText.transform.parent;
            float y = eventPanel.gameObject.activeSelf ? -170f : -92f;
            if (!Mathf.Approximately(goal.anchoredPosition.y, y)) goal.anchoredPosition = new Vector2(24f, y);
        }

        /// <summary>Плашки «событие дня» и «есть свободные очки навыков» (обновляются при изменении).</summary>
        private void RefreshEventAndSkills()
        {
            string current = StoreEventManager.CurrentName;
            if (current != shownEvent)
            {
                shownEvent = current;
                eventPanel.gameObject.SetActive(!string.IsNullOrEmpty(current));
                string effects = StoreEventManager.CurrentEffects;
                eventText.text = string.IsNullOrEmpty(effects)
                    ? $"<b>Событие дня: {current}</b>"
                    : $"<b>Событие дня: {current}</b>\n<size=80%>{effects}</size>";
            }

            int points = SkillManager.Instance != null ? SkillManager.Instance.AvailablePoints : 0;
            if (points != shownPoints)
            {
                shownPoints = points;
                skillPanel.gameObject.SetActive(points > 0);
                skillText.text = points == 1
                    ? "<b>Есть очко навыка!</b>  Нажмите  <b>[K]</b>"
                    : $"<b>Очков навыков: {points}</b>  Нажмите  <b>[K]</b>";
            }

            // Плашка навыков пульсирует, пока очки не потрачены, — её трудно не заметить.
            if (points > 0)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                skillPanel.color = Color.Lerp(new Color(0.55f, 0.1f, 0.35f, 0.9f), UIFactory.AccentColor, pulse);
                float scale = 1f + 0.04f * pulse;
                skillPanel.rectTransform.localScale = new Vector3(scale, scale, 1f);
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
            // Баннер «Новый уровень» по центру экрана (не при загрузке сохранения).
            if (shownLevel > 0 && level > shownLevel && Time.timeSinceLevelLoad > 1f) ShowLevelUp(level);
            shownLevel = level;

            LevelManager lm = LevelManager.Instance;
            float fill;
            if (lm != null && lm.IsMaxLevel)
            {
                // Максимальный уровень: опыт копится в «мастерство» — дополнительные очки навыков.
                levelText.text = $"Уровень {level} (макс.)   ·   мастерство {lm.MasteryProgress}/{lm.MasteryStep}";
                fill = Mathf.Clamp01((float)lm.MasteryProgress / lm.MasteryStep);
            }
            else
            {
                levelText.text = $"Уровень {level}   ·   {xp}/{next} опыта";
                fill = next > 0 ? Mathf.Clamp01((float)xp / next) : 1f;
            }
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
            levelText.enableAutoSizing = true;
            levelText.fontSizeMin = 13f;
            levelText.fontSizeMax = 20f;
            UIFactory.Stretch(levelText.rectTransform, 12f);

            eventPanel = UIFactory.CreatePanel("Event", canvas.transform, new Color(0.86f, 0.18f, 0.55f, 0.85f));
            UIFactory.Place(eventPanel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -92f), new Vector2(460f, 70f));
            eventText = UIFactory.CreateText("EventText", eventPanel.transform, "", 22f, Color.white);
            eventText.enableAutoSizing = true;
            eventText.fontSizeMin = 14f;
            eventText.fontSizeMax = 22f;
            UIFactory.Stretch(eventText.rectTransform, 10f);
            eventPanel.gameObject.SetActive(false);

            // Цель игры — слева под плашкой события; высота подстраивается под текст.
            Image goalPanel = UIFactory.CreatePanel("Goal", canvas.transform, new Color(0.08f, 0.09f, 0.11f, 0.8f));
            UIFactory.Place(goalPanel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -146f), new Vector2(460f, 0f));
            VerticalLayoutGroup goalLayout = UIFactory.AddVerticalLayout(goalPanel.gameObject, 0f, 12);
            goalLayout.childControlHeight = true;
            goalLayout.childForceExpandHeight = false;
            goalPanel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            goalText = UIFactory.CreateText("GoalText", goalPanel.transform, "", 19f, UIFactory.TextColor,
                TextAlignmentOptions.TopLeft);
            goalText.lineSpacing = 6f;
            goalPanel.gameObject.SetActive(false);

            skillPanel = UIFactory.CreatePanel("Skills", canvas.transform, new Color(0.08f, 0.09f, 0.11f, 0.8f));
            UIFactory.Place(skillPanel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -200f), new Vector2(300f, 50f));
            skillPanel.rectTransform.pivot = new Vector2(1f, 1f);
            skillText = UIFactory.CreateText("SkillsText", skillPanel.transform, "", 21f,
                Color.white, TextAlignmentOptions.Center);
            skillText.enableAutoSizing = true;
            skillText.fontSizeMin = 14f;
            skillText.fontSizeMax = 21f;
            UIFactory.Stretch(skillText.rectTransform, 12f);
            skillPanel.gameObject.SetActive(false);

            // Баннер нового уровня — по центру, чуть выше середины экрана.
            levelUpBanner = UIFactory.CreatePanel("LevelUp", canvas.transform, new Color(0.08f, 0.09f, 0.11f, 0.92f));
            UIFactory.Place(levelUpBanner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 220f), new Vector2(820f, 170f));
            levelUpBanner.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            levelUpBanner.raycastTarget = false;
            Image stripe = UIFactory.CreatePanel("Stripe", levelUpBanner.transform, UIFactory.AccentColor);
            stripe.rectTransform.anchorMin = new Vector2(0f, 1f);
            stripe.rectTransform.anchorMax = new Vector2(1f, 1f);
            stripe.rectTransform.pivot = new Vector2(0.5f, 1f);
            stripe.rectTransform.sizeDelta = new Vector2(0f, 8f);
            levelUpTitle = UIFactory.CreateText("Title", levelUpBanner.transform, "", 44f, Color.white,
                TextAlignmentOptions.Center);
            levelUpTitle.fontStyle = FontStyles.Bold;
            UIFactory.Place(levelUpTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -18f), new Vector2(780f, 60f));
            levelUpText = UIFactory.CreateText("Text", levelUpBanner.transform, "", 22f, UIFactory.TextColor,
                TextAlignmentOptions.Center);
            UIFactory.Place(levelUpText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 16f), new Vector2(780f, 76f));
            levelUpBanner.gameObject.SetActive(false);

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
