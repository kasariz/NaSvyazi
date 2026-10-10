using System.Collections.Generic;
using NaSvyazi.Building;
using NaSvyazi.Core;
using NaSvyazi.Customers;
using NaSvyazi.Economy;
using UnityEngine;

namespace NaSvyazi.Store
{
    /// <summary>
    /// Чистота в зале. Пока салон открыт, покупатели понемногу пачкают пол (в дождь — вдвое чаще).
    /// Пятна моются шваброй со склада. Неубранные к концу дня пятна снижают репутацию.
    /// </summary>
    public class CleaningManager : MonoBehaviour
    {
        public static CleaningManager Instance { get; private set; }

        [Tooltip("Материал пятна (полупрозрачный)")]
        [SerializeField] private Material dirtMaterial;

        [Tooltip("Интервал между пятнами, сек (мин, макс)")]
        [SerializeField] private Vector2 interval = new Vector2(30f, 55f);
        [SerializeField] private int maxSpots = 8;

        [Tooltip("Сколько неубранных пятен стоят 1 очко репутации в конце дня")]
        [SerializeField] private int spotsPerReputation = 2;

        private readonly List<DirtSpot> spots = new List<DirtSpot>();
        private bool open;
        private float timer;
        private bool hinted;

        public int SpotCount
        {
            get
            {
                spots.RemoveAll(s => s == null);
                return spots.Count;
            }
        }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable() => GameEvents.StoreOpenChanged += OnStoreOpenChanged;
        private void OnDisable() => GameEvents.StoreOpenChanged -= OnStoreOpenChanged;

        private void OnStoreOpenChanged(bool isOpen)
        {
            open = isOpen;
            timer = Random.Range(interval.x, interval.y);
        }

        private void Update()
        {
            if (!open || dirtMaterial == null) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;

            float rain = Events.StoreEventManager.CurrentId == "rain" ? 0.5f : 1f;
            timer = Random.Range(interval.x, interval.y) * rain;
            if (SpotCount < maxSpots) SpawnUnderRandomCustomer();
        }

        private void SpawnUnderRandomCustomer()
        {
            Customer[] customers = FindObjectsByType<Customer>(FindObjectsSortMode.None);
            if (customers.Length == 0) return;

            Vector3 p = customers[Random.Range(0, customers.Length)].transform.position;
            ExpansionManager layout = ExpansionManager.Instance;
            if (layout != null && !layout.ContainsPoint(LayoutZone.Salon, p.x, p.z)) return; // только в зале

            Spawn(new Vector3(p.x + Random.Range(-0.3f, 0.3f), 0.004f, p.z + Random.Range(-0.3f, 0.3f)));

            if (!hinted)
            {
                hinted = true;
                GameEvents.Notify("На полу появилась грязь. Швабра — на складе у двери в зал.");
            }
        }

        private void Spawn(Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "DirtSpot";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, Random.Range(0f, 360f), 0f));
            float size = Random.Range(0.45f, 0.85f);
            go.transform.localScale = new Vector3(size, size * Random.Range(0.7f, 1f), 1f);

            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = dirtMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Тонкий триггер — по нему наводится прицел (покупатели и игрок проходят сквозь).
            BoxCollider trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1f, 1f, 0.05f);

            DirtSpot spot = go.AddComponent<DirtSpot>();
            spot.Init(renderer, dirtMaterial.color);
            spots.Add(spot);
        }

        public void OnCleaned(DirtSpot spot)
        {
            spots.Remove(spot);
            LevelManager.AddXp(1, "уборка");
            if (SpotCount == 0) GameEvents.Notify("Пол чистый!");
        }

        /// <summary>Штраф за грязь в конце дня (вызывает DayManager). Возвращает потерю репутации.</summary>
        public int ApplyEndOfDayPenalty()
        {
            int penalty = Mathf.Min(SpotCount / Mathf.Max(1, spotsPerReputation), 5);
            if (penalty > 0) ReputationManager.Add(-penalty, "грязный пол");
            return penalty;
        }
    }
}
