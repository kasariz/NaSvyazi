using System.Collections.Generic;
using NaSvyazi.Core;
using NaSvyazi.Economy;
using NaSvyazi.Products;
using NaSvyazi.Store;
using UnityEngine;

namespace NaSvyazi.Customers
{
    /// <summary>
    /// Поток покупателей. Пока салон открыт (табличка «Открыто»), через случайные
    /// интервалы с улицы приходят новые покупатели со случайным списком покупок.
    /// </summary>
    public class CustomerSpawner : MonoBehaviour
    {
        [SerializeField] private Customer customerPrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private CheckoutQueue queue;
        [SerializeField] private ProductCatalog catalog;

        [Header("Поток покупателей")]
        [Tooltip("Интервал между покупателями, сек (мин, макс)")]
        [SerializeField] private Vector2 spawnInterval = new Vector2(8f, 16f);
        [SerializeField] private int maxCustomers = 6;

        [Tooltip("Сколько разных товаров хочет купить один покупатель (мин, макс)")]
        [SerializeField] private Vector2Int wantedItems = new Vector2Int(1, 3);

        private readonly List<Customer> active = new List<Customer>();
        private bool storeOpen;
        private float timer;

        public int ActiveCount => active.Count;

        private void OnEnable() => GameEvents.StoreOpenChanged += OnStoreOpenChanged;
        private void OnDisable() => GameEvents.StoreOpenChanged -= OnStoreOpenChanged;

        private void OnStoreOpenChanged(bool open)
        {
            storeOpen = open;
            if (open) timer = 2f; // первый покупатель приходит почти сразу
        }

        private void Update()
        {
            active.RemoveAll(c => c == null);
            if (!storeOpen || customerPrefab == null || spawnPoint == null) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;

            timer = Random.Range(spawnInterval.x, spawnInterval.y);
            if (active.Count < maxCustomers) Spawn();
        }

        private void Spawn()
        {
            List<ProductData> wants = PickShoppingList();
            if (wants.Count == 0) return;

            Customer c = Instantiate(customerPrefab, spawnPoint.position, spawnPoint.rotation);
            c.name = $"Customer_{active.Count + 1}";
            c.Init(wants, DemandModel.RandomTolerance(), queue, spawnPoint.position);
            active.Add(c);
        }

        /// <summary>Случайный список из нескольких разных товаров каталога.</summary>
        private List<ProductData> PickShoppingList()
        {
            List<ProductData> pool = new List<ProductData>();
            if (catalog != null)
                foreach (ProductData p in catalog.products)
                    if (p != null) pool.Add(p);

            List<ProductData> result = new List<ProductData>();
            int count = Random.Range(wantedItems.x, wantedItems.y + 1);

            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int index = Random.Range(0, pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return result;
        }
    }
}
