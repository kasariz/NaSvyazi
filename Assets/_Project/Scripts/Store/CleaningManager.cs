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

        /// <summary>Ближайшее неотмытое пятно (для уборщика) или null.</summary>
        public DirtSpot Nearest(Vector3 from)
        {
            DirtSpot best = null;
            float bestD = float.MaxValue;
            foreach (DirtSpot s in spots)
            {
                if (s == null || s.IsCleaned) continue;
                float d = (s.transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
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

        /// <summary>Отладка: пятно грязи в точке.</summary>
        public void DebugSpawn(Vector3 position)
        {
            if (dirtMaterial != null) Spawn(new Vector3(position.x, 0.004f, position.z));
        }

        private void Spawn(Vector3 position)
        {
            GameObject go = Core.RuntimeMaterials.CreatePrimitive(PrimitiveType.Quad);
            go.name = "DirtSpot";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, Random.Range(0f, 360f), 0f));
            float size = Random.Range(0.6f, 1.0f); // текстура с прозрачными краями — квад чуть больше
            go.transform.localScale = new Vector3(size, size * Random.Range(0.8f, 1f), 1f);

            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = SplatMaterial(Random.Range(0, SplatVariants));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Тонкий триггер — по нему наводится прицел (покупатели и игрок проходят сквозь).
            BoxCollider trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1f, 1f, 0.05f);

            DirtSpot spot = go.AddComponent<DirtSpot>();
            spot.Init(renderer, dirtMaterial.color);
            spots.Add(spot);
        }

        // ----------------------------------------------------------------
        // Внешний вид пятна
        // ----------------------------------------------------------------

        private const int SplatVariants = 4;
        private const int SplatSize = 128;
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private Material[] splatMaterials;

        /// <summary>
        /// Материал пятна с текстурой «лужи грязи»: неровный край из нескольких клякс, брызги вокруг,
        /// следы подошвы и разводы внутри. Текстуры рисуются кодом при первом пятне (4 варианта),
        /// поэтому пятна больше не выглядят квадратами.
        /// </summary>
        private Material SplatMaterial(int variant)
        {
            if (splatMaterials == null) splatMaterials = new Material[SplatVariants];
            if (splatMaterials[variant] != null) return splatMaterials[variant];

            Material m = new Material(dirtMaterial) { name = $"Dirt_Splat_{variant}" };
            Texture2D tex = GenerateSplat(1000 + variant * 37);
            if (m.HasProperty(BaseMapId)) m.SetTexture(BaseMapId, tex);
            if (m.HasProperty(MainTexId)) m.SetTexture(MainTexId, tex);
            splatMaterials[variant] = m;
            return m;
        }

        private static Texture2D GenerateSplat(int seed)
        {
            System.Random rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();

            // Кляксы: основная в центре и несколько поменьше вокруг.
            int blobs = 5 + rnd.Next(4);
            Vector3[] b = new Vector3[blobs]; // x, y — центр (0…1), z — радиус
            b[0] = new Vector3(0.5f, 0.5f, 0.22f + R() * 0.05f);
            for (int i = 1; i < blobs; i++)
            {
                float a = R() * Mathf.PI * 2f, d = 0.08f + R() * 0.17f;
                b[i] = new Vector3(0.5f + Mathf.Cos(a) * d, 0.5f + Mathf.Sin(a) * d, 0.07f + R() * 0.1f);
            }

            // Брызги — маленькие капли подальше от центра.
            int drops = 10 + rnd.Next(8);
            Vector3[] dr = new Vector3[drops];
            for (int i = 0; i < drops; i++)
            {
                float a = R() * Mathf.PI * 2f, d = 0.3f + R() * 0.16f;
                dr[i] = new Vector3(0.5f + Mathf.Cos(a) * d, 0.5f + Mathf.Sin(a) * d, 0.012f + R() * 0.025f);
            }

            float ox = R() * 100f, oy = R() * 100f;
            float footAngle = R() * Mathf.PI;
            Vector2 footDir = new Vector2(Mathf.Cos(footAngle), Mathf.Sin(footAngle));

            Texture2D tex = new Texture2D(SplatSize, SplatSize, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                name = "DirtSplat"
            };
            Color32[] px = new Color32[SplatSize * SplatSize];

            for (int y = 0; y < SplatSize; y++)
            for (int x = 0; x < SplatSize; x++)
            {
                float u = (x + 0.5f) / SplatSize, v = (y + 0.5f) / SplatSize;

                // Поле клякс (metaballs) с неровным краем от шума.
                float field = 0f;
                foreach (Vector3 c in b)
                {
                    float dx = u - c.x, dy = v - c.y;
                    field += c.z * c.z / (dx * dx + dy * dy + 0.0004f);
                }
                float edgeNoise = Mathf.PerlinNoise(ox + u * 9f, oy + v * 9f) * 0.9f + 0.55f;
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((field * edgeNoise - 1f) * 1.6f));

                foreach (Vector3 c in dr)
                {
                    float d = Vector2.Distance(new Vector2(u, v), new Vector2(c.x, c.y));
                    alpha = Mathf.Max(alpha, Mathf.Clamp01((c.z - d) / (c.z * 0.4f)));
                }

                // Внутри — разводы (темнее/светлее) и пара отпечатков подошвы поперёк лужи.
                float streak = Mathf.PerlinNoise(ox + u * 22f, oy + v * 22f);
                float shade = 0.75f + streak * 0.45f;
                Vector2 rel = new Vector2(u - 0.5f, v - 0.5f);
                float along = Vector2.Dot(rel, footDir), across = Vector2.Dot(rel, new Vector2(-footDir.y, footDir.x));
                float tread = Mathf.Abs(across) < 0.07f && Mathf.Abs(along) < 0.16f
                    && Mathf.Repeat(along * 40f, 1f) < 0.45f ? 0.75f : 1f;
                shade *= tread;

                alpha *= 0.65f + 0.35f * streak;
                byte g = (byte)(Mathf.Clamp01(shade) * 255f);
                px[y * SplatSize + x] = new Color32(g, g, g, (byte)(Mathf.Clamp01(alpha) * 255f));
            }

            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
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
