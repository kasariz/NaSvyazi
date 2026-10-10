using System.Collections;
using NaSvyazi.Customers;
using NaSvyazi.Store;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace NaSvyazi.Building
{
    /// <summary>
    /// Сотрудник, который ходит по залу (выкладчик, уборщик, охранник).
    /// Внешность берётся у моделей покупателей (тот же аниматор «ожидание ↔ ходьба»),
    /// над головой — розовый бейдж с должностью. Если моделей нет — фигура из простых форм.
    /// Работа описывается корутиной Work() в наследниках.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class StaffWorker : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>Дверь склада со стороны зала: отсюда сотрудники выходят и сюда уходят «на склад».</summary>
        public static readonly Vector3 StorageDoorHall = new Vector3(-7.35f, 0f, -6.6f);

        protected NavMeshAgent agent;
        private Animator animator;
        private Transform visual;
        private TextMeshPro badge;
        private float baseSpeed = 2.2f;

        protected abstract string Title { get; }
        /// <summary>Множитель скорости работы (навыки ветки «Персонал»).</summary>
        protected virtual float SpeedMultiplier => 1f;

        public static T Spawn<T>(string name) where T : StaffWorker
        {
            Vector3 pos = StorageDoorHall;
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 3f, NavMesh.AllAreas)) pos = hit.position;

            GameObject go = new GameObject(name);
            go.transform.position = pos;
            NavMeshAgent a = go.AddComponent<NavMeshAgent>();
            a.radius = 0.27f;
            a.height = 1.75f;
            a.acceleration = 12f;
            a.angularSpeed = 540f;
            a.stoppingDistance = 0.1f;
            a.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            a.avoidancePriority = 30;

            // Сотрудники не попадают в навигационную сетку при её перестройке.
            go.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild = true;

            T worker = go.AddComponent<T>();
            return worker;
        }

        protected virtual void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            BuildVisual();
        }

        protected virtual void Start() => StartCoroutine(Work());

        protected abstract IEnumerator Work();

        protected virtual void Update()
        {
            agent.speed = baseSpeed * SpeedMultiplier;
            if (animator != null) animator.SetFloat(SpeedId, agent.velocity.magnitude);

            // Бейдж смотрит на игрока.
            if (badge != null && Camera.main != null)
                badge.transform.rotation = Quaternion.LookRotation(badge.transform.position - Camera.main.transform.position);
        }

        // ----------------------------------------------------------------
        // Перемещение
        // ----------------------------------------------------------------

        /// <summary>Дойти до точки (с ограничением по времени, чтобы не застрять навсегда).</summary>
        protected IEnumerator WalkTo(Vector3 target, float stopDistance = 0.35f, float timeout = 25f)
        {
            if (!agent.isOnNavMesh) yield break;
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas)) target = hit.position;
            agent.isStopped = false;
            agent.SetDestination(target);

            float t = 0f;
            while (t < timeout)
            {
                t += Time.deltaTime;
                if (!agent.pathPending && agent.remainingDistance <= Mathf.Max(stopDistance, agent.stoppingDistance)) break;
                yield return null;
            }
            agent.ResetPath();
        }

        protected void FaceTowards(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d);
        }

        /// <summary>«Уйти на склад»: сотрудник заходит в дверь и пропадает на время пути туда и обратно.</summary>
        protected IEnumerator TripToStorage(float distance)
        {
            yield return WalkTo(StorageDoorHall, 0.4f);
            SetVisible(false);
            yield return new WaitForSeconds((distance * 2f / Mathf.Max(0.5f, agent.speed)) + 1.2f / SpeedMultiplier);
            SetVisible(true);
        }

        protected void SetVisible(bool visible)
        {
            if (visual != null) visual.gameObject.SetActive(visible);
            if (badge != null) badge.gameObject.SetActive(visible);
        }

        /// <summary>Ближе ли точка к торговому залу (иначе — склад или улица, туда ходят через дверь склада).</summary>
        protected static bool InSalon(Vector3 p) =>
            ExpansionManager.Instance == null || ExpansionManager.Instance.ContainsPoint(LayoutZone.Salon, p.x, p.z);

        // ----------------------------------------------------------------
        // Внешность
        // ----------------------------------------------------------------

        private void BuildVisual()
        {
            CustomerSpawner spawner = FindAnyObjectByType<CustomerSpawner>();
            Customer prefab = spawner != null ? spawner.RandomPrefab() : null;
            Animator source = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;

            if (source != null)
            {
                GameObject model = Instantiate(source.gameObject, transform);
                model.name = "Model";
                model.transform.localPosition = source.transform.localPosition;
                model.transform.localRotation = source.transform.localRotation;
                foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);
                animator = model.GetComponent<Animator>();
                visual = model.transform;
            }
            else
            {
                visual = BuildPrimitiveBody().transform;
            }

            // Бейдж с должностью над головой.
            GameObject tag = new GameObject("Badge");
            tag.transform.SetParent(transform, false);
            tag.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            badge = tag.AddComponent<TextMeshPro>();
            badge.text = Title;
            badge.fontSize = 2.2f;
            badge.alignment = TextAlignmentOptions.Center;
            badge.color = new Color(0.95f, 0.3f, 0.65f);
            badge.rectTransform.sizeDelta = new Vector2(2f, 0.4f);
            badge.outlineWidth = 0.2f;
            badge.outlineColor = new Color32(20, 20, 24, 255);
        }

        private GameObject BuildPrimitiveBody()
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(body.GetComponent<Collider>());
            body.name = "Body";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, new Color(0.86f, 0.18f, 0.55f));
            body.GetComponent<Renderer>().SetPropertyBlock(block);
            return body;
        }

        /// <summary>Прикрепить предмет к правой руке (по высоте пояса) — швабра, коробка.</summary>
        protected Transform AttachProp(GameObject prop, Vector3 localPosition, Vector3 localEuler)
        {
            prop.transform.SetParent(transform, false);
            prop.transform.localPosition = localPosition;
            prop.transform.localRotation = Quaternion.Euler(localEuler);
            foreach (Collider c in prop.GetComponentsInChildren<Collider>()) Destroy(c);
            return prop.transform;
        }

        protected static GameObject PrimitiveProp(PrimitiveType type, Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.transform.localScale = scale;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            go.GetComponent<Renderer>().SetPropertyBlock(block);
            return go;
        }
    }
}
