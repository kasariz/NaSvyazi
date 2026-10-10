using NaSvyazi.Interaction;
using UnityEngine;

namespace NaSvyazi.Store
{
    /// <summary>
    /// Швабра: берётся в руки как коробка (E), бросается на G.
    /// Со шваброй в руках грязные пятна на полу отмываются ЛКМ (удерживать).
    ///
    /// В руках швабра не «висит» перед камерой, как коробка, а держится как настоящая:
    /// верх черенка — в правой руке у пояса, левая рука ниже по черенку, насадка стоит на полу
    /// перед героем. Во время мытья насадка ходит из стороны в сторону над пятном.
    /// </summary>
    [DefaultExecutionOrder(50)] // поза швабры считается до рук героя (PlayerHands — 100)
    public class Mop : Carryable
    {
        [Header("Геометрия (локальные координаты модели)")]
        [Tooltip("Нижняя точка насадки")]
        [SerializeField] private float headBottomY = -0.62f;
        [Tooltip("Точка черенка под правой рукой")]
        [SerializeField] private float gripY = 0.6f;
        [Tooltip("Расстояние по черенку между руками")]
        [SerializeField] private float handSpacing = 0.42f;

        [Header("Поза в руках (относительно камеры)")]
        [SerializeField] private Vector3 rightHand = new Vector3(0.24f, -0.62f, 0.3f);

        [Header("Мытьё")]
        [SerializeField] private float scrubAmplitude = 0.2f;
        [SerializeField] private float scrubSpeed = 14f;

        private Camera cam;
        private float scrubTimer;
        private float scrubPhase;
        private Vector3 scrubTarget;
        private Vector3 headPoint;  // сглаженное положение насадки на полу

        private float StickLength => gripY - headBottomY;

        /// <summary>Сделать «мазок» шваброй по пятну (вызывает DirtSpot при каждом повторе ЛКМ).</summary>
        public void Scrub(Vector3 spot)
        {
            scrubTarget = spot;
            scrubTimer = 0.35f;
        }

        private void LateUpdate()
        {
            if (!IsHeld) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            Transform c = cam.transform;
            Vector3 grip = c.TransformPoint(rightHand);

            Vector3 forward = Vector3.ProjectOnPlane(c.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(c.up, Vector3.up);
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            float floorY = FloorBelow(grip + forward * 0.6f, c.position.y - 1.6f);
            float height = Mathf.Max(0.05f, grip.y - floorY);
            float reach = Mathf.Sqrt(Mathf.Max(0.04f, StickLength * StickLength - height * height));

            // Насадка на полу перед героем, чуть левее правой руки.
            Vector3 target = new Vector3(grip.x, floorY, grip.z) + forward * reach - right * 0.12f;

            scrubTimer -= Time.deltaTime;
            if (scrubTimer > 0f)
            {
                // Тянемся к пятну (в пределах досягаемости) и водим насадкой из стороны в сторону.
                Vector3 toSpot = new Vector3(scrubTarget.x, floorY, scrubTarget.z) - target;
                target += Vector3.ClampMagnitude(toSpot, 0.35f);
                scrubPhase += Time.deltaTime * scrubSpeed;
                target += right * (Mathf.Sin(scrubPhase) * scrubAmplitude) + forward * (Mathf.Cos(scrubPhase * 2f) * 0.05f);
            }

            headPoint = headPoint == Vector3.zero ? target : Vector3.Lerp(headPoint, target, 1f - Mathf.Exp(-20f * Time.deltaTime));

            // Черенок — от насадки к руке; ширина насадки поперёк взгляда.
            Vector3 up = (grip - headPoint).normalized;
            Vector3 face = Vector3.ProjectOnPlane(forward, up);
            if (face.sqrMagnitude < 0.001f) face = forward;
            transform.SetPositionAndRotation(headPoint - up * headBottomY, Quaternion.LookRotation(face.normalized, up));
        }

        /// <summary>Хваты для рук героя: правая — у верха черенка, левая — ниже.</summary>
        public override bool TryGetGrips(out Vector3 leftGrip, out Vector3 rightGrip)
        {
            rightGrip = transform.TransformPoint(new Vector3(0f, gripY, 0f));
            leftGrip = transform.TransformPoint(new Vector3(0f, gripY - handSpacing, 0f));
            return IsHeld;
        }

        private static float FloorBelow(Vector3 from, float fallback)
        {
            if (Physics.Raycast(from + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 3f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return fallback;
        }

        protected override void OnDroppedFromHands() => headPoint = Vector3.zero;
    }
}
