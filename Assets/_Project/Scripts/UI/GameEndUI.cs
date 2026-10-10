using System;
using NaSvyazi.Audio;
using NaSvyazi.Economy;
using NaSvyazi.Player;
using NaSvyazi.Progression;
using NaSvyazi.Save;
using NaSvyazi.Story;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NaSvyazi.UI
{
    /// <summary>
    /// Финальное окно: победа («Лучший салон города») или поражение (банкротство, закрытие салона).
    /// После поражения сохранение удаляется — игра начинается заново.
    /// Закрыть окно клавишей Esc нельзя — только кнопками.
    /// </summary>
    public class GameEndUI : ModalWindow
    {
        private GameObject root;
        private Image header;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI bodyText;
        private Button primaryButton;
        private Button secondaryButton;

        private Action onContinue;
        private bool victory;

        private void Awake()
        {
            Build();
            SetVisible(false);
        }

        protected override void Update() { }

        public void Show(DayOutcome outcome, int day, Action continueGame)
        {
            onContinue = continueGame;
            victory = outcome == DayOutcome.Victory;

            int balance = EconomyManager.Instance != null ? EconomyManager.Instance.Balance : 0;
            int reputation = ReputationManager.Instance != null ? ReputationManager.Instance.Value : 0;
            int level = LevelManager.Instance != null ? LevelManager.Instance.Level : 1;
            string stats = $"\n\n<color=#A6ABB8>Дней в игре: {day}   ·   уровень {level}   ·   " +
                           $"репутация {reputation}/100   ·   баланс {EconomyManager.Format(balance)}</color>";

            switch (outcome)
            {
                case DayOutcome.Victory:
                    Letter final = LoreManager.Letters.Find(l => l.id == "final");
                    titleText.text = "Лучший салон города!";
                    bodyText.text = (final != null ? final.body : "Цель достигнута.") + stats;
                    header.color = UIFactory.AccentColor;
                    SetButton(primaryButton, "Продолжить играть");
                    SetButton(secondaryButton, "Сохранить и выйти в меню");
                    SoundManager.Play(Sfx.Purchase);
                    break;

                case DayOutcome.Bankrupt:
                    titleText.text = "Банкротство";
                    bodyText.text =
                        $"Салон {GoalManager.Instance?.MaxDebtDays ?? 3} {GoalManager.DaysWord(GoalManager.Instance?.MaxDebtDays ?? 3)} подряд " +
                        "закрывал день в минусе. Арендодатель расторг договор, а сеть «На связи» " +
                        "передала точку другому управляющему.\n\n" +
                        "Совет: не закупайте больше, чем успеете продать, следите за наценкой " +
                        "и не расширяйтесь, пока выручка не перекрывает аренду с запасом." + stats;
                    header.color = UIFactory.NegativeColor;
                    SetButton(primaryButton, "Начать заново");
                    SetButton(secondaryButton, "Выйти в главное меню");
                    SoundManager.Play(Sfx.Error);
                    break;

                default:
                    titleText.text = "Салон закрыт";
                    bodyText.text =
                        "Репутация упала до нуля: жалобы покупателей дошли до управляющей сетью, " +
                        "и Марина Сергеевна закрыла салон.\n\n" +
                        "Совет: держите полки заполненными, не завышайте цены, подбирайте точные тарифы " +
                        "и не оставляйте грязь на полу к закрытию." + stats;
                    header.color = UIFactory.NegativeColor;
                    SetButton(primaryButton, "Начать заново");
                    SetButton(secondaryButton, "Выйти в главное меню");
                    SoundManager.Play(Sfx.Error);
                    break;
            }

            if (!victory) SaveSystem.DeleteSave();
            Open(FindAnyObjectByType<PlayerController>());
        }

        protected override void SetVisible(bool visible) => root.SetActive(visible);

        private void Primary()
        {
            if (victory)
            {
                Action callback = onContinue;
                onContinue = null;
                Close();
                callback?.Invoke();
            }
            else
            {
                Close();
                SceneManager.LoadScene(MainMenuUI.GameScene);
            }
        }

        private void Secondary()
        {
            if (victory)
            {
                // Следующий день начинается и автоматически сохраняется — затем выходим в меню.
                Action callback = onContinue;
                onContinue = null;
                Close();
                callback?.Invoke();
            }
            else
            {
                Close();
            }
            Time.timeScale = 1f;
            SceneManager.LoadScene(PauseMenuUI.MainMenuScene);
        }

        private static void SetButton(Button button, string label) =>
            button.GetComponentInChildren<TextMeshProUGUI>().text = label;

        private void Build()
        {
            Canvas canvas = UIFactory.CreateCanvas("GameEnd Canvas", 28, transform);
            root = canvas.gameObject;

            Image dim = UIFactory.CreatePanel("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.8f));
            UIFactory.Stretch(dim.rectTransform);

            Image window = UIFactory.CreatePanel("Window", canvas.transform, UIFactory.PanelColor);
            UIFactory.Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1100f, 700f));
            UIFactory.AddVerticalLayout(window.gameObject, 20f, 40);

            header = UIFactory.CreatePanel("Header", window.transform, UIFactory.AccentColor);
            UIFactory.SetSize(header, height: 8f);

            titleText = UIFactory.CreateText("Title", window.transform, "", 48f, UIFactory.TextColor,
                TextAlignmentOptions.Center);
            titleText.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(titleText, height: 70f);

            bodyText = UIFactory.CreateText("Body", window.transform, "", 26f, UIFactory.TextColor,
                TextAlignmentOptions.TopLeft);
            bodyText.lineSpacing = 8f;
            bodyText.enableAutoSizing = true;
            bodyText.fontSizeMin = 16f;
            bodyText.fontSizeMax = 26f;
            UIFactory.SetSize(bodyText, flexibleHeight: 1f);

            RectTransform row = UIFactory.CreateRect("Buttons", window.transform);
            UIFactory.AddHorizontalLayout(row.gameObject, 20f, 0);
            UIFactory.SetSize(row, height: 70f);

            primaryButton = UIFactory.CreateButton("Primary", row, "", Primary, UIFactory.AccentColor, 26f);
            UIFactory.SetSize(primaryButton, flexibleWidth: 1f);
            secondaryButton = UIFactory.CreateButton("Secondary", row, "", Secondary, UIFactory.ButtonColor, 26f);
            UIFactory.SetSize(secondaryButton, flexibleWidth: 1f);
        }
    }
}
