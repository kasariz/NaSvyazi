using System;
using System.Collections.Generic;
using NaSvyazi.Audio;
using NaSvyazi.Core;
using NaSvyazi.Economy;
using UnityEngine;

namespace NaSvyazi.Progression
{
    /// <summary>Ветки дерева навыков.</summary>
    public enum SkillBranch
    {
        Service,   // Сервис: терпение покупателей, кассир, консультации
        Marketing, // Маркетинг: поток покупателей, объём покупок, готовность платить
        Logistics, // Логистика: доставка, закупочные цены, аренда
        Staff      // Персонал: выкладчик, уборщица, охранник, зарплаты
    }

    /// <summary>Описание навыка: ветка, ступень, максимальный ранг, требование.</summary>
    public class SkillDefinition
    {
        public string id;
        public string name;
        public string description; // что даёт каждый ранг
        public SkillBranch branch;
        public int tier;           // 1..3 — ступень в ветке
        public int maxRank;
        public string requires;    // навык предыдущей ступени (хотя бы 1 ранг)
    }

    /// <summary>
    /// Дерево навыков салона. Очко навыка даётся за каждый уровень салона
    /// (на первом уровне — одно стартовое). Навыки следующей ступени ветки
    /// открываются, когда вложен хотя бы один ранг в предыдущую.
    /// Остальные системы читают готовые множители (PatienceMultiplier и т. д.).
    /// </summary>
    public class SkillManager : MonoBehaviour
    {
        public static SkillManager Instance { get; private set; }

        public static readonly List<SkillDefinition> Definitions = new List<SkillDefinition>
        {
            // --- Сервис ---
            new SkillDefinition { id = "politeness", name = "Вежливость", branch = SkillBranch.Service, tier = 1, maxRank = 2,
                description = "Покупатели в очереди терпеливее: +25 % терпения за ранг." },
            new SkillDefinition { id = "fast_cashier", name = "Обучение кассира", branch = SkillBranch.Service, tier = 2, maxRank = 2,
                requires = "politeness", description = "Нанятый кассир работает быстрее: +30 % скорости за ранг." },
            new SkillDefinition { id = "tariff_expert", name = "Знаток тарифов", branch = SkillBranch.Service, tier = 3, maxRank = 1,
                requires = "fast_cashier", description = "В консультации подсвечивается выгодный тариф, а кассир не ошибается с тарифами." },

            // --- Маркетинг ---
            new SkillDefinition { id = "word_of_mouth", name = "Сарафанное радио", branch = SkillBranch.Marketing, tier = 1, maxRank = 2,
                description = "Покупатели приходят чаще: −10 % к интервалу за ранг." },
            new SkillDefinition { id = "loyalty", name = "Программа лояльности", branch = SkillBranch.Marketing, tier = 2, maxRank = 2,
                requires = "word_of_mouth", description = "Покупатель хочет купить больше: +1 товар в списке за ранг." },
            new SkillDefinition { id = "price_tags", name = "Яркие ценники", branch = SkillBranch.Marketing, tier = 3, maxRank = 2,
                requires = "loyalty", description = "Покупатели легче принимают наценку: +5 % к терпимости цены за ранг." },

            // --- Логистика ---
            new SkillDefinition { id = "fast_delivery", name = "Быстрая доставка", branch = SkillBranch.Logistics, tier = 1, maxRank = 2,
                description = "Коробки приезжают быстрее: −30 % ко времени доставки за ранг." },
            new SkillDefinition { id = "wholesale", name = "Оптовые скидки", branch = SkillBranch.Logistics, tier = 2, maxRank = 2,
                requires = "fast_delivery", description = "Закупка у поставщика дешевле: −5 % за ранг." },
            new SkillDefinition { id = "lean_rent", name = "Переговоры с арендодателем", branch = SkillBranch.Logistics, tier = 3, maxRank = 2,
                requires = "wholesale", description = "Аренда дешевле: −10 % за ранг." },

            // --- Персонал ---
            new SkillDefinition { id = "merch_speed", name = "Расторопный выкладчик", branch = SkillBranch.Staff, tier = 1, maxRank = 2,
                description = "Выкладчик работает на 35 % быстрее и уносит на 3 шт. больше за ранг." },
            new SkillDefinition { id = "cleaner_speed", name = "Чистота — залог успеха", branch = SkillBranch.Staff, tier = 2, maxRank = 2,
                requires = "merch_speed", description = "Уборщица моет пол на 40 % быстрее за ранг." },
            new SkillDefinition { id = "staff_motivation", name = "Мотивация персонала", branch = SkillBranch.Staff, tier = 3, maxRank = 2,
                requires = "cleaner_speed", description = "Зарплаты всех сотрудников −10 %, охранник ловит воров на 15 % чаще за ранг." },
        };

        private readonly Dictionary<string, int> ranks = new Dictionary<string, int>();

