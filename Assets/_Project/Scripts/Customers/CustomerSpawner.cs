using System.Collections.Generic;
using NaSvyazi.Audio;
using NaSvyazi.Core;
using NaSvyazi.Economy;
using NaSvyazi.Events;
using NaSvyazi.Progression;
using NaSvyazi.Products;
using NaSvyazi.Store;
using NaSvyazi.Tariffs;
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

        [Tooltip("Варианты внешности покупателей (если пусто — используется Customer Prefab)")]
        [SerializeField] private List<Customer> customerPrefabs = new List<Customer>();
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private CheckoutQueue queue;
        [SerializeField] private ProductCatalog catalog;

        [Header("Поток покупателей")]
        [Tooltip("Интервал между покупателями, сек (мин, макс) — подобран под день ≈ 5 минут")]
        [SerializeField] private Vector2 customerInterval = new Vector2(5f, 10f);
        [SerializeField] private int maxCustomers = 6;

        [Tooltip("Сколько разных товаров хочет купить один покупатель (мин, макс)")]
        [SerializeField] private Vector2Int wantedItems = new Vector2Int(1, 3);

        [Tooltip("Доля посетителей, пришедших за консультацией по тарифу")]
        [Range(0f, 1f)] [SerializeField] private float consultationChance = 0.25f;

        [Tooltip("Доля покупателей-воришек (камера видеонаблюдения вдвое снижает её)")]
        [Range(0f, 1f)] [SerializeField] private float thiefChance = 0.07f;

        private readonly List<Customer> active = new List<Customer>();
        private bool storeOpen;
        private float timer;

        public int ActiveCount => active.Count;

        /// <summary>Случайная модель покупателя (её внешность используют и сотрудники).</summary>
        public Customer RandomPrefab() =>
            customerPrefabs.Count > 0 ? customerPrefabs[Random.Range(0, customerPrefabs.Count)] : customerPrefab;

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
            if (!storeOpen || spawnPoint == null || (customerPrefab == null && customerPrefabs.Count == 0)) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;

            // Чем выше репутация, тем чаще приходят покупатели.
            // Рекламные стенды тоже привлекают покупателей.
            timer = Random.Range(customerInterval.x, customerInterval.y) * ReputationManager.SpawnIntervalMultiplier
                    * Building.FurnitureEffect.SpawnIntervalMultiplier
                    * SkillManager.SpawnIntervalMultiplier      // навык «Сарафанное радио»
                    * StoreEventManager.SpawnMultiplier         // событие дня (например, «Чёрная пятница»)
                    * GameDifficulty.CustomerIntervalMultiplier;
            if (active.Count < maxCustomers) Spawn();
        }

        private void Spawn()
        {
            bool wantsConsultation = Random.value < consultationChance * StoreEventManager.ConsultationMultiplier;

            List<ProductData> wants = wantsConsultation ? null : PickShoppingList();
            if (!wantsConsultation && wants.Count == 0) return;

            Customer prefab = customerPrefabs.Count > 0
                ? customerPrefabs[Random.Range(0, customerPrefabs.Count)]
                : customerPrefab;
            if (prefab == null) return;

            Customer c = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
            c.name = $"Customer_{active.Count + 1}";

            if (wantsConsultation)
                c.InitConsultation(TariffBuilder.RandomNeed(), queue, spawnPoint.position);
            else
            {
                c.Init(wants, DemandModel.RandomTolerance(), queue, spawnPoint.position);

                // Воришки появляются со второго дня; камера видеонаблюдения их отпугивает.
                float chance = thiefChance;
                if (Building.FurnitureEffect.Count(Building.FurnitureEffectType.SecurityCamera) > 0) chance *= 0.5f;
                if (DayManager.Instance != null && DayManager.Instance.Day >= 2 && Random.value < chance)
                    c.MakeThief();
            }

            active.Add(c);
            SoundManager.Play(Sfx.DoorBell, 0.5f);
        }

        /// <summary>Случайный список из нескольких разных товаров каталога.</summary>
        private List<ProductData> PickShoppingList()
        {
            List<ProductData> pool = new List<ProductData>();
            if (catalog != null)
                foreach (ProductData p in catalog.products)
                    if (p != null && UnlockManager.IsUnlocked(p.category)) pool.Add(p);

            List<ProductData> result = new List<ProductData>();
            // Программа лояльности и «Чёрная пятница» — покупатель хочет больше товаров.
            int count = Random.Range(wantedItems.x, wantedItems.y + 1)
                        + SkillManager.ExtraWantedItems + StoreEventManager.ExtraWantedItems;

            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int index = PickIndex(pool);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return result;
        }

        /// <summary>
        /// Случайный индекс товара. В день выхода нового смартфона смартфоны и аудио
        /// выбираются втрое чаще остальных товаров.
        /// </summary>
        private static int PickIndex(List<ProductData> pool)
        {
            if (!StoreEventManager.FavorPhones) return Random.Range(0, pool.Count);

            float total = 0f;
            foreach (ProductData p in pool) total += p.category.RequiresShowcase() ? 3f : 1f;

            float roll = Random.value * total;
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= pool[i].category.RequiresShowcase() ? 3f : 1f;
                if (roll <= 0f) return i;
            }
            return pool.Count - 1;
        }
    }
}
