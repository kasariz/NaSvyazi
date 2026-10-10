using NaSvyazi.Core;
using NaSvyazi.Customers;
using NaSvyazi.Economy;
using NaSvyazi.Events;
using NaSvyazi.Progression;
using NaSvyazi.Player;
using NaSvyazi.UI;
using UnityEngine;

namespace NaSvyazi.Store
{
    /// <summary>
    /// Рабочий день салона.
    /// Подготовка (время стоит) → игрок открывает салон табличкой → идёт время,
    /// приходят покупатели → в час закрытия новые не приходят → когда последний
    /// покупатель ушёл, списывается аренда и показывается отчёт → следующий день.
    /// </summary>
    public class DayManager : MonoBehaviour
    {
        public static DayManager Instance { get; private set; }

        [SerializeField] private int openHour = 9;
        [SerializeField] private int closeHour = 21;

        [Tooltip("Сколько реальных секунд длится одна игровая минута (0.4 → день с 9:00 до 21:00 ≈ 5 минут)")]
        [SerializeField] private float secondsPerGameMinute = 0.4f;

        [Tooltip("Базовая аренда и коммунальные платежи за день, руб.")]
        [SerializeField] private int dailyRent = 800;

        [Tooltip("Прибавка к аренде за каждый уровень салона выше первого, руб.")]
        [SerializeField] private int rentPerLevel = 300;

        [Tooltip("Если покупатели не ушли за это время после закрытия, день завершается принудительно")]
        [SerializeField] private float closingTimeout = 60f;

        [SerializeField] private DayReportUI reportUI;

        private float closingTimer;
        private int lastMinute = -1;

        public int Day { get; private set; } = 1;
        public float Minutes { get; private set; }
        public DayPhase Phase { get; private set; } = DayPhase.Preparing;
        /// <summary>
        /// Аренда за день: базовая + надбавка за уровень салона + доплата за второй зал.
        /// Чем больше салон, тем дороже его содержание — растущие расходы держат игрока в тонусе.
        /// </summary>
        public int DailyRent
        {
            get
            {
                int level = Economy.LevelManager.Instance != null ? Economy.LevelManager.Instance.Level : 1;
                int expansion = Building.ExpansionManager.Instance != null ? Building.ExpansionManager.Instance.ExtraRent : 0;
                int rent = Mathf.RoundToInt((dailyRent + rentPerLevel * (level - 1) + expansion) * GameDifficulty.RentMultiplier);
                // Навык «Переговоры с арендодателем» снижает аренду.
                return Mathf.RoundToInt(rent * SkillManager.RentMultiplier);
            }
        }

        /// <summary>Зарплата персонала за день.</summary>
        public int DailyWages => Building.StaffManager.Instance != null ? Building.StaffManager.Instance.DailyWages : 0;

