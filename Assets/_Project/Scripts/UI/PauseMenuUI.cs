using NaSvyazi.Core;
using NaSvyazi.Player;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NaSvyazi.UI
{
    /// <summary>
    /// Меню паузы (Esc): продолжить, настройки, сохранить и выйти в главное меню, выйти из игры.
    /// На время паузы игровое время останавливается (Time.timeScale = 0).
    /// </summary>
    public class PauseMenuUI : ModalWindow
    {
        public const string MainMenuScene = "MainMenu";

        public static PauseMenuUI Instance { get; private set; }

        private GameObject root;
        private GameObject mainPanel;
        private GameObject settingsPanel;
        private PlayerController player;

        private void Awake()
        {
            Instance = this;
            Build();
            SetVisible(false);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        protected override void Update()
        {
            base.Update(); // Esc закрывает меню, если оно открыто

            bool escapeFree = !IsAnyOpen && LastClosedFrame != Time.frameCount;
            if (escapeFree && GameInput.EscapePressed)
            {
                if (player == null) player = FindAnyObjectByType<PlayerController>();
                Open(player);
            }
        }

        protected override void SetVisible(bool visible) => root.SetActive(visible);

        protected override void OnOpened()
        {
            Time.timeScale = 0f;
            ShowMain();
        }

        protected override void OnClosed() => Time.timeScale = 1f;

        private void ShowMain()
        {
            mainPanel.SetActive(true);
            settingsPanel.SetActive(false);
        }

        private void ShowSettings()
        {
            mainPanel.SetActive(false);
            settingsPanel.SetActive(true);
        }

        private void SaveAndExitToMenu()
        {
            if (Save.SaveSystem.Instance != null) Save.SaveSystem.Instance.Save();
            Time.timeScale = 1f;
            SceneManager.LoadScene(MainMenuScene);
        }

        private void QuitGame()
        {
            if (Save.SaveSystem.Instance != null) Save.SaveSystem.Instance.Save();
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Build()
        {
            Canvas canvas = UIFactory.CreateCanvas("Pause Canvas", 40, transform);
            root = canvas.gameObject;

            Image dim = UIFactory.CreatePanel("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.6f));
            UIFactory.Stretch(dim.rectTransform);

            Image window = UIFactory.CreatePanel("Window", canvas.transform, UIFactory.PanelColor);
            UIFactory.Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760f, 620f));

            RectTransform content = UIFactory.CreateRect("Content", window.transform);
            UIFactory.Stretch(content, 40f);

            // Главная панель паузы.
            RectTransform main = UIFactory.CreateRect("Main", content);
            UIFactory.Stretch(main);
            UIFactory.AddVerticalLayout(main.gameObject, 16f, 0);
            mainPanel = main.gameObject;

            TextMeshProUGUI title = UIFactory.CreateText("Title", main, "Пауза", 40f, UIFactory.TextColor);
            title.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(title, height: 64f);

            AddButton(main, "Продолжить", Close, UIFactory.AccentColor);
            AddButton(main, "Настройки", ShowSettings, UIFactory.ButtonColor);
            AddButton(main, "Сохранить и выйти в меню", SaveAndExitToMenu, UIFactory.ButtonColor);
            AddButton(main, "Сохранить и выйти из игры", QuitGame, UIFactory.ButtonColor);

            TextMeshProUGUI hint = UIFactory.CreateText("Hint", main,
                "При загрузке день начнётся заново с утра, деньги и товар сохранятся.", 18f,
                UIFactory.MutedTextColor, TextAlignmentOptions.Center);
            UIFactory.SetSize(hint, height: 40f);

            // Панель настроек.
            settingsPanel = SettingsPanel.Build(content, ShowMain);
        }

        private static void AddButton(Transform parent, string label, System.Action onClick, Color color)
        {
            Button b = UIFactory.CreateButton(label, parent, label, onClick, color, 26f);
            UIFactory.SetSize(b, height: 68f);
        }
    }
}
