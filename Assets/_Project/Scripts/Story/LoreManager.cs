using System.Collections.Generic;
using NaSvyazi.Building;
using NaSvyazi.Core;
using NaSvyazi.Economy;
using NaSvyazi.Player;
using NaSvyazi.Store;
using NaSvyazi.UI;
using UnityEngine;

namespace NaSvyazi.Story
{
    /// <summary>Письмо управляющей сетью: заголовок и текст.</summary>
    public class Letter
    {
        public string id;
        public string title;
        public string body;
    }

    /// <summary>
    /// Сюжет: письма от Марины Сергеевны, управляющей сетью салонов «На связи».
    /// Приходят в ключевые моменты (первый день, новые уровни, расширение) и показываются
    /// утром, пока салон закрыт, — чтобы не мешать работе. Прочитанные письма сохраняются.
    /// </summary>
    public class LoreManager : MonoBehaviour
    {
        public static LoreManager Instance { get; private set; }

        [SerializeField] private LetterUI letterUI;

        private const string Signature = "\n\n<i>Марина Сергеевна,\nуправляющая сетью «На связи»</i>";

        public static readonly List<Letter> Letters = new List<Letter>
        {
            new Letter
            {
                id = "intro", title = "Добро пожаловать в сеть!",
                body = "Поздравляю: с сегодняшнего дня этот салон — ваш. Да, он пока крошечный: восемь на десять метров, " +
                       "один стеллаж и склад размером с кладовку. Прошлый управляющий ушёл открывать шаурмичную, " +
                       "так что покупатели здесь отвыкли от хорошего сервиса.\n\n" +
                       "Задача простая: закупайте товар, держите честные цены, подбирайте людям тарифы, " +
                       "которые им действительно нужны. Через месяц я жду отчёт — и, надеюсь, второй зал." + Signature
            },
            new Letter
            {
                id = "day2", title = "Про соседей и чистоту",
                body = "Видела отчёт за первый день — неплохо для начала.\n\n" +
                       "Пара советов. Слева от вас кофейня «Зерно»: Гоша варит лучший эспрессо в квартале, " +
                       "после его кофе носишься по залу заметно быстрее (проверено на мне). Справа — цветочный Веры: " +
                       "букет на прилавке творит чудеса с настроением покупателей.\n\n" +
                       "И следите за полом. Швабра на складе у двери в зал. Грязный салон — минус репутация." + Signature
            },
            new Letter
            {
                id = "level3", title = "О вас заговорили",
                body = "В отделе маркетинга заметили рост вашей репутации — поздравляю, уровень салона растёт.\n\n" +
                       "Самое время подумать о смартфонах: витрина, демо-стол, правильные цены. " +
                       "Ходят слухи, что «МегаСвязь» присматривает помещение на вашей улице. " +
                       "Конкуренции бояться не надо — надо быть лучше." + Signature
            },
            new Letter
            {
                id = "expand1", title = "Стены раздвинулись",
                body = "Видела фото расширенного зала — совсем другое дело! Больше места — больше стеллажей, " +
                       "а значит, и выручки. Не забывайте только, что аренда тоже выросла: " +
                       "пустой зал никому не нужен, заполняйте его товаром." + Signature
            },
            new Letter
            {
                id = "level5", title = "«МегаСвязь» открылась",
                body = "Как я и говорила: «МегаСвязь» открылась через два квартала. Цены у них ниже, " +
                       "зато консультанты путают тарифы и грубят.\n\n" +
                       "Наше оружие — сервис: быстрая касса, точные консультации, чистый зал и честные цены. " +
                       "Держите репутацию высокой — покупатели это видят." + Signature
            },
            new Letter
            {
                id = "hall2", title = "Второй зал!",
                body = "Вы открыли второй зал — в нашей сети так быстро это удавалось немногим. " +
                       "Сделайте из него зал смартфонов: витрины, демо-столы, хороший свет. " +
                       "Люди приходят за новинками — пусть уходят с покупками." + Signature
            },
            new Letter
            {
                id = "final", title = "Лучший салон города",
                body = "По итогам квартала ваш салон — первый в городе по выручке и отзывам. " +
                       "«МегаСвязь» закрывает свою точку и, говорят, переманивает ваших покупателей скидками — безуспешно.\n\n" +
                       "Совет директоров предлагает вам открыть ещё один салон. Подумайте. " +
                       "А пока — спасибо. Вы показали, каким должен быть салон «На связи»." + Signature
            },
        };

        private readonly HashSet<string> shown = new HashSet<string>();
        private readonly List<string> queue = new List<string>();
        private float checkTimer = 1.5f; // первое письмо — через полторы секунды после запуска
        private int lastLetterDay = -1;  // не больше одного письма за утро

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            checkTimer -= Time.deltaTime;
            if (checkTimer > 0f) return;
            checkTimer = 1f;

            CheckTriggers();
            TryShowNext();
        }

        private void CheckTriggers()
        {
            DayManager day = DayManager.Instance;
            int level = LevelManager.CurrentLevel;
            ExpansionManager layout = ExpansionManager.Instance;

            Enqueue("intro");
            if (day != null && day.Day >= 2) Enqueue("day2");
            if (level >= 3 && level < 99) Enqueue("level3");
            if (layout != null && layout.SalonStage >= 1) Enqueue("expand1");
            if (level >= 5 && level < 99) Enqueue("level5");
            if (layout != null && layout.SalonStage >= 3) Enqueue("hall2");
            // Письмо «final» больше не приходит само: его текст показывается в окне победы (GoalManager).
        }

        private void Enqueue(string id)
        {
            if (!shown.Contains(id) && !queue.Contains(id)) queue.Add(id);
        }

        /// <summary>Письма показываются утром (салон ещё закрыт) и когда не открыто другое окно.</summary>
        private void TryShowNext()
        {
            if (queue.Count == 0 || letterUI == null || ModalWindow.IsAnyOpen || FurniturePlacer.IsPlacing) return;
            DayManager day = DayManager.Instance;
            if (day != null && (day.Phase != DayPhase.Preparing || day.Day == lastLetterDay)) return;
            if (day != null) lastLetterDay = day.Day;

            Letter letter = Letters.Find(l => l.id == queue[0]);
            queue.RemoveAt(0);
            if (letter == null) return;

            shown.Add(letter.id);
            letterUI.Show(letter, FindAnyObjectByType<PlayerController>());
        }

        // --- Сохранение ---

        public List<string> GetShown() => new List<string>(shown);

        public void SetShown(IEnumerable<string> ids)
        {
            shown.Clear();
            foreach (string id in ids) shown.Add(id);
            queue.RemoveAll(id => shown.Contains(id));
        }
    }
}