        private void Awake()
        {
            Instance = this;
            Minutes = openHour * 60;

            // Цель игры и условия поражения (банкротство, нулевая репутация).
            GoalManager.Ensure(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            RaiseTime();
            GameEvents.Notify($"День {Day}. Подготовьте салон и откройте его табличкой у входа.");

            // Событие дня: «Чёрная пятница», выход нового смартфона, дождь и т. д.
            if (StoreEventManager.Instance != null) StoreEventManager.Instance.RollForDay(Day);
        }

        private void Update()
        {
            if (Phase == DayPhase.Open)
            {
                Minutes += Time.deltaTime / secondsPerGameMinute;
                if ((int)Minutes != lastMinute) RaiseTime();

                if (Minutes >= closeHour * 60) BeginClosing("Рабочий день окончен");
            }
            else if (Phase == DayPhase.Closing)
            {
                closingTimer += Time.deltaTime;
                Customer[] left = FindObjectsByType<Customer>(FindObjectsSortMode.None);

                if (left.Length == 0) EndDay();
                else if (closingTimer > closingTimeout)
                {
                    foreach (Customer c in left) Destroy(c.gameObject);
                    EndDay();
                }
            }
        }

        // --- Управление днём ---

        public void OpenStore()
        {
            if (Phase != DayPhase.Preparing) return;

            Phase = DayPhase.Open;
            GameEvents.RaiseStoreOpenChanged(true);
            GameEvents.Notify($"Салон открыт! Работаем до {closeHour}:00");
            RaiseTime();
        }

        public void CloseEarly()
        {
            if (Phase == DayPhase.Open) BeginClosing("Салон закрыт досрочно");
        }

        private void BeginClosing(string message)
        {
            Phase = DayPhase.Closing;
            closingTimer = 0f;
            GameEvents.RaiseStoreOpenChanged(false);
            GameEvents.Notify($"{message}. Обслужите оставшихся покупателей.");
            RaiseTime();
        }

        private void EndDay()
        {
            Phase = DayPhase.Summary;
            RaiseTime();

            int rent = DailyRent;
            int wages = DailyWages;

            EconomyManager economy = EconomyManager.Instance;
            if (economy != null)
            {
                economy.Charge(rent, "Аренда и коммунальные платежи");
                if (wages > 0) economy.Charge(wages, "Зарплата персонала");
            }

            // Налог с выручки (на обычной и сложной сложности), как УСН «доходы».
            int tax = economy != null ? Mathf.RoundToInt(economy.DayIncome * GameDifficulty.TaxRate) : 0;
            if (tax > 0) economy.Charge(tax, "Налог с выручки");

            int dirtPenalty = CleaningManager.Instance != null ? CleaningManager.Instance.ApplyEndOfDayPenalty() : 0;

            DayReport report = DayReport.Collect(Day, rent, wages);
            report.dirtPenalty = dirtPenalty;
            report.tax = tax;

            // Итог дня для цели игры: долг, банкротство, закрытие или победа.
            DayOutcome outcome = GoalManager.Instance != null ? GoalManager.Instance.EvaluateDay() : DayOutcome.Continue;
            report.debtDays = GoalManager.Instance != null ? GoalManager.Instance.DebtDays : 0;
            report.maxDebtDays = GoalManager.Instance != null ? GoalManager.Instance.MaxDebtDays : 0;

            // Если в момент окончания дня открыт терминал или идёт расстановка мебели,
            // отчёт не мог открыться и новый день не начинался. Закрываем всё заранее.
            if (Building.FurniturePlacer.Instance != null) Building.FurniturePlacer.Instance.CancelPlacement();
            ModalWindow.CloseCurrent();

            System.Action afterReport = () => AfterReport(outcome);
            if (reportUI != null)
                reportUI.ShowReport(report, afterReport, FindAnyObjectByType<PlayerController>());
            else
                afterReport();
        }

        /// <summary>После отчёта: окно победы/поражения или следующий день.</summary>
        private void AfterReport(DayOutcome outcome)
        {
            if (outcome == DayOutcome.Continue || GoalManager.Instance == null)
            {
                StartNextDay();
                return;
            }

            // Победа: «Продолжить» начинает следующий день. Поражение: окно предлагает начать заново.
            GoalManager.Instance.ShowOutcome(outcome, Day, StartNextDay);
        }

        private void StartNextDay()
        {
            Day++;
            Minutes = openHour * 60;
            Phase = DayPhase.Preparing;

            if (EconomyManager.Instance != null) EconomyManager.Instance.ResetDayStats();
            StoreStats.ResetDay();

            // Уютный зал (растения, кулер) понемногу поднимает репутацию.
            int decor = Building.FurnitureEffect.DailyReputationBonus;
            if (decor > 0) ReputationManager.Add(decor, "уютный салон");

            RaiseTime();
            int debt = GoalManager.Instance != null ? GoalManager.Instance.DebtDays : 0;
            if (debt > 0)
            {
                int left = GoalManager.Instance.MaxDebtDays - debt;
                GameEvents.Notify($"День {Day}. Салон в долгах! До банкротства: {left} {GoalManager.DaysWord(left)} — выйдите в плюс.");
            }
            else
                GameEvents.Notify($"День {Day}. Подготовьте салон и откройте его табличкой у входа.");

            // Событие нового дня (объявляется после приветствия, чтобы его не перекрыло).
            // Раньше событие выбиралось только при запуске сцены (день 1), а события доступны
            // с 3-го дня — поэтому «Чёрная пятница» и другие никогда не наступали.
            if (StoreEventManager.Instance != null) StoreEventManager.Instance.RollForDay(Day);

            if (Save.SaveSystem.Instance != null) Save.SaveSystem.Instance.Save();
        }

        /// <summary>Отладка: перемотать открытый день к самому закрытию.</summary>
        public void DebugSkipToClose()
        {
            if (Phase == DayPhase.Open) Minutes = closeHour * 60 - 1;
        }

        /// <summary>Установить номер дня (при загрузке сохранения). День начинается с подготовки.</summary>
        public void SetDay(int day)
        {
            Day = Mathf.Max(1, day);
            Minutes = openHour * 60;
            Phase = DayPhase.Preparing;
            RaiseTime();
        }

        private void RaiseTime()
        {
            lastMinute = (int)Minutes;
            GameEvents.RaiseDayTimeChanged(Day, lastMinute, Phase);
        }

        /// <summary>Время в формате ЧЧ:ММ.</summary>
        public static string FormatTime(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";
    }

    /// <summary>Итоги рабочего дня для отчёта.</summary>
    public struct DayReport
    {
        public int day;
        public int income;
        public int expenses;
        public int rent;
        public int wages;
        public int balance;
        public int reputation;
        public int customersServed;
        public int itemsSold;
        public int priceRefusals;
        public int outOfStock;
        public int leftQueue;
        public int cardPayments;
        public int cashPayments;
        public int changeErrors;
        public int changeLoss;
        public int consultBest;
        public int consultAcceptable;
        public int consultFailed;
        public int impulseBuys;
        public int theftItems;
        public int theftLoss;
        public int thievesCaught;
        public string eventName;
        public int dirtPenalty;
        public int tax;
        public int debtDays;
        public int maxDebtDays;

        public int Profit => income - expenses;

        public static DayReport Collect(int day, int rent, int wages)
        {
            EconomyManager e = EconomyManager.Instance;
            return new DayReport
            {
                day = day,
                income = e != null ? e.DayIncome : 0,
                expenses = e != null ? e.DayExpenses : 0,
                rent = rent,
                wages = wages,
                balance = e != null ? e.Balance : 0,
                reputation = ReputationManager.Instance != null ? ReputationManager.Instance.Value : 0,
                customersServed = StoreStats.CustomersServed,
                itemsSold = StoreStats.ItemsSold,
                priceRefusals = StoreStats.PriceRefusals,
                outOfStock = StoreStats.OutOfStockMisses,
                leftQueue = StoreStats.LeftQueue,
                cardPayments = StoreStats.CardPayments,
                cashPayments = StoreStats.CashPayments,
                changeErrors = StoreStats.ChangeErrors,
                changeLoss = StoreStats.ChangeLoss,
                consultBest = StoreStats.ConsultationsBest,
                consultAcceptable = StoreStats.ConsultationsAcceptable,
                consultFailed = StoreStats.ConsultationsFailed,
                impulseBuys = StoreStats.ImpulseBuys,
                theftItems = StoreStats.TheftItems,
                theftLoss = StoreStats.TheftLoss,
                thievesCaught = StoreStats.ThievesCaught,
                eventName = StoreEventManager.CurrentName
            };
        }
    }
}
