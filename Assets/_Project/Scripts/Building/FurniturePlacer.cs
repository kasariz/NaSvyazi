using System.Collections.Generic;
using NaSvyazi.Audio;
using NaSvyazi.Core;
using NaSvyazi.Economy;
using NaSvyazi.Store;
using NaSvyazi.UI;
using UnityEngine;

namespace NaSvyazi.Building
{
    /// <summary>
    /// Режим расстановки мебели. Предмет-«призрак» следует за прицелом по полу,
    /// R — поворот на 90°, ЛКМ — установить, ПКМ/Esc — отмена (за новую покупку деньги возвращаются).
    /// Подсветка: зелёная — место свободно, красная — мешает стена, прилавок, другой предмет или проход.
    /// </summary>
    public class FurniturePlacer : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public static FurniturePlacer Instance { get; private set; }

        /// <summary>Идёт расстановка.</summary>
        public static bool IsPlacing => Instance != null && Instance.ghost != null;

        /// <summary>Кадр отмены — чтобы тот же Esc не открыл меню паузы.</summary>
        public static int LastCancelFrame { get; private set; } = -1;

        [SerializeField] private float maxDistance = 7f;
        [SerializeField] private float gridStep = 0.25f;

        [Header("Разрешённая область (основной зал)")]
        [SerializeField] private Rect mainHall = new Rect(-9.8f, -9.8f, 19.6f, 19.6f);

        [Tooltip("Проход от двери, который нельзя загораживать")]
        [SerializeField] private Rect doorPassage = new Rect(-7.8f, -10f, 3.6f, 2.6f);

        private PlacedFurniture ghost;
        private bool isNew;
        private int refundPrice;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private float yaw;
        private bool valid;

        private readonly List<Collider> disabledColliders = new List<Collider>();
        private readonly List<ShelfSection> disabledSections = new List<ShelfSection>();
        private readonly List<FurnitureEffect> disabledEffects = new List<FurnitureEffect>();
        private readonly Dictionary<Renderer, MaterialPropertyBlock> savedBlocks = new Dictionary<Renderer, MaterialPropertyBlock>();

        /// <summary>Подсказка для интерфейса во время расстановки.</summary>
        public string Prompt => valid
            ? "[ЛКМ] Установить   [R] Повернуть   [ПКМ] Отмена"
            : "Здесь нельзя поставить   [R] Повернуть   [ПКМ] Отмена";

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ----------------------------------------------------------------
        // Начало расстановки
        // ----------------------------------------------------------------

        /// <summary>Новая покупка: создаём предмет и сразу переходим к расстановке.</summary>
        public void BeginPlaceNew(FurnitureData data)
        {
            if (IsPlacing || data == null || data.prefab == null) return;

            GameObject go = Instantiate(data.prefab);
            go.name = data.prefab.name;
            PlacedFurniture furniture = go.GetComponent<PlacedFurniture>();
            if (furniture == null)
            {
                Debug.LogError($"У префаба {data.prefab.name} нет компонента PlacedFurniture.");
                Destroy(go);
                return;
            }

            furniture.AssignNewInstanceId();
            isNew = true;
            refundPrice = data.price;
            yaw = SnapYaw(Camera.main != null ? Camera.main.transform.eulerAngles.y + 180f : 0f);
            StartGhost(furniture);
        }

        /// <summary>Перестановка уже стоящей мебели.</summary>
        public void BeginMove(PlacedFurniture furniture)
        {
            if (IsPlacing || furniture == null) return;

            isNew = false;
            refundPrice = 0;
            originalPosition = furniture.transform.position;
            originalRotation = furniture.transform.rotation;
            yaw = furniture.transform.eulerAngles.y;
            StartGhost(furniture);
        }

        private void StartGhost(PlacedFurniture furniture)
        {
            ghost = furniture;

            disabledColliders.Clear();
            foreach (Collider c in ghost.GetComponentsInChildren<Collider>())
                if (c.enabled)
                {
                    c.enabled = false;
                    disabledColliders.Add(c);
                }

            // Пока предмет «в руках», покупатели не должны идти к его полкам.
            disabledSections.Clear();
            foreach (ShelfSection s in ghost.GetComponentsInChildren<ShelfSection>())
                if (s.enabled)
                {
                    s.enabled = false;
                    disabledSections.Add(s);
                }

            // Эффект мебели (реклама, декор и т. п.) не действует, пока её переставляют.
            disabledEffects.Clear();
            foreach (FurnitureEffect e in ghost.GetComponentsInChildren<FurnitureEffect>())
                if (e.enabled)
                {
                    e.enabled = false;
                    disabledEffects.Add(e);
                }

            savedBlocks.Clear();
            foreach (Renderer r in ghost.GetComponentsInChildren<Renderer>())
            {
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                savedBlocks[r] = block;
            }
        }

