using System.Collections;
using NaSvyazi.Audio;
using NaSvyazi.Core;
using NaSvyazi.Store;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NaSvyazi.UI
{
    /// <summary>
    /// Обучение первого дня: последовательность подсказок, каждая закрывается,
    /// когда игрок сделал нужное действие (событие из GameEvents).
    /// Заказ → коробка → выкладка → цена → открытие салона → обслуживание покупателя.
    /// F1 — пропустить обучение.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        private enum Step { Order, PickUp, Shelve, Price, Open, Serve, Done }

        private static readonly string[] Texts =
        {
            "Закажите товар. Подойдите к монитору «Заказ товара» на прилавке и нажмите E. " +
            "Добавьте пару коробок кнопкой «+» и оформите заказ.",

            "Через несколько секунд коробки привезут на улицу — в жёлтую зону доставки у ворот склада. " +
            "Подойдите и возьмите коробку — E. Лишние коробки можно поставить на стеллаж склада (ЛКМ).",

            "Отнесите коробку к стеллажу, наведите прицел на полку и выложите товар — ЛКМ. " +
            "Кнопку можно удерживать.",

            "Проверьте цену: наведите прицел на полку с товаром и нажмите E. " +
            "Слишком высокая цена отпугнёт покупателей.",

            "Всё готово! Откройте салон: подойдите к табличке «Открыто / Закрыто» у входа (на стекле слева от двери) и нажмите E — она перевернётся.",

            "Встаньте за прилавок у кассы. Когда покупатель выложит товары, отсканируйте каждый (ЛКМ), " +
            "затем примите оплату (E)."
        };

        private const string FinalText =
            "Отлично, первый покупатель обслужен! Дальше следите за запасами, выбрасывайте пустые коробки в бак, " +
            "консультируйте по тарифам и развивайте салон через терминал: мебель, новые категории, расширение зала и склада.";

        private Step step = Step.Order;
        private bool active;

        private GameObject panel;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI bodyText;

        /// <summary>Обучение пройдено или пропущено (сохраняется в файле игры).</summary>
        public bool Completed { get; private set; }

        private void Awake()
        {
            Instance = this;
            Build();
            panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private IEnumerator Start()
        {
            // Ждём кадр: за это время SaveSystem успевает загрузить сохранение.
            yield return null;

            bool firstDay = DayManager.Instance == null || DayManager.Instance.Day == 1;
            if (!Completed && firstDay) Begin();
        }

        private void OnEnable()
        {
            GameEvents.OrderPlaced += OnOrderPlaced;
            GameEvents.BoxPickedUp += OnBoxPickedUp;
            GameEvents.ItemShelved += OnItemShelved;
            GameEvents.PriceWindowOpened += OnPriceWindowOpened;
            GameEvents.StoreOpenChanged += OnStoreOpenChanged;
            GameEvents.SaleCompleted += OnSaleCompleted;
        }

        private void OnDisable()
        {
            GameEvents.OrderPlaced -= OnOrderPlaced;
            GameEvents.BoxPickedUp -= OnBoxPickedUp;
            GameEvents.ItemShelved -= OnItemShelved;
            GameEvents.PriceWindowOpened -= OnPriceWindowOpened;
            GameEvents.StoreOpenChanged -= OnStoreOpenChanged;
            GameEvents.SaleCompleted -= OnSaleCompleted;
        }

        private void Update()
        {
            if (active && GameInput.TutorialSkipPressed) Finish(skipped: true);
        }

        /// <summary>Отметить обучение пройденным (при загрузке сохранения).</summary>
        public void SetCompleted(bool completed)
        {
            Completed = completed;
            if (completed && active)
            {
                active = false;
                panel.SetActive(false);
            }
        }

        // --- Шаги ---

        private void Begin()
        {
            active = true;
            step = Step.Order;
            panel.SetActive(true);
            ShowStep();
        }

        private void Advance(Step expected)
        {
            if (!active || step != expected) return;

            step++;
            SoundManager.Play(Sfx.Notify, 0.5f);

            if (step == Step.Done) Finish(skipped: false);
            else ShowStep();
        }

        private void ShowStep()
        {
            int index = (int)step;
            titleText.text = $"Обучение · шаг {index + 1} из {Texts.Length}";
            bodyText.text = Texts[index];
        }

        private void Finish(bool skipped)
        {
            active = false;
            Completed = true;

            if (skipped)
            {
                panel.SetActive(false);
                return;
            }

            titleText.text = "Обучение пройдено";
            bodyText.text = FinalText;
            StartCoroutine(HideLater(10f));
        }

        private IEnumerator HideLater(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (!active) panel.SetActive(false);
        }

        private void OnOrderPlaced() => Advance(Step.Order);
        private void OnBoxPickedUp() => Advance(Step.PickUp);
        private void OnItemShelved() => Advance(Step.Shelve);
        private void OnPriceWindowOpened() => Advance(Step.Price);
        private void OnSaleCompleted() => Advance(Step.Serve);

        private void OnStoreOpenChanged(bool open)
        {
            if (open) Advance(Step.Open);
        }

        // --- Интерфейс ---

        private void Build()
        {
            Canvas canvas = UIFactory.CreateCanvas("Tutorial Canvas", 15, transform);

            Image background = UIFactory.CreatePanel("Tutorial", canvas.transform, new Color(0.08f, 0.09f, 0.11f, 0.9f));
            UIFactory.Place(background.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(24f, 60f), new Vector2(560f, 250f));
            panel = background.gameObject;

            Image accent = UIFactory.CreatePanel("Accent", background.transform, UIFactory.AccentColor);
            accent.rectTransform.anchorMin = new Vector2(0f, 0f);
            accent.rectTransform.anchorMax = new Vector2(0f, 1f);
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            accent.rectTransform.sizeDelta = new Vector2(6f, 0f);

            RectTransform content = UIFactory.CreateRect("Content", background.transform);
            UIFactory.Stretch(content, 20f);
            UIFactory.AddVerticalLayout(content.gameObject, 8f, 0);

            titleText = UIFactory.CreateText("Title", content, "", 22f, UIFactory.AccentColor);
            titleText.fontStyle = FontStyles.Bold;
            UIFactory.SetSize(titleText, height: 30f);

            bodyText = UIFactory.CreateText("Body", content, "", 21f, UIFactory.TextColor, TextAlignmentOptions.TopLeft);
            UIFactory.SetSize(bodyText, flexibleHeight: 1f);

            TextMeshProUGUI hint = UIFactory.CreateText("Hint", content, "[F1] — пропустить обучение", 16f,
                UIFactory.MutedTextColor);
            UIFactory.SetSize(hint, height: 22f);
        }
    }
}
