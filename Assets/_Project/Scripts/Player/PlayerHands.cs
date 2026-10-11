using NaSvyazi.Building;
using UnityEngine;

namespace NaSvyazi.Player
{
    /// <summary>
    /// Руки героя от первого лица. Видны, когда он что-то держит (коробку, швабру)
    /// или катит тележку: кисти — по бокам предмета или на ручке тележки,
    /// рукава тянутся к ним от плеч. Собираются из простых фигур при запуске.
    /// </summary>
    [DefaultExecutionOrder(100)] // после швабры: её поза уже посчитана в этом кадре
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
            public Transform root;
            public Transform sleeve;
            public Transform cuff;
            public Transform hand;    // ладонь с пальцами (собраны внутри)
            public bool isLeft;
        }

        private void Start()
        {
            carry = GetComponent<PlayerCarry>();
            Camera c = GetComponentInChildren<Camera>();
            if (c == null) c = Camera.main;
            cam = c != null ? c.transform : null;
            if (cam == null) return;
            c.nearClipPlane = Mathf.Min(c.nearClipPlane, 0.05f); // руки близко к камере — не обрезать

            left = BuildArm("Arm_L", true);
            right = BuildArm("Arm_R", false);
            SetVisible(false);
        }

        private Arm BuildArm(string name, bool isLeft)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(cam, false);
            Arm arm = new Arm
            {
                root = root,
                isLeft = isLeft,
                sleeve = Part(PrimitiveType.Capsule, root, sleeveColor),
                cuff = Part(PrimitiveType.Cylinder, root, cuffColor),
                hand = new GameObject("Hand").transform
            };
            arm.hand.SetParent(root, false);
            BuildHand(arm.hand, isLeft);
            return arm;
        }

        /// <summary>
        /// Кисть в локальных осях: Y — вдоль руки к пальцам, Z — от ладони наружу (тыльная сторона).
        /// Ладонь — скруглённый брусок, сжатые пальцы — валик, большой палец сбоку.
        /// </summary>
        private void BuildHand(Transform hand, bool isLeft)
        {
            const float side = 1f; // большой палец по +X кисти; сторону выбирает Pose (зеркалированием)

            Transform palm = Part(PrimitiveType.Cube, hand, skinColor);
            palm.localPosition = new Vector3(0f, 0.005f, 0f);
            palm.localScale = new Vector3(0.075f, 0.085f, 0.032f);

            // Сжатые пальцы: валик поперёк ладони, чуть загнут внутрь.
            Transform fingers = Part(PrimitiveType.Capsule, hand, skinColor);
            fingers.localPosition = new Vector3(0f, 0.055f, -0.012f);
            fingers.localRotation = Quaternion.Euler(0f, 0f, 90f);
            fingers.localScale = new Vector3(0.032f, 0.04f, 0.032f);

            Transform knuckles = Part(PrimitiveType.Capsule, hand, skinColor);
            knuckles.localPosition = new Vector3(0f, 0.045f, 0.008f);
            knuckles.localRotation = Quaternion.Euler(0f, 0f, 90f);
            knuckles.localScale = new Vector3(0.026f, 0.039f, 0.026f);

            Transform thumb = Part(PrimitiveType.Capsule, hand, skinColor);
            thumb.localPosition = new Vector3(0.04f * side, 0.02f, -0.012f);
            thumb.localRotation = Quaternion.Euler(0f, 0f, -35f * side);
            thumb.localScale = new Vector3(0.022f, 0.028f, 0.022f);
        }

        private static Transform Part(PrimitiveType type, Transform parent, Color color)
        {
            GameObject go = Core.RuntimeMaterials.CreatePrimitive(type);
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

            // Предмет со своими хватами (швабра — за черенок).
            if (carry.Held.TryGetGrips(out leftGrip, out rightGrip)) return true;

            // Бока предмета — по его габаритам в осях камеры. Раньше брались мировые габариты (AABB):
            // при повороте героя они «раздувались», и руки разъезжались от коробки.
            bool any = false;
            Bounds local = new Bounds();
            foreach (Renderer r in carry.Held.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = cam.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (!any) { local = new Bounds(p, Vector3.zero); any = true; }
                    else local.Encapsulate(p);
                }
            }
            if (!any) return false;

            // Хват — по бокам, чуть ниже середины и ближе к передней грани (как держат коробку снизу-сбоку).
            float halfWidth = Mathf.Max(0.06f, local.extents.x);
            Vector3 c = new Vector3(local.center.x, local.center.y - local.extents.y * 0.3f, local.center.z - local.extents.z * 0.15f);
            leftGrip = cam.TransformPoint(c - new Vector3(halfWidth, 0f, 0f));
            rightGrip = cam.TransformPoint(c + new Vector3(halfWidth, 0f, 0f));
            return true;
        }

        /// <summary>Рукав от плеча к кисти, манжета и кисть у цели.</summary>
        private Vector3 camRight => cam.right;
        private Vector3 camForward => cam.forward;

        private void Pose(Arm arm, Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float length = Mathf.Max(0.1f, dir.magnitude - 0.06f);
            Quaternion along = Quaternion.FromToRotation(Vector3.up, dir.normalized);

            arm.sleeve.SetPositionAndRotation(from + dir.normalized * (length * 0.5f), along);
            arm.sleeve.localScale = new Vector3(0.085f, length * 0.5f, 0.085f);

            arm.cuff.SetPositionAndRotation(from + dir.normalized * length, along);
            arm.cuff.localScale = new Vector3(0.09f, 0.015f, 0.09f);

            // Кисть смотрит вдоль руки, ладонью к предмету (внутрь, к центру экрана).
            Vector3 inward = arm.isLeft ? camRight : -camRight;
            Vector3 palmNormal = Vector3.ProjectOnPlane(inward, dir.normalized);
            if (palmNormal.sqrMagnitude < 0.0001f) palmNormal = Vector3.ProjectOnPlane(camForward, dir.normalized);
            arm.hand.SetPositionAndRotation(to - dir.normalized * 0.03f,
                Quaternion.LookRotation(-palmNormal.normalized, dir.normalized));

            // Большой палец — сверху, как при настоящем хвате: при необходимости зеркалим кисть.
            float thumbUp = Vector3.Dot(arm.hand.right, cam.up) >= 0f ? 1f : -1f;
            arm.hand.localScale = new Vector3(thumbUp, 1f, 1f);
        }

        private void SetVisible(bool visible)
        {
            foreach (Arm arm in new[] { left, right })
            {
                if (arm == null) continue;
                arm.root.gameObject.SetActive(visible);
            }
        }
    }
}
