using NaSvyazi.Core;
using UnityEngine;

namespace NaSvyazi.Economy
{
    /// <summary>
    /// Деньги салона: баланс, списания (закупка товара) и поступления (продажи).
    /// Ведёт счётчики доходов и расходов за текущий день для итогового отчёта.
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        public static EconomyManager Instance { get; private set; }

        public int Balance { get; private set; }
        public int DayIncome { get; private set; }
        public int DayExpenses { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("На сцене больше одного EconomyManager — лишний отключён.");
                enabled = false;
                return;
            }

            Instance = this;
            // Стартовый капитал зависит от сложности (при загрузке сохранения баланс перезапишется).
            Balance = GameDifficulty.StartMoney;
        }

        private void Start() => GameEvents.RaiseMoneyChanged(Balance, 0);

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool CanAfford(int amount) => amount <= Balance;

        /// <summary>Списать деньги. Возвращает false, если средств недостаточно.</summary>
        public bool TrySpend(int amount, string reason)
        {
            if (amount < 0 || !CanAfford(amount)) return false;

            Balance -= amount;
            DayExpenses += amount;
            Debug.Log($"[Экономика] −{amount} руб. ({reason}). Баланс: {Balance}");
            GameEvents.RaiseMoneyChanged(Balance, -amount);
            return true;
        }

        /// <summary>Зачислить деньги (оплата покупателя).</summary>
        public void Earn(int amount, string reason)
        {
            if (amount <= 0) return;

            Balance += amount;
            DayIncome += amount;
            Debug.Log($"[Экономика] +{amount} руб. ({reason}). Баланс: {Balance}");
            GameEvents.RaiseMoneyChanged(Balance, amount);
        }

        /// <summary>
        /// Обязательный платёж (аренда): списывается всегда, баланс может уйти в минус.
        /// </summary>
        public void Charge(int amount, string reason)
        {
            if (amount <= 0) return;

            Balance -= amount;
            DayExpenses += amount;
            Debug.Log($"[Экономика] −{amount} руб. ({reason}). Баланс: {Balance}");
            GameEvents.RaiseMoneyChanged(Balance, -amount);
        }

        /// <summary>Вернуть деньги за отменённую покупку (уменьшает дневные расходы).</summary>
        public void Refund(int amount, string reason)
        {
            if (amount <= 0) return;

            Balance += amount;
            DayExpenses = Mathf.Max(0, DayExpenses - amount);
            Debug.Log($"[Экономика] Возврат {amount} руб. ({reason}). Баланс: {Balance}");
            GameEvents.RaiseMoneyChanged(Balance, amount);
        }

        /// <summary>Установить баланс (при загрузке сохранения).</summary>
        public void SetBalance(int balance)
        {
            Balance = balance;
            GameEvents.RaiseMoneyChanged(Balance, 0);
        }

        /// <summary>Обнулить дневные счётчики (в начале нового дня).</summary>
        public void ResetDayStats()
        {
            DayIncome = 0;
            DayExpenses = 0;
        }

        /// <summary>Формат суммы для интерфейса: «12 500 руб.»</summary>
        public static string Format(int amount) => $"{amount:N0} руб.".Replace(',', ' ').Replace(' ', ' ');
    }
}
