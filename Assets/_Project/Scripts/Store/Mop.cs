using NaSvyazi.Interaction;
using UnityEngine;

namespace NaSvyazi.Store
{
    /// <summary>
    /// Швабра: берётся в руки как коробка (E), бросается на G.
    /// Со шваброй в руках грязные пятна на полу отмываются ЛКМ (удерживать).
    ///
    /// Поза в руках: правая рука — внизу справа в кадре (видна при любом наклоне головы),
    /// насадка стоит на полу там, куда смотрит игрок (не ближе minReach и не дальше maxReach).
    /// Черенок идёт от насадки к руке и проскальзывает в ладонях, поэтому швабра никогда
    /// не задирается вверх. Во время мытья насадка ходит из стороны в сторону над пятном.
    /// </summary>
    [DefaultExecutionOrder(50)] // поза швабры считается до рук героя (PlayerHands — 100)
    public class Mop : Carryable
    {
        [Header("Геометрия (локальные координаты модели)")]
        [Tooltip("Нижняя точка насадки")]
        [SerializeField] private float headBottomY = -0.62f;
        [Tooltip("Верхний конец черенка")]
        [SerializeField] private float stickTopY = 0.7f;
        [Tooltip("Расстояние по черенку между руками")]
        [SerializeField] private float handSpacing = 0.38f;

        [Header("Поза в руках")]
        [Tooltip("Правая рука относительно камеры: вправо, вниз, вперёд — в нижней части кадра")]
        [SerializeField] private Vector3 handInView = new Vector3(0.2f, -0.26f, 0.5f);
        [Tooltip("Насадка не ближе и не дальше этого расстояния от героя (по полу), м")]
        [SerializeField] private float minReach = 0.55f;
        [SerializeField] private float maxReach = 1.3f;

        [Header("Мытьё")]
        [SerializeField] private float scrubAmplitude = 0.2f;
        [SerializeField] private float scrubSpeed = 14f;

        private Camera cam;
        private float scrubTimer;
        private float scrubPhase;
        private Vector3 scrubTarget;
        private Vector3 headPoint;      // сглаженное положение насадки на полу
        private bool hasPose;
        private Vector3 rightGripPoint, leftGripPoint;

        private float StickLength => stickTopY - headBottomY;

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
            Vector3 grip = c.TransformPoint(handInView);

            // Направление «вперёд по полу». Если смотрим строго вниз, вперёд — это верх кадра.
            Vector3 forward = Vector3.ProjectOnPlane(c.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(c.up, Vector3.up);
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 eye = c.position;

            // Где взгляд встречает пол — туда и ставим насадку (в пределах досягаемости).
            float floorY = FloorBelow(eye + forward * 0.9f, eye.y - 1.6f);
            float reach = maxReach;
            if (c.forward.y < -0.05f)
            {
                float t = (floorY - eye.y) / c.forward.y;
                Vector3 hit = eye + c.forward * t;
                reach = new Vector2(hit.x - eye.x, hit.z - eye.z).magnitude;
            }
            reach = Mathf.Clamp(reach, minReach, maxReach);

            Vector3 target = new Vector3(eye.x, floorY, eye.z) + forward * reach + right * 0.05f;
            target.y = FloorBelow(target, floorY);

            scrubTimer -= Time.deltaTime;
            if (scrubTimer > 0f)
            {
                // Тянемся к пятну и водим насадкой из стороны в сторону.
                Vector3 toSpot = new Vector3(scrubTarget.x, target.y, scrubTarget.z) - target;
                target += Vector3.ClampMagnitude(toSpot, 0.4f);
                scrubPhase += Time.deltaTime * scrubSpeed;
                target += right * (Mathf.Sin(scrubPhase) * scrubAmplitude) + forward * (Mathf.Cos(scrubPhase * 2f) * 0.05f);
            }

            // Черенок не длиннее, чем есть: если рука дальше — насадка чуть приподнимается.
            Vector3 fromHand = target - grip;
            if (fromHand.magnitude > StickLength - 0.05f) target = grip + fromHand.normalized * (StickLength - 0.05f);

            headPoint = hasPose ? Vector3.Lerp(headPoint, target, 1f - Mathf.Exp(-18f * Time.deltaTime)) : target;
            hasPose = true;

            Vector3 up = grip - headPoint;
            float gripDistance = up.magnitude;
            up /= Mathf.Max(0.001f, gripDistance);

            // Насадка поперёк взгляда; черенок — от насадки к руке.
            Vector3 face = Vector3.ProjectOnPlane(forward, up);
            if (face.sqrMagnitude < 0.001f) face = Vector3.ProjectOnPlane(-c.up, up);
            transform.SetPositionAndRotation(headPoint - up * headBottomY, Quaternion.LookRotation(face.normalized, up));

            // Хваты: правая рука — там, где черенок проходит через точку руки, левая — ниже по черенку.
            rightGripPoint = headPoint + up * gripDistance;
            leftGripPoint = headPoint + up * Mathf.Max(0.15f, gripDistance - handSpacing);
        }

        /// <summary>Хваты для рук героя (считаются в LateUpdate вместе с позой швабры).</summary>
        public override bool TryGetGrips(out Vector3 leftGrip, out Vector3 rightGrip)
        {
            leftGrip = leftGripPoint;
            rightGrip = rightGripPoint;
            return IsHeld && hasPose;
        }

        private static float FloorBelow(Vector3 from, float fallback)
        {
            if (Physics.Raycast(from + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 3f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return fallback;
        }

        protected override void OnDroppedFromHands() => hasPose = false;
    }
}
