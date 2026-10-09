using System.Collections.Generic;
using NaSvyazi.Core;
using NaSvyazi.Products;
using UnityEngine;

namespace NaSvyazi.Economy
{
    /// <summary>
    /// Цены продажи в салоне. Изначально каждая цена равна рыночной,
    /// игрок может изменить её у полки (клавиша E).
    /// </summary>
    public class PriceManager : MonoBehaviour
    {
        public static PriceManager Instance { get; private set; }

        [SerializeField] private int minPrice = 1;
        [SerializeField] private int maxPrice = 999999;

        private readonly Dictionary<ProductData, int> prices = new Dictionary<ProductData, int>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public int GetPrice(ProductData product)
        {
            if (product == null) return 0;
            return prices.TryGetValue(product, out int price) ? price : product.marketPrice;
        }

        public void SetPrice(ProductData product, int price)
        {
            if (product == null) return;
            price = Mathf.Clamp(price, minPrice, maxPrice);
            prices[product] = price;
            GameEvents.RaisePriceChanged(product, price);
        }

        /// <summary>Все цены, изменённые игроком (для сохранения).</summary>
        public IEnumerable<KeyValuePair<ProductData, int>> CustomPrices => prices;

        /// <summary>Цена товара, даже если менеджера цен нет на сцене.</summary>
        public static int PriceOf(ProductData product) =>
            Instance != null ? Instance.GetPrice(product) : (product != null ? product.marketPrice : 0);
    }
}
