using System;
using System.Collections.Generic;
using NaSvyazi.Core;
using NaSvyazi.Products;
using UnityEngine;

namespace NaSvyazi.Economy
{
    /// <summary>Условия открытия категории товаров.</summary>
    [Serializable]
    public class CategoryUnlock
    {
        public ProductCategory category;

        [Tooltip("Стоимость открытия (договор с поставщиком), руб.")]
        public int cost;

        [Tooltip("Минимальная репутация салона")]
        public int minReputation;
    }

    /// <summary>
    /// Прогрессия: на старте салон торгует аксессуарами, кабелями и SIM-картами.
    /// Наушники и смартфоны открываются за деньги при достаточной репутации.
    /// </summary>
    public class UnlockManager : MonoBehaviour
    {
        public static UnlockManager Instance { get; private set; }

        [SerializeField] private List<ProductCategory> startUnlocked = new List<ProductCategory>
        {
            ProductCategory.Accessories, ProductCategory.Cables, ProductCategory.SimCards
        };

        [SerializeField] private List<CategoryUnlock> unlocks = new List<CategoryUnlock>
        {
            new CategoryUnlock { category = ProductCategory.Audio, cost = 3000, minReputation = 40 },
            new CategoryUnlock { category = ProductCategory.Phones, cost = 10000, minReputation = 55 }
        };

        private readonly HashSet<ProductCategory> unlocked = new HashSet<ProductCategory>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }
            Instance = this;
            foreach (ProductCategory c in startUnlocked) unlocked.Add(c);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Открыта ли категория (если менеджера нет — всё открыто).</summary>
        public static bool IsUnlocked(ProductCategory category) =>
            Instance == null || Instance.unlocked.Contains(category);

        public CategoryUnlock GetUnlock(ProductCategory category) =>
            unlocks.Find(u => u.category == category);

        public bool TryUnlock(ProductCategory category)
        {
            if (unlocked.Contains(category)) return true;

            CategoryUnlock rule = GetUnlock(category);
            if (rule == null) return false;

            int reputation = ReputationManager.Instance != null ? ReputationManager.Instance.Value : 100;
            if (reputation < rule.minReputation)
            {
                GameEvents.Notify($"Нужна репутация не ниже {rule.minReputation}");
                Audio.SoundManager.Play(Audio.Sfx.Error);
                return false;
            }

            EconomyManager economy = EconomyManager.Instance;
            if (economy == null || !economy.TrySpend(rule.cost, $"Открытие категории «{category.ToDisplayName()}»"))
            {
                GameEvents.Notify("Недостаточно денег");
                Audio.SoundManager.Play(Audio.Sfx.Error);
                return false;
            }

            unlocked.Add(category);
            GameEvents.RaiseCategoryUnlocked(category);
            GameEvents.Notify($"Открыта категория «{category.ToDisplayName()}»");
            return true;
        }

        /// <summary>Список открытых категорий (для сохранения).</summary>
        public List<ProductCategory> GetUnlocked() => new List<ProductCategory>(unlocked);

        /// <summary>Восстановить открытые категории (при загрузке).</summary>
        public void SetUnlocked(IEnumerable<ProductCategory> categories)
        {
            unlocked.Clear();
            foreach (ProductCategory c in startUnlocked) unlocked.Add(c);
            foreach (ProductCategory c in categories) unlocked.Add(c);
        }
    }
}
