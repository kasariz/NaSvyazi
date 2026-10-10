using NaSvyazi.Audio;
using NaSvyazi.Core;
using NaSvyazi.Economy;
using NaSvyazi.Store;
using UnityEngine;

namespace NaSvyazi.Building
{
    /// <summary>
    /// Улучшения из вкладки «Развитие», которые не относятся к помещениям:
    /// вместимость стеллажей (ярусы товара) и тележка для коробок.
    /// </summary>
    public class UpgradeManager : MonoBehaviour
    {
        public static UpgradeManager Instance { get; private set; }

        /// <summary>Этап улучшения стеллажей: цена и уровень салона.</summary>
        public struct ShelfUpgrade
        {
            public string name;
            public string description;
            public int price;
            public int minLevel;
        }

        public static readonly ShelfUpgrade[] ShelfUpgrades =
        {
            new ShelfUpgrade
            {
                name = "Полки повышенной вместимости",
                description = "Мелкий товар ставится в два яруса — на полку помещается больше.",
                price = 6000, minLevel = 2
            },
            new ShelfUpgrade
            {
                name = "Полки повышенной вместимости II",
                description = "Три яруса для мелкого товара: SIM-карты, кабели, стёкла.",
                price = 14000, minLevel = 4
            },
        };

        [Header("Тележка")]
        [Tooltip("Тележка в сцене (выключена до покупки)")]
        [SerializeField] private HandCart cart;
        [SerializeField] private int cartPrice = 2500;
        [SerializeField] private int cartMinLevel = 1;
        [SerializeField] private Vector3 cartSpawn = new Vector3(-9f, 0f, -12.6f);

        public int ShelfTier { get; private set; }
        public bool HasCart { get; private set; }
        public int CartPrice => cartPrice;
        public int CartMinLevel => cartMinLevel;
        public HandCart Cart => cart;

        private void Awake()
        {
            Instance = this;
            ShelfSection.SetExtraTiers(0);
            if (cart != null) cart.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Следующее улучшение стеллажей или null.</summary>
        public ShelfUpgrade? NextShelfUpgrade =>
            ShelfTier < ShelfUpgrades.Length ? ShelfUpgrades[ShelfTier] : (ShelfUpgrade?)null;

        public bool TryUpgradeShelves()
        {
            ShelfUpgrade? next = NextShelfUpgrade;
            if (next == null || !Pay(next.Value.price, next.Value.minLevel, next.Value.name)) return false;

            ShelfTier++;
            ShelfSection.SetExtraTiers(ShelfTier);
            GameEvents.Notify($"{next.Value.name}: готово! Теперь на полки помещается больше товара.");
            return true;
        }

        public bool TryBuyCart()
        {
            if (HasCart || cart == null || !Pay(cartPrice, cartMinLevel, "Тележка для коробок")) return false;

            HasCart = true;
            cart.transform.SetPositionAndRotation(cartSpawn, Quaternion.identity);
            cart.gameObject.SetActive(true);
            GameEvents.Notify("Тележка ждёт у ворот склада. E — катить, ЛКМ с коробкой — поставить на неё.");
            return true;
        }

        private static bool Pay(int price, int minLevel, string what)
        {
            if (LevelManager.CurrentLevel < minLevel)
            {
                GameEvents.Notify($"Нужен уровень салона {minLevel}");
                SoundManager.Play(Sfx.Error);
                return false;
            }
            EconomyManager economy = EconomyManager.Instance;
            if (economy == null || !economy.TrySpend(price, what))
            {
                GameEvents.Notify("Недостаточно денег");
                SoundManager.Play(Sfx.Error);
                return false;
            }
            SoundManager.Play(Sfx.Purchase);
            return true;
        }

        // --- Сохранение ---

        public void SetState(int shelfTier, bool hasCart, Vector3 cartPosition, Quaternion cartRotation)
        {
            ShelfTier = Mathf.Clamp(shelfTier, 0, ShelfUpgrades.Length);
            ShelfSection.SetExtraTiers(ShelfTier);

            HasCart = hasCart && cart != null;
            if (cart != null)
            {
                cart.gameObject.SetActive(HasCart);
                if (HasCart) cart.transform.SetPositionAndRotation(cartPosition, cartRotation);
            }
        }
    }
}
