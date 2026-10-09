using NaSvyazi.Core;
using NaSvyazi.Customers;
using NaSvyazi.Economy;
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

        [Tooltip("Сколько реальных секунд длится одна игровая минута (0.75 → день ≈ 9 минут)")]
        [SerializeField] private float realSecondsPerGameMinute = 0.75f;

        [Tooltip("Аренда и коммунальные платежи за день, руб.")]
        [SerializeField] private int dailyRent = 800;

        [Tooltip("Если покупатели не ушли за это время после закрытия, день завершается принудительно")]
        [SerializeField] private float closingTimeout = 120f;

        [SerializeField] private DayReportUI reportUI;

        private float closingTimer;
        private int lastMinute = -1;

        public int Day { get; private set; } = 1;
        public float Minutes { get; private set; }
        public DayPhase Phase { get; private set; } = DayPhase.Preparing;
        public int DailyRent => dailyRent;

        private void Awake()
        {
            Instance = this;
            Minutes = openHour * 60;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            RaiseTime();
            GameEvents.Notify($"День {Day}. Подготовьте салон и откройте его табличкой у входа.");
        }

        private void Update()
        {
            if (Phase == DayPhase.Open)
            {
                Minutes += Time.deltaTime / realSecondsPerGameMinute;
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

            EconomyManager economy = EconomyManager.Instance;
            if (economy != null) economy.Charge(dailyRent, "Аренда и коммунальные платежи");

            DayReport report = DayReport.Collect(Day, dailyRent);

            if (reportUI != null)
                reportUI.ShowReport(report, StartNextDay, FindAnyObjectByType<PlayerController>());
            else
                StartNextDay();
        }

        private void StartNextDay()
        {
            Day++;
            Minutes = openHour * 60;
            Phase = DayPhase.Preparing;

            if (EconomyManager.Instance != null) EconomyManager.Instance.ResetDayStats();
            StoreStats.ResetDay();

            RaiseTime();
            GameEvents.Notify($"День {Day}. Подготовьте салон и откройте его табличкой у входа.");

            if (Save.SaveSystem.Instance != null) Save.SaveSystem.Instance.Save();
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

        public int Profit => income - expenses;

        public static DayReport Collect(int day, int rent)
        {
            EconomyManager e = EconomyManager.Instance;
            return new DayReport
            {
                day = day,
                income = e != null ? e.DayIncome : 0,
                expenses = e != null ? e.DayExpenses : 0,
                rent = rent,
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
                consultFailed = StoreStats.ConsultationsFailed
            };
        }
    }
}
