using UnityEngine;

namespace NaSvyazi.Core
{
    public enum Difficulty
    {
        Easy,    // как в версиях 0.1–0.2: можно спокойно осваиваться
        Normal,  // реалистичные наценки, налог, ощутимая аренда
        Hard     // дорогой товар, придирчивые покупатели, мало права на ошибку
    }

    /// <summary>
    /// Уровень сложности и все его множители. Выбирается в главном меню при новой игре,
    /// сохраняется вместе с игрой. Остальные системы читают готовые значения
    /// (закупочные цены, аренда, налог, терпимость покупателей и т. д.).
    ///
    /// Почему «Обычная» сложнее прежней игры: товар закупался втрое дешевле рыночной цены,
    /// а аренда была копеечной — деньги копились быстрее, чем их можно было потратить.
    /// Теперь закупка дороже, есть налог с выручки (как УСН 6 %), аренда и зарплаты выше,
    /// покупатели чувствительнее к наценке, а опыт идёт медленнее.
    /// </summary>
    public static class GameDifficulty
    {
        private const string PrefsKey = "nasvyazi.difficulty";

        private static Difficulty current;
        private static bool loaded;

        /// <summary>Текущая сложность (читается из настроек при первом обращении).</summary>
        public static Difficulty Current
        {
            get
            {
                if (!loaded) { current = Load(); loaded = true; }
                return current;
            }
        }

        private struct Settings
        {
            public string name, description;
            public int startMoney;
            public float marginCut, rent, wages, expansion, xp, tolerance, customerInterval, reputationLoss, tax;
            public int debtDays;
        }

        private static readonly Settings[] All =
        {
            new Settings
            {
                name = "Лёгкая",
                description = "Большая наценка, низкая аренда, без налога. Для знакомства с игрой.",
                startMoney = 8000, marginCut = 0f, rent = 1f, wages = 1f, expansion = 1f, xp = 1f,
                tolerance = 0f, customerInterval = 1f, reputationLoss = 1f, tax = 0f, debtDays = 5
            },
            new Settings
            {
                name = "Обычная",
                description = "Реалистичная наценка, налог 6 % с выручки, аренда и зарплаты выше. Развитие заметно медленнее.",
                startMoney = 6000, marginCut = 0.35f, rent = 2.5f, wages = 1.3f, expansion = 1.35f, xp = 0.8f,
                tolerance = -0.06f, customerInterval = 1.1f, reputationLoss = 1.25f, tax = 0.06f, debtDays = 3
            },
            new Settings
            {
                name = "Сложная",
                description = "Дорогой товар, налог 8 %, высокая аренда, придирчивые покупатели. Одна ошибка — и в минус.",
                startMoney = 4500, marginCut = 0.5f, rent = 3.5f, wages = 1.6f, expansion = 1.7f, xp = 0.65f,
                tolerance = -0.1f, customerInterval = 1.25f, reputationLoss = 1.5f, tax = 0.08f, debtDays = 2
            },
        };

        private static Settings S => All[(int)Current];

        public static string Name(Difficulty d) => All[(int)d].name;
        public static string Description(Difficulty d) => All[(int)d].description;
        public static string CurrentName => S.name;

        /// <summary>Стартовый капитал новой игры.</summary>
        public static int StartMoney => S.startMoney;
        /// <summary>
        /// Какую долю разницы между закупкой и рыночной ценой «съедает» поставщик:
        /// закупка = базовая + (рыночная − базовая) × доля. 0 — как раньше (наценка до 200 %),
        /// 0,35 — обычная (кабель 255 ₽ при рыночной 450), 0,5 — сложная.
        /// </summary>
        public static float MarginCut => S.marginCut;

        public static int PurchasePrice(int basePurchase, int marketPrice) =>
            basePurchase + Mathf.RoundToInt(Mathf.Max(0, marketPrice - basePurchase) * MarginCut);
        public static float RentMultiplier => S.rent;
        public static float WageMultiplier => S.wages;
        /// <summary>Множитель цены расширений и оборудования.</summary>
        public static float ExpansionMultiplier => S.expansion;
        public static float XpMultiplier => S.xp;
        /// <summary>Сдвиг порога терпимости цены (отрицательный — покупатели экономнее).</summary>
        public static float ToleranceShift => S.tolerance;
        /// <summary>Множитель интервала между покупателями (больше — реже приходят).</summary>
        public static float CustomerIntervalMultiplier => S.customerInterval;
        /// <summary>Множитель потери репутации.</summary>
        public static float ReputationLossMultiplier => S.reputationLoss;
        /// <summary>Налог с дневной выручки (доля).</summary>
        public static float TaxRate => S.tax;
        /// <summary>Сколько дней подряд можно быть в минусе до банкротства.</summary>
        public static int DebtDays => S.debtDays;

        public static int ScalePrice(int price) => Mathf.RoundToInt(price * ExpansionMultiplier / 100f) * 100;

        /// <summary>Выбрать сложность (в главном меню перед новой игрой или при загрузке сохранения).</summary>
        public static void Set(Difficulty difficulty)
        {
            current = difficulty;
            loaded = true;
            PlayerPrefs.SetInt(PrefsKey, (int)difficulty);
            PlayerPrefs.Save();
        }

        private static Difficulty Load()
        {
            int v = PlayerPrefs.GetInt(PrefsKey, (int)Difficulty.Normal);
            return v >= 0 && v < All.Length ? (Difficulty)v : Difficulty.Normal;
        }
    }
}
