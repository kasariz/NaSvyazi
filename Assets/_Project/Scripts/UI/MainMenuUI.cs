using NaSvyazi.Save;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NaSvyazi.UI
{
    /// <summary>
    /// Главное меню: продолжить сохранённую игру, начать новую, настройки, выход.
    /// Интерфейс собирается из кода при запуске сцены MainMenu.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public const string GameScene = "Salon";

        private GameObject buttonsPanel;
        private GameObject settingsPanel;
        private GameObject difficultyPanel;

        private void Start()
        {
            Store.DayManager day = FindAnyObjectByType<Store.DayManager>();
            if (day != null && day.gameObject.scene != gameObject.scene)
            {
                // В редакторе открыты обе сцены (MainMenu и Salon) — меню выгружаем, игра идёт.
                Debug.LogWarning("[На связи] Открыты обе сцены — MainMenu и Salon. Меню выгружено.");
                SceneManager.UnloadSceneAsync(gameObject.scene);
                return;
            }
            if (day != null)
            {
                // В самой сцене меню оказались объекты салона (сцену перезаписали).
                Debug.LogError("[На связи] В сцене MainMenu лежат объекты салона. " +
                               "Выполните «На связи → Пересоздать сцену главного меню».");
            }

            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Build();
        }

        private void ContinueGame() => SceneManager.LoadScene(GameScene);

        /// <summary>«Новая игра» — сначала выбор сложности.</summary>
        private void NewGame()
        {
            buttonsPanel.SetActive(false);
            difficultyPanel.SetActive(true);
        }

        private void StartNewGame(Core.Difficulty difficulty)
        {
            Core.GameDifficulty.Set(difficulty);
            SaveSystem.DeleteSave();
            SceneManager.LoadScene(GameScene);
        }

        private void BackToMenu()
        {
            difficultyPanel.SetActive(false);
            buttonsPanel.SetActive(true);
        }

        /// <summary>Панель выбора сложности: три карточки с описанием и кнопка «Назад».</summary>
        private GameObject BuildDifficultyPanel(Transform parent)
        {
            RectTransform panel = UIFactory.CreateRect("Difficulty", parent);
            UIFactory.Stretch(panel);
            UIFactory.AddVerticalLayout(panel.gameObject, 14f, 0);

            TextMeshProUGUI header = UIFactory.CreateText("Header", panel, "Выберите сложность", 34f, UIFactory.TextColor);
            header.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(header, height: 50f);

            foreach (Core.Difficulty d in new[] { Core.Difficulty.Easy, Core.Difficulty.Normal, Core.Difficulty.Hard })
            {
                Core.Difficulty chosen = d;
                Image card = UIFactory.CreatePanel($"Card_{d}", panel, UIFactory.RowColor);
                HorizontalLayoutGroup row = UIFactory.AddHorizontalLayout(card.gameObject, 18f, 14);
                row.childAlignment = TextAnchor.MiddleLeft;
                UIFactory.SetSize(card, height: 120f);

                RectTransform texts = UIFactory.CreateRect("Texts", card.transform);
                UIFactory.AddVerticalLayout(texts.gameObject, 4f, 0).childAlignment = TextAnchor.MiddleLeft;
                UIFactory.SetSize(texts, flexibleWidth: 1f);
                TextMeshProUGUI name = UIFactory.CreateText("Name", texts, Core.GameDifficulty.Name(d), 28f,
                    d == Core.Difficulty.Normal ? UIFactory.AccentColor : UIFactory.TextColor);
                name.fontStyle = FontStyles.Bold;
                UIFactory.SetSize(name, height: 36f);
                TextMeshProUGUI desc = UIFactory.CreateText("Desc", texts, Core.GameDifficulty.Description(d), 18f,
                    UIFactory.MutedTextColor, TextAlignmentOptions.TopLeft);
                desc.enableAutoSizing = true;
                desc.fontSizeMin = 13f;
                desc.fontSizeMax = 18f;
                UIFactory.SetSize(desc, height: 52f);

                Button play = UIFactory.CreateButton("Play", card.transform, "Начать", () => StartNewGame(chosen),
                    d == Core.Difficulty.Normal ? UIFactory.AccentColor : UIFactory.ButtonColor, 24f);
                UIFactory.SetSize(play, width: 150f, height: 60f);
            }

            Button back = UIFactory.CreateButton("Back", panel, "Назад", BackToMenu, UIFactory.ButtonColor, 24f);
            UIFactory.SetSize(back, height: 56f);
            return panel.gameObject;
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ShowSettings(bool show)
        {
            buttonsPanel.SetActive(!show);
            settingsPanel.SetActive(show);
        }

        private void Build()
        {
            Canvas canvas = UIFactory.CreateCanvas("Main Menu Canvas", 0, transform);

            Image background = UIFactory.CreatePanel("Background", canvas.transform, new Color(0.06f, 0.07f, 0.09f, 1f));
            UIFactory.Stretch(background.rectTransform);

            // Акцентная полоса слева.
            Image accent = UIFactory.CreatePanel("Accent", canvas.transform, UIFactory.AccentColor);
            accent.rectTransform.anchorMin = new Vector2(0f, 0f);
            accent.rectTransform.anchorMax = new Vector2(0f, 1f);
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            accent.rectTransform.sizeDelta = new Vector2(16f, 0f);

            TextMeshProUGUI title = UIFactory.CreateText("Title", canvas.transform, "НА СВЯЗИ", 120f, UIFactory.TextColor);
            title.fontStyle = FontStyles.Bold;
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(140f, -140f), new Vector2(1200f, 150f));

            TextMeshProUGUI subtitle = UIFactory.CreateText("Subtitle", canvas.transform,
                "симулятор салона связи", 40f, UIFactory.MutedTextColor);
            UIFactory.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(146f, -290f), new Vector2(1200f, 60f));

            // Блок кнопок / настроек.
            RectTransform area = UIFactory.CreateRect("Area", canvas.transform);
            UIFactory.Place(area, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(140f, 90f),
                new Vector2(760f, 640f));

            RectTransform buttons = UIFactory.CreateRect("Buttons", area);
            UIFactory.Stretch(buttons);
            UIFactory.AddVerticalLayout(buttons.gameObject, 16f, 0);
            buttonsPanel = buttons.gameObject;

            int savedDay = SaveSystem.ReadSavedDay();
            if (savedDay > 0)
                AddButton(buttons, $"Продолжить (день {savedDay})", ContinueGame, UIFactory.AccentColor);

            AddButton(buttons, "Новая игра", NewGame, savedDay > 0 ? UIFactory.ButtonColor : UIFactory.AccentColor);
            AddButton(buttons, "Настройки", () => ShowSettings(true), UIFactory.ButtonColor);
            AddButton(buttons, "Выход", Quit, UIFactory.ButtonColor);

            RectTransform spacer = UIFactory.CreateRect("Spacer", buttons);
            UIFactory.SetSize(spacer, flexibleHeight: 1f);

            settingsPanel = SettingsPanel.Build(area, () => ShowSettings(false));
            settingsPanel.SetActive(false);

            difficultyPanel = BuildDifficultyPanel(area);
            difficultyPanel.SetActive(false);

            TextMeshProUGUI footer = UIFactory.CreateText("Footer", canvas.transform,
                $"Версия {Application.version}", 20f, UIFactory.MutedTextColor,
                TextAlignmentOptions.MidlineRight);
            UIFactory.Place(footer.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-40f, 30f), new Vector2(900f, 40f));
        }

        private static void AddButton(Transform parent, string label, System.Action onClick, Color color)
        {
            Button b = UIFactory.CreateButton(label, parent, label, onClick, color, 30f);
            UIFactory.SetSize(b, height: 80f);
        }
    }
}
