using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NaSvyazi.UI
{
    /// <summary>
    /// Построение интерфейса из кода: холсты, панели, тексты, кнопки, раскладки.
    /// Все окна игры собираются этими методами, поэтому стиль задаётся в одном месте.
    /// </summary>
    public static class UIFactory
    {
        // Палитра интерфейса (тёмная тема с акцентным цветом).
        public static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.11f, 0.96f);
        public static readonly Color RowColor = new Color(1f, 1f, 1f, 0.05f);
        public static readonly Color ButtonColor = new Color(0.22f, 0.24f, 0.28f, 1f);
        public static readonly Color AccentColor = new Color(0.86f, 0.18f, 0.55f, 1f);
        public static readonly Color TextColor = new Color(0.95f, 0.95f, 0.96f, 1f);
        public static readonly Color MutedTextColor = new Color(0.65f, 0.67f, 0.72f, 1f);
        public static readonly Color PositiveColor = new Color(0.35f, 0.85f, 0.45f, 1f);
        public static readonly Color NegativeColor = new Color(0.95f, 0.35f, 0.35f, 1f);

        /// <summary>Холст поверх экрана с масштабированием под 1920×1080.</summary>
        public static Canvas CreateCanvas(string name, int sortingOrder, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Растянуть элемент на всего родителя с отступом.</summary>
        public static void Stretch(RectTransform rt, float padding = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        /// <summary>Разместить элемент с привязкой к точке родителя.</summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static Image CreatePanel(string name, Transform parent, Color color)
        {
            RectTransform rt = CreateRect(name, parent);
            Image image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize,
            Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            RectTransform rt = CreateRect(name, parent);
            TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static Button CreateButton(string name, Transform parent, string label, Action onClick,
            Color color, float fontSize = 24f)
        {
            Image image = CreatePanel(name, parent, color);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            TextMeshProUGUI text = CreateText("Label", image.transform, label, fontSize, TextColor,
                TextAlignmentOptions.Center);
            Stretch(text.rectTransform);

            button.onClick.AddListener(() =>
            {
                Audio.SoundManager.Play(Audio.Sfx.Click, 0.5f);
                onClick?.Invoke();
            });
            return button;
        }

        /// <summary>Горизонтальный ползунок с подписью значения.</summary>
        public static Slider CreateSlider(string name, Transform parent, float min, float max, float value,
            Action<float> onChanged)
        {
            Image background = CreatePanel(name, parent, new Color(1f, 1f, 1f, 0.12f));
            Slider slider = background.gameObject.AddComponent<Slider>();

            RectTransform fillArea = CreateRect("Fill Area", background.transform);
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(5f, 6f);
            fillArea.offsetMax = new Vector2(-15f, -6f);

            Image fill = CreatePanel("Fill", fillArea, AccentColor);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.sizeDelta = new Vector2(10f, 0f);

            RectTransform handleArea = CreateRect("Handle Slide Area", background.transform);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);

            Image handle = CreatePanel("Handle", handleArea, TextColor);
            handle.rectTransform.anchorMin = Vector2.zero;
            handle.rectTransform.anchorMax = new Vector2(0f, 1f);
            handle.rectTransform.sizeDelta = new Vector2(22f, 0f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            if (onChanged != null) slider.onValueChanged.AddListener(v => onChanged(v));
            return slider;
        }

        /// <summary>Задать желаемый размер элемента внутри раскладки (−1 — не задавать).</summary>
        public static LayoutElement SetSize(Component c, float width = -1f, float height = -1f,
            float flexibleWidth = -1f, float flexibleHeight = -1f)
        {
            LayoutElement le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;

            // Если задан точный размер, элемент не должен растягиваться: иначе вложенная раскладка
            // (например, ряд кнопок) сообщает «гибкость» и забирает свободное место у соседей.
            le.flexibleWidth = flexibleWidth >= 0f ? flexibleWidth : (width >= 0f ? 0f : -1f);
            le.flexibleHeight = flexibleHeight >= 0f ? flexibleHeight : (height >= 0f ? 0f : -1f);
            return le;
        }

        public static VerticalLayoutGroup AddVerticalLayout(GameObject go, float spacing, int padding)
        {
            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup AddHorizontalLayout(GameObject go, float spacing, int padding)
        {
            HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return layout;
        }
    }
}
