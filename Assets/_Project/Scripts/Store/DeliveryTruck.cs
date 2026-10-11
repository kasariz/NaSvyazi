using System.Collections;
using NaSvyazi.Audio;
using UnityEngine;

namespace NaSvyazi.Store
{
    /// <summary>
    /// Грузовик доставки: приезжает по дороге к воротам склада, сигналит, стоит, пока выгружают коробки,
    /// и уезжает. Создаётся из кода службой доставки — в сцене ничего настраивать не нужно.
    ///
    /// Внешний вид: если в Resources/Models есть модель «delivery_truck» (FBX/OBJ, например фургон
    /// из Kenney Car Kit), используется она — масштабируется до длины lengthMeters и разворачивается
    /// вдоль дороги. Иначе фургон собирается из простых фигур в цветах сети «На связи».
    /// </summary>
    public class DeliveryTruck : MonoBehaviour
    {
        public const string ModelResource = "Models/delivery_truck";

        private const float LengthMeters = 5f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Transform body;   // модель (покачивается на ходу)
        private Transform[] wheels;

        /// <summary>Создать грузовик в точке start, «лицом» по направлению движения.</summary>
        public static DeliveryTruck Create(Vector3 start, Vector3 direction)
        {
            GameObject root = new GameObject("DeliveryTruck");
            root.transform.SetPositionAndRotation(start, Quaternion.LookRotation(direction, Vector3.up));
            DeliveryTruck truck = root.AddComponent<DeliveryTruck>();
            truck.Build();
            return truck;
        }

        /// <summary>Доехать до точки за время duration (с разгоном и торможением).</summary>
        public IEnumerator DriveTo(Vector3 target, float duration, bool accelerate, bool brake)
        {
            Vector3 from = transform.position;
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(0.1f, duration));
                float k = accelerate && brake ? Mathf.SmoothStep(0f, 1f, t)
                    : brake ? 1f - (1f - t) * (1f - t)
                    : accelerate ? t * t : t;
                Vector3 previous = transform.position;
                transform.position = Vector3.Lerp(from, target, k);
                Animate(Vector3.Distance(previous, transform.position));
                yield return null;
            }
        }

        public void Honk() => SoundManager.Play(Sfx.Horn, 0.6f);

        /// <summary>Лёгкое покачивание кузова и вращение колёс на ходу.</summary>
        private void Animate(float moved)
        {
            if (wheels != null)
                foreach (Transform w in wheels)
                    if (w != null) w.Rotate(Vector3.right, moved / 0.35f * Mathf.Rad2Deg, Space.Self);
            if (body != null)
                body.localPosition = new Vector3(0f, moved > 0.001f ? Mathf.Sin(Time.time * 18f) * 0.01f : 0f, 0f);
        }

        // ----------------------------------------------------------------
        // Внешний вид
        // ----------------------------------------------------------------

        private void Build()
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);

            GameObject model = Resources.Load<GameObject>(ModelResource);
            if (model != null) BuildFromModel(model);
            else BuildPrimitive();

            foreach (Collider c in GetComponentsInChildren<Collider>()) Destroy(c);
        }

        private void BuildFromModel(GameObject prefab)
        {
            GameObject m = Instantiate(prefab, body);
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.identity;

            // Длинная сторона модели — вдоль движения (+Z). У моделей Kenney перёд и так смотрит в +Z.
            Bounds b = LocalBounds(m);
            if (b.size.x > b.size.z) m.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            b = LocalBounds(m);

            float scale = LengthMeters / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
            m.transform.localScale = Vector3.one * scale;
            b = LocalBounds(m);
            m.transform.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z);

            // Колёса модели не известны — крутить нечего; покачивание кузова остаётся.
            wheels = new Transform[0];
        }

        private Bounds LocalBounds(GameObject go)
        {
            bool any = false;
            Bounds result = new Bounds();
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = body.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                    else result.Encapsulate(p);
                }
            }
            return result;
        }

        /// <summary>Фургон из простых фигур: кабина впереди (+Z), белый кузов с розовой полосой сети.</summary>
        private void BuildPrimitive()
        {
            Color white = new Color(0.93f, 0.93f, 0.94f);
            Color pink = new Color(0.86f, 0.18f, 0.55f);
            Color dark = new Color(0.08f, 0.08f, 0.1f);
            Color glass = new Color(0.15f, 0.2f, 0.26f);
            Color light = new Color(1f, 0.95f, 0.75f);

            // Рама, кузов и кабина (длина 5 м: кузов 3,3 м, кабина 1,5 м).
            Part(PrimitiveType.Cube, new Vector3(0f, 0.42f, 0f), new Vector3(1.9f, 0.18f, 4.9f), dark);
            Part(PrimitiveType.Cube, new Vector3(0f, 1.55f, -0.75f), new Vector3(2.0f, 2.1f, 3.3f), white);
            Part(PrimitiveType.Cube, new Vector3(0f, 1.6f, -0.75f), new Vector3(2.02f, 0.35f, 3.32f), pink);
            Part(PrimitiveType.Cube, new Vector3(0f, 1.25f, 1.65f), new Vector3(1.95f, 1.5f, 1.45f), white);
            Part(PrimitiveType.Cube, new Vector3(0f, 0.85f, 2.3f), new Vector3(1.9f, 0.6f, 0.25f), white);

            // Стёкла кабины и фары.
            Part(PrimitiveType.Cube, new Vector3(0f, 1.6f, 2.37f), new Vector3(1.75f, 0.6f, 0.04f), glass);
            Part(PrimitiveType.Cube, new Vector3(0.98f, 1.6f, 1.75f), new Vector3(0.04f, 0.55f, 0.9f), glass);
            Part(PrimitiveType.Cube, new Vector3(-0.98f, 1.6f, 1.75f), new Vector3(0.04f, 0.55f, 0.9f), glass);
            Part(PrimitiveType.Cube, new Vector3(0.65f, 0.9f, 2.44f), new Vector3(0.35f, 0.18f, 0.04f), light);
            Part(PrimitiveType.Cube, new Vector3(-0.65f, 0.9f, 2.44f), new Vector3(0.35f, 0.18f, 0.04f), light);
            Part(PrimitiveType.Cube, new Vector3(0f, 0.5f, 2.47f), new Vector3(1.95f, 0.2f, 0.1f), dark);

            // Задние двери: тонкий шов посередине.
            Part(PrimitiveType.Cube, new Vector3(0f, 1.55f, -2.41f), new Vector3(0.03f, 2.0f, 0.02f), dark);

            // Колёса.
            wheels = new Transform[4];
            int n = 0;
            foreach (float z in new[] { 1.55f, -1.6f })
                foreach (float x in new[] { 0.92f, -0.92f })
                {
                    Transform w = Part(PrimitiveType.Cylinder, new Vector3(x, 0.35f, z), new Vector3(0.7f, 0.12f, 0.7f), dark);
                    w.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    // Вращать колесо удобнее вокруг его собственной оси — добавим промежуточный узел.
                    Transform hub = new GameObject("Wheel").transform;
                    hub.SetParent(body, false);
                    hub.localPosition = w.localPosition;
                    w.SetParent(hub, true);
                    wheels[n++] = hub;
                }
        }

        private Transform Part(PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            GameObject go = Core.RuntimeMaterials.CreatePrimitive(type);
            go.transform.SetParent(body, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            r.SetPropertyBlock(block);
            return go.transform;
        }
    }
}