        /// <summary>Сколько всего очков заработано: по одному на каждый уровень салона.</summary>
        /// После максимального уровня очки дают «мастерство» — пока не прокачано всё дерево.
        public int EarnedPoints => LevelManager.Instance != null
            ? Mathf.Min(LevelManager.Instance.Level + LevelManager.Instance.MasteryPoints, TotalRanks)
            : 1;

        /// <summary>Сколько очков нужно, чтобы прокачать всё дерево.</summary>
        public static int TotalRanks
        {
            get
            {
                int sum = 0;
                foreach (SkillDefinition d in Definitions) sum += d.maxRank;
                return sum;
            }
        }

        public int SpentPoints
        {
            get
            {
                int sum = 0;
                foreach (int r in ranks.Values) sum += r;
                return sum;
            }
        }

        public int AvailablePoints => Mathf.Max(0, EarnedPoints - SpentPoints);

        /// <summary>Вызывается при любом изменении навыков.</summary>
        public static event Action Changed;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable() => GameEvents.LevelChanged += OnLevelChanged;
        private void OnDisable() => GameEvents.LevelChanged -= OnLevelChanged;


        private void OnLevelChanged(int level, int xp, int next)
        {
            // О новом уровне и очке навыка сообщает баннер в HUD (GameHUD.ShowLevelUp).
            Changed?.Invoke();
        }

        public int RankOf(string id) => ranks.TryGetValue(id, out int r) ? r : 0;

        public static int Rank(string id) => Instance != null ? Instance.RankOf(id) : 0;

        public static SkillDefinition Find(string id) => Definitions.Find(d => d.id == id);

        public bool IsAvailable(SkillDefinition skill) =>
            string.IsNullOrEmpty(skill.requires) || RankOf(skill.requires) > 0;

        public bool CanUpgrade(SkillDefinition skill) =>
            AvailablePoints > 0 && RankOf(skill.id) < skill.maxRank && IsAvailable(skill);

        public bool TryUpgrade(string id)
        {
            SkillDefinition skill = Find(id);
            if (skill == null || !CanUpgrade(skill))
            {
                SoundManager.Play(Sfx.Error);
                return false;
            }

            ranks[id] = RankOf(id) + 1;
            SoundManager.Play(Sfx.Purchase, 0.6f);
            GameEvents.Notify($"Навык «{skill.name}» — ранг {ranks[id]}");
            Changed?.Invoke();
            return true;
        }

        // --- Сохранение ---

        public Dictionary<string, int> GetRanks() => new Dictionary<string, int>(ranks);

        public void SetRanks(IEnumerable<KeyValuePair<string, int>> values)
        {
            ranks.Clear();
            foreach (KeyValuePair<string, int> pair in values)
            {
                SkillDefinition d = Find(pair.Key);
                if (d != null && pair.Value > 0) ranks[pair.Key] = Mathf.Min(pair.Value, d.maxRank);
            }
            Changed?.Invoke();
        }

        // --- Эффекты навыков (читаются другими системами) ---

        /// <summary>Множитель терпения покупателей в очереди.</summary>
        public static float PatienceMultiplier => 1f + 0.25f * Rank("politeness");

        /// <summary>Множитель скорости нанятого кассира.</summary>
        public static float CashierSpeedMultiplier => 1f + 0.3f * Rank("fast_cashier");

        public static bool TariffExpert => Rank("tariff_expert") > 0;

        /// <summary>Множитель интервала между покупателями.</summary>
        public static float SpawnIntervalMultiplier => 1f - 0.1f * Rank("word_of_mouth");

        /// <summary>Сколько товаров дополнительно хочет купить покупатель.</summary>
        public static int ExtraWantedItems => Rank("loyalty");

        /// <summary>Прибавка к порогу терпимости цены.</summary>
        public static float ToleranceBonus => 0.05f * Rank("price_tags");

        /// <summary>Множитель времени доставки.</summary>
        public static float DeliveryTimeMultiplier => 1f - 0.3f * Rank("fast_delivery");

        /// <summary>Множитель закупочной цены.</summary>
        public static float PurchasePriceMultiplier => 1f - 0.05f * Rank("wholesale");

        /// <summary>Скорость выкладчика.</summary>
        public static float MerchandiserSpeedMultiplier => 1f + 0.35f * Rank("merch_speed");

        /// <summary>Скорость уборщицы.</summary>
        public static float CleanerSpeedMultiplier => 1f + 0.4f * Rank("cleaner_speed");

        /// <summary>Вероятность, что охранник задержит вора.</summary>
        public static float GuardCatchChance => Mathf.Min(0.95f, 0.6f + 0.15f * Rank("staff_motivation"));

        /// <summary>Множитель зарплат персонала.</summary>
        public static float StaffWageMultiplier => 1f - 0.1f * Rank("staff_motivation");

        /// <summary>Множитель аренды.</summary>
        public static float RentMultiplier => 1f - 0.1f * Rank("lean_rent");
    }
}
