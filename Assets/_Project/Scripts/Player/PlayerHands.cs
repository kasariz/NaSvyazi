using NaSvyazi.Building;
using UnityEngine;

namespace NaSvyazi.Player
{
    /// <summary>
    /// Руки героя от первого лица. Видны, когда он что-то держит (коробку, швабру)
    /// или катит тележку: кисти — по бокам предмета или на ручке тележки,
    /// рукава тянутся к ним от плеч. Собираются из простых фигур при запуске.
    /// </summary>
    public class PlayerHands : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Color sleeveColor = new Color(0.12f, 0.12f, 0.14f);
        [SerializeField] private Color cuffColor = new Color(0.86f, 0.18f, 0.55f);
        [SerializeField] private Color skinColor = new Color(0.93f, 0.76f, 0.64f);

        [Tooltip("Плечи относительно камеры: в стороны, вниз, вперёд")]
        [SerializeField] private Vector3 shoulder = new Vector3(0.24f, -0.42f, 0.02f);

        private PlayerCarry carry;
        private Transform cam;
        private Arm left, right;

        private class Arm
        {
            public Transform sleeve;
            public Transform cuff;
            public Transform hand;
        }

        private void Start()
        {
            carry = GetComponent<PlayerCarry>();
            Camera c = GetComponentInChildren<Camera>();
            if (c == null) c = Camera.main;
            cam = c != null ? c.transform : null;
            if (cam == null) return;
            c.nearClipPlane = Mathf.Min(c.nearClipPlane, 0.05f); // руки близко к камере — не обрезать

            left = BuildArm("Arm_L");
            right = BuildArm("Arm_R");
            SetVisible(false);
        }

        private Arm BuildArm(string name)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(cam, false);
            return new Arm
            {
                sleeve = Part(PrimitiveType.Capsule, root, sleeveColor),
                cuff = Part(PrimitiveType.Cylinder, root, cuffColor),
                hand = Part(PrimitiveType.Capsule, root, skinColor)
            };
        }

        private static Transform Part(PrimitiveType type, Transform parent, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            Renderer r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            r.SetPropertyBlock(block);
            return go.transform;
        }

        private void LateUpdate()
        {
            if (cam == null || left == null) return;

            Vector3 leftTarget, rightTarget;
            if (!TryGetGrips(out leftTarget, out rightTarget))
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            Pose(left, cam.TransformPoint(new Vector3(-shoulder.x, shoulder.y, shoulder.z)), leftTarget);
            Pose(right, cam.TransformPoint(shoulder), rightTarget);
        }

        /// <summary>Куда тянутся руки: ручка тележки или бока предмета в руках.</summary>
        private bool TryGetGrips(out Vector3 leftGrip, out Vector3 rightGrip)
        {
            leftGrip = rightGrip = Vector3.zero;

            if (HandCart.Pushed != null)
            {
                leftGrip = HandCart.Pushed.LeftGripWorld;
                rightGrip = HandCart.Pushed.RightGripWorld;
                return true;
            }

            if (carry == null || carry.Held == null) return false;

            // Бока предмета по его габаритам (в плоскости экрана).
            Bounds bounds = new Bounds(carry.Held.transform.position, Vector3.zero);
            foreach (Renderer r in carry.Held.GetComponentsInChildren<Renderer>())
                if (r.enabled) bounds.Encapsulate(r.bounds);

            Vector3 side = cam.right * Mathf.Max(0.06f, Vector3.Dot(bounds.extents, Abs(cam.right)));
            Vector3 center = bounds.center - cam.up * 0.02f;
            leftGrip = center - side;
            rightGrip = center + side;
            return true;
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>Рукав от плеча к кисти, манжета и кисть у цели.</summary>
        private static void Pose(Arm arm, Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float length = Mathf.Max(0.1f, dir.magnitude - 0.06f);
            Quaternion along = Quaternion.FromToRotation(Vector3.up, dir.normalized);

            arm.sleeve.SetPositionAndRotation(from + dir.normalized * (length * 0.5f), along);
            arm.sleeve.localScale = new Vector3(0.085f, length * 0.5f, 0.085f);

            arm.cuff.SetPositionAndRotation(from + dir.normalized * length, along);
            arm.cuff.localScale = new Vector3(0.09f, 0.015f, 0.09f);

            arm.hand.SetPositionAndRotation(to, along);
            arm.hand.localScale = new Vector3(0.07f, 0.06f, 0.05f);
        }

        private void SetVisible(bool visible)
        {
            foreach (Arm arm in new[] { left, right })
            {
                if (arm == null) continue;
                arm.sleeve.gameObject.SetActive(visible);
                arm.cuff.gameObject.SetActive(visible);
                arm.hand.gameObject.SetActive(visible);
            }
        }
    }
}
