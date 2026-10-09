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

        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Build();
        }

        private void ContinueGame() => SceneManager.LoadScene(GameScene);

        private void NewGame()
        {
            SaveSystem.DeleteSave();
            SceneManager.LoadScene(GameScene);
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
            UIFactory.Place(area, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(140f, 120f),
                new Vector2(720f, 560f));

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

            TextMeshProUGUI footer = UIFactory.CreateText("Footer", canvas.transform,
                "Прототип игры в жанре Simulator · Unity 6", 20f, UIFactory.MutedTextColor,
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
