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

        [Tooltip("Минимальный уровень салона")]
        public int minLevel = 1;
    }

    /// <summary>
    /// Прогрессия: на старте салон торгует аксессуарами, кабелями и SIM-картами.
    /// Аудио и смартфоны открываются за деньги на определённом уровне салона.
    /// </summary>
    public class UnlockManager : MonoBehaviour
    {
        public static UnlockManager Instance { get; private set; }

        [SerializeField] private List<ProductCategory> startUnlocked = new List<ProductCategory>
        {
            ProductCategory.Accessories, ProductCategory.Cables, ProductCategory.SimCards
        };

        [SerializeField] private List<CategoryUnlock> categoryUnlocks = new List<CategoryUnlock>
        {
            new CategoryUnlock { category = ProductCategory.Audio, cost = 3000, minLevel = 2 },
            new CategoryUnlock { category = ProductCategory.Phones, cost = 10000, minLevel = 3 }
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
            categoryUnlocks.Find(u => u.category == category);

        public bool TryUnlock(ProductCategory category)
        {
            if (unlocked.Contains(category)) return true;

            CategoryUnlock rule = GetUnlock(category);
            if (rule == null) return false;

            if (LevelManager.CurrentLevel < rule.minLevel)
            {
                GameEvents.Notify($"Нужен уровень салона {rule.minLevel}");
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
