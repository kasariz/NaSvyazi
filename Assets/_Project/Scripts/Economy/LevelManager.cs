using NaSvyazi.Audio;
using NaSvyazi.Core;
using UnityEngine;

namespace NaSvyazi.Economy
{
    /// <summary>
    /// Уровень салона. Опыт начисляется за проданные товары, обслуженных покупателей
    /// и удачные консультации. Новые уровни открывают категории товаров, мебель,
    /// расширение зала и найм персонала, но и аренда с уровнем растёт.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Tooltip("Сколько всего опыта нужно для уровня 2, 3, 4… (первый элемент — уровень 1). " +
                 "Версия 0.2: развитие медленнее — тестировщики дошли до максимума слишком быстро.")]
        [SerializeField] private int[] xpThresholds = { 0, 100, 280, 550, 900, 1400, 2000, 2800 };

        [Tooltip("После максимального уровня каждые столько опыта дают дополнительное очко навыка")]
        [SerializeField] private int masteryXpStep = 600;

        public int Level { get; private set; } = 1;
        public int Xp { get; private set; }
        public int MaxLevel => xpThresholds.Length;
        public bool IsMaxLevel => Level >= MaxLevel;

        /// <summary>
        /// Сколько всего опыта нужно для следующего уровня.
        /// На максимальном уровне раньше возвращался порог последнего уровня, а опыт продолжал расти —
        /// отсюда «1250/1000» у тестировщиков. Теперь на максимуме опыт идёт в «мастерство».
        /// </summary>
        public int XpForNextLevel => IsMaxLevel ? MaxLevelXp + (MasteryPoints + 1) * masteryXpStep : xpThresholds[Level];

        /// <summary>Опыт, с которым достигнут максимальный уровень.</summary>
        public int MaxLevelXp => xpThresholds[MaxLevel - 1];

        /// <summary>Очки мастерства: дополнительные очки навыков за опыт после максимального уровня.</summary>
        public int MasteryPoints => IsMaxLevel ? Mathf.Max(0, (Xp - MaxLevelXp) / masteryXpStep) : 0;

        /// <summary>Прогресс к следующему очку мастерства: (набрано, нужно).</summary>
        public int MasteryProgress => IsMaxLevel ? (Xp - MaxLevelXp) % masteryXpStep : 0;
        public int MasteryStep => masteryXpStep;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start() => Raise();

        /// <summary>Текущий уровень (если менеджера нет — считаем, что всё открыто).</summary>
        public static int CurrentLevel => Instance != null ? Instance.Level : 99;

        public static void AddXp(int amount, string reason)
        {
            if (Instance != null) Instance.Gain(amount, reason);
        }

        private void Gain(int amount, string reason)
        {
            if (amount <= 0) return;
            amount = Mathf.Max(1, Mathf.RoundToInt(amount * GameDifficulty.XpMultiplier));
            int masteryBefore = MasteryPoints;
            Xp += amount;

            while (!IsMaxLevel && Xp >= xpThresholds[Level])
            {
                Level++;
                GameEvents.Notify($"Новый уровень салона: {Level}! Загляните в терминал — открылись новые возможности.");
                SoundManager.Play(Sfx.Purchase);
            }

            if (MasteryPoints > masteryBefore)
            {
                GameEvents.Notify("Мастерство: получено дополнительное очко навыка! Клавиша K.");
                SoundManager.Play(Sfx.Purchase);
            }

            Raise();
        }

        /// <summary>Восстановить уровень и опыт (при загрузке).</summary>
        public void SetState(int level, int xp)
        {
            Level = Mathf.Clamp(level, 1, MaxLevel);
            Xp = Mathf.Max(0, xp);
            Raise();
        }

        private void Raise() => GameEvents.RaiseLevelChanged(Level, Xp, XpForNextLevel);
    }
}
