using System.Collections.Generic;
using NaSvyazi.Audio;
using NaSvyazi.Interaction;
using NaSvyazi.Player;
using NaSvyazi.Products;
using UnityEngine;

namespace NaSvyazi.Building
{
    /// <summary>
    /// Стеллаж склада: хранит коробки с товаром, чтобы они не валялись на полу.
    /// С коробкой в руках: ЛКМ по стеллажу — поставить на свободное место.
    /// Чтобы забрать коробку, наведитесь на неё и нажмите E (как обычно).
    /// </summary>
    public class StorageRack : MonoBehaviour, IActionTarget
    {
        public static readonly List<StorageRack> All = new List<StorageRack>();

        [Tooltip("Места под коробки (локальные координаты центров)")]
        [SerializeField] private List<Vector3> slots = new List<Vector3>();

        [Tooltip("Куда ставится коробка — для подсказки («на склад», «на тележку»)")]
        [SerializeField] private string placeName = "на склад";

        private readonly List<Transform> anchors = new List<Transform>();

        private void Awake()
        {
            // Для каждого места — пустой объект-якорь, к которому крепится коробка.
            for (int i = 0; i < slots.Count; i++)
            {
                GameObject anchor = new GameObject($"Slot_{i + 1}");
                anchor.transform.SetParent(transform, false);
                anchor.transform.localPosition = slots[i];
                anchors.Add(anchor.transform);
            }
        }

        /// <summary>Коробки, заранее расставленные на стеллаже в сцене, закрепляются на своих местах.</summary>
        private void Start()
        {
            foreach (ProductBox box in FindObjectsByType<ProductBox>(FindObjectsSortMode.None))
                if (!box.IsHeld && box.GetComponentInParent<StorageRack>() == null)
                    TryAttachNear(box);
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public int FreeSlots
        {
            get
            {
                int free = 0;
                foreach (Transform a in anchors)
                    if (a.childCount == 0) free++;
                return free;
            }
        }

        /// <summary>Свободное место на стеллаже (для выкладчика) или null.</summary>
        public Transform FreeAnchor => FindFreeAnchor();

        private Transform FindFreeAnchor()
        {
            foreach (Transform a in anchors)
                if (a.childCount == 0) return a;
            return null;
        }

        /// <summary>Поставить коробку на свободное место. false — мест нет.</summary>
        public bool TryStore(Carryable box)
        {
            Transform anchor = FindFreeAnchor();
            if (anchor == null || box == null) return false;
            box.OnReleasedTo(anchor, Vector3.zero, Quaternion.identity);
            return true;
        }

        /// <summary>
        /// При загрузке: если коробка оказалась рядом с местом на складе — закрепить её там.
        /// </summary>
        public static bool TryAttachNear(Carryable box, float maxDistance = 0.35f)
        {
            foreach (StorageRack rack in All)
                foreach (Transform a in rack.anchors)
                {
                    if (a.childCount > 0) continue;
                    if (Vector3.Distance(a.position, box.transform.position) <= maxDistance)
                    {
                        box.PrepareForStorage();
                        box.OnReleasedTo(a, Vector3.zero, Quaternion.identity);
                        return true;
                    }
                }
            return false;
        }

        // --- IActionTarget ---

        public string GetPrimaryPrompt(PlayerInteractor interactor)
        {
            if (!(interactor.Carry.Held is ProductBox)) return null;
            return FreeSlots > 0 ? $"Поставить коробку {placeName} (мест: {FreeSlots})" : "Свободных мест нет";
        }

        public void DoPrimary(PlayerInteractor interactor)
        {
            if (!(interactor.Carry.Held is ProductBox) || FreeSlots == 0) return;

            Carryable box = interactor.Carry.Release();
            if (TryStore(box)) SoundManager.Play(Sfx.Pickup, 0.6f);
        }

        public string GetSecondaryPrompt(PlayerInteractor interactor) => null;
        public void DoSecondary(PlayerInteractor interactor) { }
    }
}
