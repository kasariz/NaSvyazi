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

        [Tooltip("Сколько всего опыта нужно для уровня 2, 3, 4… (первый элемент — уровень 1)")]
        [SerializeField] private int[] levelThresholds = { 0, 40, 120, 250, 450, 700, 1000 };

        public int Level { get; private set; } = 1;
        public int Xp { get; private set; }
        public int MaxLevel => levelThresholds.Length;
        public bool IsMaxLevel => Level >= MaxLevel;

        /// <summary>Сколько всего опыта нужно для следующего уровня.</summary>
        public int XpForNextLevel => IsMaxLevel ? levelThresholds[MaxLevel - 1] : levelThresholds[Level];

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
            Xp += amount;

            while (!IsMaxLevel && Xp >= levelThresholds[Level])
            {
                Level++;
                GameEvents.Notify($"Новый уровень салона: {Level}! Загляните в терминал — открылись новые возможности.");
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
