using NaSvyazi.Audio;
using NaSvyazi.Core;
using NaSvyazi.Economy;
using UnityEngine;

namespace NaSvyazi.Building
{
    /// <summary>
    /// Расширение салона: второй торговый зал за восточной стеной.
    /// После покупки открывается проём в стене, зал становится доступен
    /// для мебели и покупателей, но растёт ежедневная аренда.
    /// </summary>
    public class ExpansionManager : MonoBehaviour
    {
        public static ExpansionManager Instance { get; private set; }

        [SerializeField] private GameObject hallRoot;
        [SerializeField] private GameObject gateWall;

        [SerializeField] private int price = 25000;
        [SerializeField] private int minLevel = 3;
        [SerializeField] private int extraRent = 1000;

        [Tooltip("Область второго зала для расстановки мебели (X, Z, ширина, глубина)")]
        [SerializeField] private Rect hallArea = new Rect(10.2f, -9.8f, 7.6f, 19.6f);

        public bool IsPurchased { get; private set; }
        public int Price => price;
        public int MinLevel => minLevel;
        public int ExtraRent => IsPurchased ? extraRent : 0;
        public int ExtraRentIfPurchased => extraRent;
        public Rect HallArea => hallArea;

        private void Awake()
        {
            Instance = this;
            Apply();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool TryPurchase()
        {
            if (IsPurchased) return true;

            if (LevelManager.CurrentLevel < minLevel)
            {
                GameEvents.Notify($"Нужен уровень салона {minLevel}");
                SoundManager.Play(Sfx.Error);
                return false;
            }

            EconomyManager economy = EconomyManager.Instance;
            if (economy == null || !economy.TrySpend(price, "Расширение зала"))
            {
                GameEvents.Notify("Недостаточно денег");
                SoundManager.Play(Sfx.Error);
                return false;
            }

            IsPurchased = true;
            Apply();
            SoundManager.Play(Sfx.Purchase);
            GameEvents.Notify("Второй зал открыт! Расставьте в нём стеллажи.");
            GameEvents.RaiseLayoutChanged();
            return true;
        }

        /// <summary>Восстановить состояние (при загрузке).</summary>
        public void SetPurchased(bool purchased)
        {
            IsPurchased = purchased;
            Apply();
            GameEvents.RaiseLayoutChanged();
        }

        private void Apply()
        {
            if (hallRoot != null) hallRoot.SetActive(IsPurchased);
            if (gateWall != null) gateWall.SetActive(!IsPurchased);
        }
    }
}
