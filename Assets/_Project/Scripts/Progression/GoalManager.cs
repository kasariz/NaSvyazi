using System.Text;
using NaSvyazi.Building;
using NaSvyazi.Core;
using NaSvyazi.Economy;
using NaSvyazi.Store;
using NaSvyazi.UI;
using UnityEngine;

namespace NaSvyazi.Progression
{
    /// <summary>Чем закончился день с точки зрения цели игры.</summary>
    public enum DayOutcome
    {
        Continue,   // играем дальше
        Victory,    // цель достигнута (показывается один раз)
        Bankrupt,   // баланс слишком долго в минусе
        Closed      // репутация упала до нуля — сеть закрывает салон
    }

    /// <summary>
    /// Цель игры и условия поражения.
    ///
    /// Цель — «Лучший салон города»: максимальный уровень, открытый второй зал,
    /// высокая репутация и запас денег. Проверяется в конце каждого дня.
    ///
    /// Поражение:
    /// • банкротство — баланс в минусе к концу дня несколько дней подряд (по сложности: 5 / 3 / 2);
    /// • закрытие — репутация к концу дня упала до нуля.
    /// После победы можно продолжить игру без цели (свободный режим).
    /// </summary>
    public class GoalManager : MonoBehaviour
    {
        public static GoalManager Instance { get; private set; }

        [Header("Цель «Лучший салон города»")]
        [SerializeField] private bool requireMaxLevel = true;
        [Tooltip("Этап торгового зала, который нужно открыть (3 — второй зал)")]
        [SerializeField] private int targetSalonStage = 3;
        [SerializeField] private int targetReputation = 80;
        [SerializeField] private int targetBalance = 100000;


        /// <summary>Сколько дней подряд день закончился с отрицательным балансом.</summary>
        public int DebtDays { get; private set; }

        /// <summary>Цель уже достигнута (дальше — свободный режим).</summary>
        public bool GoalReached { get; private set; }

        /// <summary>Дней в минусе до банкротства — зависит от сложности (лёгкая 5, обычная 3, сложная 2).</summary>
        public int MaxDebtDays => Core.GameDifficulty.DebtDays;

        private GameEndUI endUI;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }
            Instance = this;

            // Окно победы/поражения собирается из кода — в сцене ничего настраивать не нужно.
            GameObject ui = new GameObject("GameEndUI");
            ui.transform.SetParent(transform, false);
            endUI = ui.AddComponent<GameEndUI>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Подвести итог дня: обновить счётчик долга и проверить цель.
        /// Вызывается после списания аренды и зарплаты.
        /// </summary>
        public DayOutcome EvaluateDay()
        {
            int balance = EconomyManager.Instance != null ? EconomyManager.Instance.Balance : 0;
            int reputation = ReputationManager.Instance != null ? ReputationManager.Instance.Value : 50;

            DebtDays = balance < 0 ? DebtDays + 1 : 0;

            if (reputation <= 0) return DayOutcome.Closed;
            if (DebtDays >= MaxDebtDays) return DayOutcome.Bankrupt;

            if (!GoalReached && IsGoalComplete())
            {
                GoalReached = true;
                return DayOutcome.Victory;
            }
            return DayOutcome.Continue;
        }

        /// <summary>Показать окно победы или поражения. onContinue — продолжение игры после победы.</summary>
        public void ShowOutcome(DayOutcome outcome, int day, System.Action onContinue)
        {
            endUI.Show(outcome, day, onContinue);
        }

        // --- Условия цели ---

        public bool LevelDone => !requireMaxLevel || (LevelManager.Instance != null && LevelManager.Instance.IsMaxLevel);
        public bool HallDone => ExpansionManager.Instance != null && ExpansionManager.Instance.SalonStage >= targetSalonStage;
        public bool ReputationDone => ReputationManager.Instance != null && ReputationManager.Instance.Value >= targetReputation;
        public bool MoneyDone => EconomyManager.Instance != null && EconomyManager.Instance.Balance >= targetBalance;

        public bool IsGoalComplete() => LevelDone && HallDone && ReputationDone && MoneyDone;

        /// <summary>Текст плашки цели в интерфейсе.</summary>
        public string BuildGoalText()
        {
            StringBuilder sb = new StringBuilder();

            int balance = EconomyManager.Instance != null ? EconomyManager.Instance.Balance : 0;
            if (balance < 0 || DebtDays > 0)
            {
                int left = Mathf.Max(0, MaxDebtDays - DebtDays);
                sb.AppendLine($"<color=#F25959><b>Долг! Выйдите в плюс к концу дня.</b>  " +
                              $"До банкротства: {left} {DaysWord(left)}</color>");
            }

            if (GoalReached)
            {
                sb.Append("<b>Цель достигнута</b> — свободная игра");
                return sb.ToString();
            }

            int maxLevel = LevelManager.Instance != null ? LevelManager.Instance.MaxLevel : 0;
            int reputation = ReputationManager.Instance != null ? ReputationManager.Instance.Value : 0;

            sb.AppendLine("<b>Цель: лучший салон города</b>");
            if (requireMaxLevel) sb.AppendLine(Line(LevelDone, $"Уровень салона {maxLevel}"));
            sb.AppendLine(Line(HallDone, "Открыть второй зал"));
            sb.AppendLine(Line(ReputationDone, $"Репутация {targetReputation}+  ({reputation})"));
            sb.Append(Line(MoneyDone, $"На счёте {EconomyManager.Format(targetBalance)}"));
            return sb.ToString();
        }

        private static string Line(bool done, string text) =>
            done ? $"<color=#59D973>• {text} — готово</color>" : $"<color=#A6ABB8>•</color> {text}";

        public static string DaysWord(int n)
        {
            int m10 = n % 10, m100 = n % 100;
            if (m10 == 1 && m100 != 11) return "день";
            if (m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14)) return "дня";
            return "дней";
        }

        // --- Сохранение ---

        public void SetState(bool goalReached, int debtDays)
        {
            GoalReached = goalReached;
            DebtDays = Mathf.Max(0, debtDays);
        }

        /// <summary>Добавить менеджер цели, если в сцене его ещё нет.</summary>
        public static void Ensure(GameObject host)
        {
            if (Instance == null && FindAnyObjectByType<GoalManager>() == null) host.AddComponent<GoalManager>();
        }
    }
}
