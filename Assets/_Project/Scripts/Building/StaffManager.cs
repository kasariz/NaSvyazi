using NaSvyazi.Audio;
using NaSvyazi.Core;
using NaSvyazi.Economy;
using NaSvyazi.Store;
using UnityEngine;

namespace NaSvyazi.Building
{
    /// <summary>
    /// Персонал салона. Пока доступен один сотрудник — кассир.
    /// Зарплата списывается в конце каждого рабочего дня.
    /// </summary>
    public class StaffManager : MonoBehaviour
    {
        public static StaffManager Instance { get; private set; }

        [SerializeField] private Cashier cashier;
        [SerializeField] private int cashierWage = 1500;
        [SerializeField] private int cashierMinLevel = 2;

        public bool CashierHired { get; private set; }
        public int CashierWage => cashierWage;
        public int CashierMinLevel => cashierMinLevel;
        public int DailyWages => CashierHired ? cashierWage : 0;

        private void Awake()
        {
            Instance = this;
            Apply();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool TryHireCashier()
        {
            if (CashierHired) return true;
            if (LevelManager.CurrentLevel < cashierMinLevel)
            {
                GameEvents.Notify($"Нужен уровень салона {cashierMinLevel}");
                SoundManager.Play(Sfx.Error);
                return false;
            }

            CashierHired = true;
            Apply();
            GameEvents.Notify($"Кассир нанят. Зарплата {EconomyManager.Format(cashierWage)} в день.");
            return true;
        }

        public void FireCashier()
        {
            if (!CashierHired) return;
            CashierHired = false;
            Apply();
            GameEvents.Notify("Кассир уволен");
        }

        /// <summary>Восстановить состояние (при загрузке).</summary>
        public void SetCashierHired(bool hired)
        {
            CashierHired = hired;
            Apply();
        }

        private void Apply()
        {
            if (cashier != null) cashier.gameObject.SetActive(CashierHired);
        }
    }
}
