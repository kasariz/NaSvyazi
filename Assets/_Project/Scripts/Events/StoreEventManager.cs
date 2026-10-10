using System.Collections.Generic;
using NaSvyazi.Core;
using NaSvyazi.Products;
using UnityEngine;

namespace NaSvyazi.Events
{
    /// <summary>Событие дня и его влияние на салон.</summary>
    public class StoreEvent
    {
        public string id;
        public string name;
        public string announcement;
        public string effects;                    // коротко для плашки в HUD

        public float spawnMultiplier = 1f;        // множитель интервала между покупателями (<1 — чаще)
        public float consultationMultiplier = 1f; // множитель доли клиентов за консультацией
        public int extraWantedItems;              // сколько товаров дополнительно хотят купить
        public float toleranceBonus;              // прибавка к терпимости цены для всех товаров
        public float phoneToleranceBonus;         // прибавка для смартфонов и аудио
        public bool favorPhones;                  // покупатели чаще хотят смартфоны и аудио
        public float deliveryMultiplier = 1f;     // множитель времени доставки
        public int minDay = 3;                    // с какого дня событие может случиться
    }

    /// <summary>
    /// События дня: каждое утро (начиная с третьего дня) с некоторой вероятностью
    /// выбирается событие — «Чёрная пятница», выход нового смартфона, дождь, сбой у поставщика.
    /// Событие объявляется утром, показывается в HUD и попадает в отчёт.
    /// </summary>
    public class StoreEventManager : MonoBehaviour
    {
        public static StoreEventManager Instance { get; private set; }

        [Range(0f, 1f)] [SerializeField] private float eventChance = 0.35f;

        public static readonly List<StoreEvent> Events = new List<StoreEvent>
        {
            new StoreEvent
            {
                id = "black_friday", name = "Чёрная пятница",
                announcement = "Сегодня «Чёрная пятница»: покупателей будет намного больше, и они берут больше товаров — но ждут скидок.",
                effects = "Покупателей больше · берут больше · ждут скидок",
                spawnMultiplier = 0.55f, extraWantedItems = 1, toleranceBonus = -0.08f
            },
            new StoreEvent
            {
                id = "phone_launch", name = "Выход нового смартфона",
                announcement = "Вышел новый смартфон! Покупатели охотнее берут смартфоны и аудио и готовы за них переплатить.",
                effects = "Спрос на смартфоны и аудио · готовы переплатить",
                spawnMultiplier = 0.8f, favorPhones = true, phoneToleranceBonus = 0.15f, minDay = 4
            },
            new StoreEvent
            {
                id = "rain", name = "Дождливый день",
                announcement = "На улице ливень: покупателей меньше, зато чаще заходят подобрать тариф.",
                effects = "Покупателей меньше · больше консультаций · пол грязнее",
                spawnMultiplier = 1.4f, consultationMultiplier = 1.8f
            },
            new StoreEvent
            {
                id = "supplier_delay", name = "Сбой у поставщика",
                announcement = "У поставщика сбой: доставка сегодня идёт втрое дольше. Заказывайте заранее!",
                effects = "Доставка в 3 раза дольше",
                deliveryMultiplier = 3f
            },
            new StoreEvent
            {
                id = "student_day", name = "День студента",
                announcement = "Сегодня день студента: много покупателей с ограниченным бюджетом — цены выше рынка их отпугнут.",
                effects = "Много покупателей · дорогие цены отпугивают",
                spawnMultiplier = 0.75f, toleranceBonus = -0.12f, consultationMultiplier = 1.3f
            },
        };

        public StoreEvent Current { get; private set; }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Выбрать событие на новый день (вызывается DayManager утром).</summary>
        public void RollForDay(int day)
        {
            Current = null;
            if (Random.value < eventChance)
            {
                List<StoreEvent> possible = Events.FindAll(e => day >= e.minDay);
                if (possible.Count > 0) Current = possible[Random.Range(0, possible.Count)];
            }

            if (Current != null) GameEvents.Notify($"Событие дня: {Current.name}. {Current.announcement}");
        }

        /// <summary>Восстановить событие (при загрузке).</summary>
        public void SetCurrent(string id) => Current = string.IsNullOrEmpty(id) ? null : Events.Find(e => e.id == id);

        // --- Эффекты (значения по умолчанию, если события нет) ---

        private static StoreEvent Active => Instance != null ? Instance.Current : null;

        public static string CurrentName => Active != null ? Active.name : null;
        public static string CurrentEffects => Active != null ? Active.effects : null;
        public static string CurrentId => Active != null ? Active.id : null;
        public static float SpawnMultiplier => Active != null ? Active.spawnMultiplier : 1f;
        public static float ConsultationMultiplier => Active != null ? Active.consultationMultiplier : 1f;
        public static int ExtraWantedItems => Active != null ? Active.extraWantedItems : 0;
        public static bool FavorPhones => Active != null && Active.favorPhones;
        public static float DeliveryMultiplier => Active != null ? Active.deliveryMultiplier : 1f;

        public static float ToleranceBonus(ProductCategory category)
        {
            StoreEvent e = Active;
            if (e == null) return 0f;
            return e.toleranceBonus + (category.RequiresShowcase() ? e.phoneToleranceBonus : 0f);
        }
    }
}