        // ----------------------------------------------------------------
        // Каждый кадр
        // ----------------------------------------------------------------

        private void Update()
        {
            if (ghost == null || ModalWindow.IsAnyOpen) return;

            if (GameInput.RotatePressed) yaw = SnapYaw(yaw + 90f);

            Camera cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 target = Physics.Raycast(ray, out RaycastHit hit, maxDistance, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point
                : ray.origin + ray.direction * 3f;

            Vector3 position = new Vector3(Snap(target.x), 0f, Snap(target.z));
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            ghost.transform.SetPositionAndRotation(position, rotation);

            valid = IsPlacementValid(position, rotation);
            Tint(valid ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.35f, 0.35f));

            if (GameInput.PrimaryPressed && valid) Confirm();
            else if (GameInput.SecondaryPressed || GameInput.EscapePressed) Cancel();
        }

        private bool IsPlacementValid(Vector3 position, Quaternion rotation)
        {
            Vector3 size = ghost.Footprint;
            bool rotated = Mathf.Abs(Mathf.DeltaAngle(yaw, 90f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(yaw, 270f)) < 1f;
            float halfX = (rotated ? size.z : size.x) * 0.5f;
            float halfZ = (rotated ? size.x : size.z) * 0.5f;

            Rect footprint = new Rect(position.x - halfX, position.z - halfZ, halfX * 2f, halfZ * 2f);

            bool insideHall = Contains(mainHall, footprint);
            if (!insideHall && ExpansionManager.Instance != null && ExpansionManager.Instance.IsPurchased)
                insideHall = Contains(ExpansionManager.Instance.HallArea, footprint);
            if (!insideHall) return false;

            if (doorPassage.Overlaps(footprint)) return false;

            // Пересечение с другими объектами (пол не задевается — проверка начинается на 10 см выше).
            Vector3 center = position + Vector3.up * (size.y * 0.5f + 0.1f);
            Vector3 halfExtents = new Vector3(size.x * 0.48f, Mathf.Max(0.05f, size.y * 0.5f - 0.1f), size.z * 0.48f);
            Collider[] hits = Physics.OverlapBox(center, halfExtents, rotation, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider c in hits)
                if (!c.transform.IsChildOf(ghost.transform)) return false;

            return true;
        }

        private static bool Contains(Rect outer, Rect inner) =>
            inner.xMin >= outer.xMin && inner.xMax <= outer.xMax && inner.yMin >= outer.yMin && inner.yMax <= outer.yMax;

        // ----------------------------------------------------------------
        // Завершение
        // ----------------------------------------------------------------

        private void Confirm()
        {
            PlacedFurniture placed = ghost;
            EndGhost();

            SoundManager.Play(Sfx.Place);
            GameEvents.Notify(isNew ? "Мебель установлена" : "Мебель переставлена");
            GameEvents.RaiseLayoutChanged();
            Debug.Log($"[Мебель] {placed.name} → {placed.transform.position}");
        }

        private void Cancel()
        {
            LastCancelFrame = Time.frameCount;
            PlacedFurniture target = ghost;

            if (isNew)
            {
                EndGhost();
                Destroy(target.gameObject);
                if (EconomyManager.Instance != null) EconomyManager.Instance.Refund(refundPrice, "Отмена покупки мебели");
                GameEvents.Notify("Покупка отменена, деньги возвращены");
            }
            else
            {
                target.transform.SetPositionAndRotation(originalPosition, originalRotation);
                EndGhost();
            }
        }

        private void EndGhost()
        {
            foreach (KeyValuePair<Renderer, MaterialPropertyBlock> pair in savedBlocks)
                if (pair.Key != null) pair.Key.SetPropertyBlock(pair.Value);
            foreach (Collider c in disabledColliders)
                if (c != null) c.enabled = true;
            foreach (ShelfSection s in disabledSections)
                if (s != null) s.enabled = true;
            foreach (FurnitureEffect e in disabledEffects)
                if (e != null) e.enabled = true;
            disabledEffects.Clear();

            savedBlocks.Clear();
            disabledColliders.Clear();
            disabledSections.Clear();
            ghost = null;
        }

        private void Tint(Color color)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            foreach (Renderer r in savedBlocks.Keys)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                r.SetPropertyBlock(block);
            }
        }

        private float Snap(float value) => Mathf.Round(value / gridStep) * gridStep;

        private static float SnapYaw(float angle) => Mathf.Round(angle / 90f) * 90f % 360f;
    }
}
