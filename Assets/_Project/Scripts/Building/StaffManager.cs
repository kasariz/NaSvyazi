using System.Collections.Generic;
using NaSvyazi.Audio;
using NaSvyazi.Core;
using NaSvyazi.Economy;
using NaSvyazi.Progression;
using NaSvyazi.Store;
using UnityEngine;

namespace NaSvyazi.Building
{
    /// <summary>Должности персонала.</summary>
    public enum StaffRole
    {
        Cashier,       // касса: сканирует, принимает оплату, консультирует
        Merchandiser,  // выкладывает товар из коробок на полки
        Cleaner,       // моет пол
        Guard          // ловит воров у входа
    }

    /// <summary>
    /// Персонал салона. Кассир стоит за кассой (объект в сцене), остальные сотрудники
    /// появляются из двери склада и ходят по залу (StaffWorker). Зарплата списывается
    /// в конце каждого рабочего дня; навык «Мотивация персонала» её снижает.
    /// </summary>
    public class StaffManager : MonoBehaviour
    {
        public static StaffManager Instance { get; private set; }

        public class RoleInfo
        {
            public StaffRole role;
            public string name;
            public string description;
            public int wage;
            public int minLevel;
        }

        public static readonly RoleInfo[] Roles =
        {
            new RoleInfo { role = StaffRole.Cashier, name = "Кассир", wage = 1500, minLevel = 2,
                description = "Сканирует товар, принимает оплату и собирает тарифы (иногда ошибается)." },
            new RoleInfo { role = StaffRole.Merchandiser, name = "Выкладчик", wage = 1300, minLevel = 2,
                description = "Пополняет полусырые полки тем же товаром из коробок в зале и на складе." },
            new RoleInfo { role = StaffRole.Cleaner, name = "Уборщица", wage = 900, minLevel = 3,
                description = "Ходит со шваброй и отмывает грязь в зале." },
            new RoleInfo { role = StaffRole.Guard, name = "Охранник", wage = 1400, minLevel = 4,
                description = "Дежурит у входа и задерживает воров (60 %, с навыком — чаще)." },
        };

        [SerializeField] private Cashier cashier;
        [Tooltip("Устарело: зарплаты заданы в StaffManager.Roles")]
        [SerializeField] private int cashierWage = 1500;
        [SerializeField] private int cashierMinLevel = 2;

        private readonly HashSet<StaffRole> hired = new HashSet<StaffRole>();
        private readonly Dictionary<StaffRole, StaffWorker> workers = new Dictionary<StaffRole, StaffWorker>();

        public static RoleInfo Info(StaffRole role) => System.Array.Find(Roles, r => r.role == role);

        public bool IsHired(StaffRole role) => hired.Contains(role);

        /// <summary>Зарплата в день с учётом сложности и навыка.</summary>
        public int WageOf(StaffRole role) =>
            Mathf.RoundToInt(Info(role).wage * GameDifficulty.WageMultiplier * SkillManager.StaffWageMultiplier / 10f) * 10;

        public int DailyWages
        {
            get
            {
                int sum = 0;
                foreach (StaffRole r in hired) sum += WageOf(r);
                return sum;
            }
        }

        // Совместимость со старым кодом (касса, сохранения 0.2).
        public bool CashierHired => IsHired(StaffRole.Cashier);
        public int CashierWage => WageOf(StaffRole.Cashier);
        public int CashierMinLevel => Info(StaffRole.Cashier).minLevel;

        private void Awake()
        {
            Instance = this;
            Info(StaffRole.Cashier).wage = cashierWage;
            Info(StaffRole.Cashier).minLevel = cashierMinLevel;
            Apply();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool TryHire(StaffRole role)
        {
            if (IsHired(role)) return true;
            RoleInfo info = Info(role);
            if (LevelManager.CurrentLevel < info.minLevel)
            {
                GameEvents.Notify($"Нужен уровень салона {info.minLevel}");
                SoundManager.Play(Sfx.Error);
                return false;
            }

            hired.Add(role);
            Apply();
            SoundManager.Play(Sfx.Purchase, 0.6f);
            GameEvents.Notify($"{info.name}: нанят(а). Зарплата {EconomyManager.Format(WageOf(role))} в день.");
            return true;
        }

        public void Fire(StaffRole role)
        {
            if (!hired.Remove(role)) return;
            Apply();
            GameEvents.Notify($"{Info(role).name}: уволен(а)");
        }

        public bool TryHireCashier() => TryHire(StaffRole.Cashier);
        public void FireCashier() => Fire(StaffRole.Cashier);

        /// <summary>Восстановить состояние (при загрузке).</summary>
        public void SetHired(StaffRole role, bool value)
        {
            if (value) hired.Add(role);
            else hired.Remove(role);
            Apply();
        }

        public void SetCashierHired(bool value) => SetHired(StaffRole.Cashier, value);

        private void Apply()
        {
            if (cashier != null) cashier.gameObject.SetActive(IsHired(StaffRole.Cashier));
            Sync<Merchandiser>(StaffRole.Merchandiser);
            Sync<Cleaner>(StaffRole.Cleaner);
            Sync<Guard>(StaffRole.Guard);
        }

        /// <summary>Создать или убрать сотрудника, который ходит по залу.</summary>
        private void Sync<T>(StaffRole role) where T : StaffWorker
        {
            bool want = IsHired(role);
            workers.TryGetValue(role, out StaffWorker existing);
            if (want && existing == null && Application.isPlaying)
                workers[role] = StaffWorker.Spawn<T>($"Staff_{role}");
            else if (!want && existing != null)
            {
                Destroy(existing.gameObject);
                workers.Remove(role);
            }
        }
    }
}
