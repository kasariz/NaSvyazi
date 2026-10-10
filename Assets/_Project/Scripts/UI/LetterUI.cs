using NaSvyazi.Audio;
using NaSvyazi.Player;
using NaSvyazi.Story;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NaSvyazi.UI
{
    /// <summary>Окно письма: светлый лист с заголовком, текстом и подписью. Закрывается кнопкой или Esc.</summary>
    public class LetterUI : ModalWindow
    {
        private static readonly Color Paper = new Color(0.97f, 0.96f, 0.93f, 1f);
        private static readonly Color Ink = new Color(0.12f, 0.12f, 0.14f, 1f);
        private static readonly Color InkMuted = new Color(0.4f, 0.4f, 0.44f, 1f);

        private GameObject root;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI bodyText;

        private void Awake()
        {
            Build();
            SetVisible(false);
        }

        public void Show(Letter letter, PlayerController player)
        {
            titleText.text = letter.title;
            bodyText.text = letter.body;
            SoundManager.Play(Sfx.Notify, 0.5f);
            Open(player);
        }

        protected override void SetVisible(bool visible) => root.SetActive(visible);

        private void Build()
        {
            Canvas canvas = UIFactory.CreateCanvas("Letter Canvas", 22, transform);
            root = canvas.gameObject;

            Image dim = UIFactory.CreatePanel("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.6f));
            UIFactory.Stretch(dim.rectTransform);

            Image sheet = UIFactory.CreatePanel("Sheet", canvas.transform, Paper);
            UIFactory.Place(sheet.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(860f, 720f));
            UIFactory.AddVerticalLayout(sheet.gameObject, 14f, 48);

            // Шапка письма: розовая метка и «от кого».
            RectTransform head = UIFactory.CreateRow("Head", sheet.transform, 28f, 12f, 0);
            Image mark = UIFactory.CreatePanel("Mark", head, UIFactory.AccentColor);
            UIFactory.SetSize(mark, width: 28f, height: 6f);
            TextMeshProUGUI from = UIFactory.CreateText("From", head, "ПИСЬМО ОТ УПРАВЛЯЮЩЕЙ СЕТЬЮ", 16f, InkMuted);
            from.characterSpacing = 4f;
            UIFactory.SetSize(from, flexibleWidth: 1f);

            titleText = UIFactory.CreateText("Title", sheet.transform, "", 36f, Ink);
            titleText.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(titleText, height: 52f);

            bodyText = UIFactory.CreateText("Body", sheet.transform, "", 22f, Ink, TextAlignmentOptions.TopLeft);
            bodyText.lineSpacing = 8f;
            bodyText.enableAutoSizing = true;
            bodyText.fontSizeMin = 16f;
            bodyText.fontSizeMax = 22f;
            UIFactory.SetSize(bodyText, flexibleHeight: 1f);

            RectTransform footer = UIFactory.CreateRow("Footer", sheet.transform, UIFactory.ButtonHeight, 12f, 0);
            RectTransform spacer = UIFactory.CreateRect("Spacer", footer);
            UIFactory.SetSize(spacer, flexibleWidth: 1f);
            Button ok = UIFactory.CreateButton("Ok", footer, "Понятно", Close, UIFactory.AccentColor, 22f);
            UIFactory.SetSize(ok, width: 200f, height: UIFactory.ButtonHeight);
        }
    }
}
