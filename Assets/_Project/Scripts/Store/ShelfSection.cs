using System.Collections.Generic;
using NaSvyazi.Audio;
using NaSvyazi.Core;
using NaSvyazi.Economy;
using NaSvyazi.Interaction;
using NaSvyazi.Player;
using NaSvyazi.Products;
using NaSvyazi.UI;
using TMPro;
using UnityEngine;

namespace NaSvyazi.Store
{
    /// <summary>
    /// Секция стеллажа — место под один вид товара.
    /// Сетка мест рассчитывается автоматически по габаритам товара:
    /// мелкие SIM-пакеты встанут плотнее, крупные зарядки — реже.
    /// Синяя ось Z (вперёд) секции должна смотреть на покупателя.
    /// E — изменить цену товара, ЛКМ — выложить из коробки, ПКМ — убрать в коробку.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ShelfSection : MonoBehaviour, IActionTarget, IInteractable
    {
        /// <summary>Все секции на сцене — понадобится покупателям для поиска товара.</summary>
        public static readonly List<ShelfSection> All = new List<ShelfSection>();

        [Header("Размер секции (м)")]
        [SerializeField] private float width = 0.85f;
        [SerializeField] private float depth = 0.4f;
        [SerializeField] private float height = 0.38f;

        [Tooltip("Зазор между единицами товара")]
        [SerializeField] private float gap = 0.01f;

        [Tooltip("Ограничение вместимости секции")]
        [SerializeField] private int maxItems = 40;

        [Tooltip("Ценник/надпись на краю полки (необязательно)")]
        [SerializeField] private TMP_Text label;

        private readonly List<Vector3> slots = new List<Vector3>();
        private readonly List<GameObject> items = new List<GameObject>();
        private ProductData product;

        public ProductData Product => product;
        public int Count => items.Count;
        public int Capacity => slots.Count;
        public bool IsEmpty => items.Count == 0;
        public bool IsFull => !IsEmpty && items.Count >= slots.Count;

        private void Awake()
        {
            ConfigureCollider();
            UpdateLabel();
        }

        private void OnEnable()
        {
            All.Add(this);
            GameEvents.PriceChanged += OnPriceChanged;
        }

        private void OnDisable()
        {
            All.Remove(this);
            GameEvents.PriceChanged -= OnPriceChanged;
        }

        private void OnPriceChanged(ProductData changed, int price)
        {
            if (changed == product) UpdateLabel();
        }

        /// <summary>Точка на полу перед секцией, куда подходит покупатель.</summary>
        public Vector3 CustomerStandPoint
        {
            get
            {
                Vector3 p = transform.TransformPoint(new Vector3(0f, 0f, depth * 0.5f + 0.55f));
                p.y = 0f;
                return p;
            }
        }

        // Вызывается в редакторе при добавлении компонента.
        private void Reset() => ConfigureCollider();

        /// <summary>Подходит ли товар для выкладки в эту секцию.</summary>
        public bool CanPlace(ProductData item)
        {
            if (item == null || item.size.y > height) return false;
            if (IsEmpty) return true;
            return item == product && !IsFull;
        }

        /// <summary>Выложить 1 шт. товара (из коробки игрока или при начальном заполнении).</summary>
        public bool TryPlace(ProductData item)
        {
            if (!CanPlace(item)) return false;

            if (IsEmpty)
            {
                product = item;
                BuildSlots(item);
            }

            GameObject visual = item.CreateVisual(transform);
            visual.transform.localPosition = slots[items.Count];
            visual.transform.localRotation = Quaternion.identity;
            items.Add(visual);

            UpdateLabel();
            return true;
        }

        /// <summary>Забрать 1 шт. товара (игроком обратно в коробку или покупателем).</summary>
        public bool TryTakeOne(out ProductData taken)
        {
            if (IsEmpty)
            {
                taken = null;
                return false;
            }

            taken = product;
            int last = items.Count - 1;
            Destroy(items[last]);
            items.RemoveAt(last);

            if (IsEmpty) product = null;

            UpdateLabel();
            return true;
        }

        /// <summary>Убрать весь товар с полки.</summary>
        public void Clear()
        {
            while (TryTakeOne(out _)) { }
        }

        /// <summary>Заполнить полку заданным количеством товара (при загрузке сохранения).</summary>
        public void Restore(ProductData item, int count)
        {
            Clear();
            for (int i = 0; i < count; i++)
                if (!TryPlace(item)) break;
        }

        // --- IInteractable: E — изменить цену ---

        public string GetPrompt(PlayerInteractor interactor) =>
            $"Цена: {EconomyManager.Format(PriceManager.PriceOf(product))} — изменить";

        public bool CanInteract(PlayerInteractor interactor) =>
            !IsEmpty && PriceUI.Instance != null && !ModalWindow.IsAnyOpen;

        public void Interact(PlayerInteractor interactor) =>
            PriceUI.Instance.OpenFor(product, interactor.Controller);

        // --- IActionTarget: ЛКМ — выложить, ПКМ — убрать в коробку ---

        public string GetPrimaryPrompt(PlayerInteractor interactor)
        {
            ProductBox box = interactor.Carry.Held as ProductBox;
            if (box == null || box.IsEmpty || !CanPlace(box.Product)) return null;
            return $"Выложить: {box.Product.displayName}";
        }

        public void DoPrimary(PlayerInteractor interactor)
        {
            ProductBox box = interactor.Carry.Held as ProductBox;
            if (box == null || box.IsEmpty || !CanPlace(box.Product)) return;

            if (box.TryTakeOne(out ProductData item) && TryPlace(item))
                SoundManager.Play(Sfx.Place, 0.6f);
        }

        public string GetSecondaryPrompt(PlayerInteractor interactor)
        {
            ProductBox box = interactor.Carry.Held as ProductBox;
            if (box == null || IsEmpty || !box.CanAccept(product)) return null;
            return $"Убрать в коробку: {product.displayName}";
        }

        public void DoSecondary(PlayerInteractor interactor)
        {
            ProductBox box = interactor.Carry.Held as ProductBox;
            if (box == null || IsEmpty || !box.CanAccept(product)) return;

            if (TryTakeOne(out ProductData item))
            {
                box.TryPutOne(item);
                SoundManager.Play(Sfx.Place, 0.4f);
            }
        }

        // --- Внутреннее ---

        /// <summary>Расчёт мест: ряды слева направо, начиная с переднего края полки.</summary>
        private void BuildSlots(ProductData item)
        {
            slots.Clear();

            float cellX = item.size.x + gap;
            float cellZ = item.size.z + gap;
            int columns = Mathf.Max(1, Mathf.FloorToInt(width / cellX));
            int rows = Mathf.Max(1, Mathf.FloorToInt(depth / cellZ));

            float startX = -columns * cellX * 0.5f + cellX * 0.5f;
            float frontZ = depth * 0.5f - cellZ * 0.5f;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    if (slots.Count >= maxItems) return;
                    slots.Add(new Vector3(startX + c * cellX, 0f, frontZ - r * cellZ));
                }
            }
        }

        private void ConfigureCollider()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return;
            box.isTrigger = true;
            box.size = new Vector3(width, height, depth);
            box.center = new Vector3(0f, height * 0.5f, 0f);
        }

        private void UpdateLabel()
        {
            if (label == null) return;
            label.text = IsEmpty
                ? "Пусто"
                : $"{product.displayName} · {EconomyManager.Format(PriceManager.PriceOf(product))} · {Count}/{Capacity}";
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.8f);
            Gizmos.DrawWireCube(new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth));
            // Короткая линия показывает, где передний край секции.
            Gizmos.DrawLine(new Vector3(0f, 0f, depth * 0.5f), new Vector3(0f, 0f, depth * 0.5f + 0.15f));
        }
    }
}
