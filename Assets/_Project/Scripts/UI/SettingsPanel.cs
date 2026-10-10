using System;
using NaSvyazi.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NaSvyazi.UI
{
    /// <summary>
    /// Панель настроек (общая для главного меню и меню паузы):
    /// громкость, чувствительность мыши, полноэкранный режим.
    /// </summary>
    public static class SettingsPanel
    {
        public static GameObject Build(Transform parent, Action onBack)
        {
            RectTransform panel = UIFactory.CreateRect("Settings", parent);
            UIFactory.Stretch(panel);
            UIFactory.AddVerticalLayout(panel.gameObject, 10f, 0);

            TextMeshProUGUI title = UIFactory.CreateText("Title", panel, "Настройки", 36f, UIFactory.TextColor);
            title.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(title, height: 56f);

            AddSliderRow(panel, "Громкость", 0f, 1f, GameSettings.MasterVolume,
                v => GameSettings.MasterVolume = v, v => $"{v * 100f:0} %");

            AddSliderRow(panel, "Музыка", 0f, 1f, GameSettings.MusicVolume,
                v => GameSettings.MusicVolume = v, v => $"{v * 100f:0} %");

            AddSliderRow(panel, "Чувствительность мыши", 0.2f, 3f, GameSettings.MouseSensitivity,
                v => GameSettings.MouseSensitivity = v, v => $"×{v:0.0}");

            AddCycleButton(panel, () => $"Качество графики: {QualitySettings.names[QualitySettings.GetQualityLevel()]}",
                () => GameSettings.QualityLevel = (QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length);

            AddCycleButton(panel, () => $"Разрешение: {Screen.width}×{Screen.height}", NextResolution);

            AddCycleButton(panel, () => $"Вертикальная синхронизация: {(GameSettings.VSync ? "вкл." : "выкл.")}",
                () => GameSettings.VSync = !GameSettings.VSync);

            Button fullscreen = null;
            fullscreen = UIFactory.CreateButton("Fullscreen", panel, FullscreenText(), () =>
            {
                GameSettings.Fullscreen = !GameSettings.Fullscreen;
                fullscreen.GetComponentInChildren<TextMeshProUGUI>().text = FullscreenText();
            }, UIFactory.ButtonColor);
            UIFactory.SetSize(fullscreen, height: 48f);

            RectTransform spacer = UIFactory.CreateRect("Spacer", panel);
            UIFactory.SetSize(spacer, flexibleHeight: 1f);

            Button back = UIFactory.CreateButton("Back", panel, "Назад", onBack, UIFactory.AccentColor);
            UIFactory.SetSize(back, height: 56f);

            return panel.gameObject;
        }

        /// <summary>Кнопка, которая по нажатию переключает значение и обновляет свою подпись.</summary>
        private static void AddCycleButton(Transform parent, Func<string> text, Action next)
        {
            Button button = null;
            button = UIFactory.CreateButton("Cycle", parent, text(), () =>
            {
                next();
                button.GetComponentInChildren<TextMeshProUGUI>().text = text();
            }, UIFactory.ButtonColor, 22f);
            UIFactory.SetSize(button, height: 48f);
        }

        /// <summary>Следующее доступное разрешение экрана (по кругу).</summary>
        private static void NextResolution()
        {
            Resolution[] all = Screen.resolutions;
            if (all.Length == 0) return;

            int current = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i].width == Screen.width && all[i].height == Screen.height) current = i;

            // Пропускаем дубликаты с другой частотой обновления.
            for (int step = 1; step <= all.Length; step++)
            {
                Resolution next = all[(current + step) % all.Length];
                if (next.width != Screen.width || next.height != Screen.height)
                {
                    GameSettings.SetResolution(next.width, next.height);
                    return;
                }
            }
        }

        private static string FullscreenText() =>
            $"Полноэкранный режим: {(GameSettings.Fullscreen ? "вкл." : "выкл.")}";

        private static void AddSliderRow(Transform parent, string label, float min, float max, float value,
            Action<float> apply, Func<float, string> format)
        {
            RectTransform row = UIFactory.CreateRect($"Row_{label}", parent);
            UIFactory.AddHorizontalLayout(row.gameObject, 16f, 0);
            UIFactory.SetSize(row, height: 42f);

            TextMeshProUGUI name = UIFactory.CreateText("Label", row, label, 24f, UIFactory.TextColor);
            UIFactory.SetSize(name, width: 300f);

            TextMeshProUGUI valueText = UIFactory.CreateText("Value", row, format(value), 22f,
                UIFactory.MutedTextColor, TextAlignmentOptions.MidlineRight);

            Slider slider = UIFactory.CreateSlider("Slider", row, min, max, value, v =>
            {
                apply(v);
                valueText.text = format(v);
            });
            UIFactory.SetSize(slider, flexibleWidth: 1f);

            // Подпись значения — справа от ползунка.
            valueText.transform.SetAsLastSibling();
            UIFactory.SetSize(valueText, width: 90f);
        }
    }
}
