using System;
using System.Collections;
using System.Collections.Generic;
using NaSvyazi.Core;
using NaSvyazi.Products;
using UnityEngine;

namespace NaSvyazi.Store
{
    /// <summary>Позиция заказа: товар и количество коробок.</summary>
    [Serializable]
    public struct DeliveryItem
    {
        public ProductData product;
        public int boxes;

        public DeliveryItem(ProductData product, int boxes)
        {
            this.product = product;
            this.boxes = boxes;
        }
    }

    /// <summary>
    /// Доставка заказов: по дороге приезжает грузовик, останавливается напротив ворот склада,
    /// сигналит, коробки по одной появляются в зоне доставки, и грузовик уезжает.
    /// Коробки ставятся сеткой и штабелем, занятые места пропускаются,
    /// чтобы новые коробки не появлялись внутри старых.
    /// </summary>
    public class DeliveryService : MonoBehaviour
    {
        [SerializeField] private ProductBox boxPrefab;
        [SerializeField] private Transform deliveryPoint;

        [Tooltip("Время доставки в секундах")]
        [SerializeField] private float deliveryTime = 5f;

        [Header("Грузовик")]
        [Tooltip("Полоса дороги, по которой едет грузовик (координата Z)")]
        [SerializeField] private float roadLaneZ = -19.6f;
        [Tooltip("Откуда приезжает и куда уезжает (координата X)")]
        [SerializeField] private float roadStartX = 30f;
        [SerializeField] private float roadEndX = -40f;
        [SerializeField] private float roadY = -0.1f;
        [Tooltip("Сколько секунд грузовик едет до склада")]
        [SerializeField] private float driveInTime = 4f;
        [Tooltip("Пауза между коробками при выгрузке, сек")]
        [SerializeField] private float unloadInterval = 0.18f;

        private bool truckBusy;

        [Header("Раскладка коробок в зоне доставки")]
        [SerializeField] private int columns = 3;
        [SerializeField] private int rows = 3;
        [SerializeField] private int layers = 3;
        [SerializeField] private Vector3 spacing = new Vector3(0.45f, 0.32f, 0.35f);
        [SerializeField] private Vector3 boxHalfSize = new Vector3(0.2f, 0.15f, 0.15f);

        /// <summary>
        /// Фактическое время доставки: базовое × навык «Быстрая доставка» × событие дня
        /// («Сбой у поставщика» — втрое дольше).
        /// </summary>
        public float DeliveryTime => Mathf.Max(1f, deliveryTime * Progression.SkillManager.DeliveryTimeMultiplier
                                                   * Events.StoreEventManager.DeliveryMultiplier);

        /// <summary>Оформить доставку. Коробки появятся через DeliveryTime секунд.</summary>
        public void ScheduleDelivery(List<DeliveryItem> items)
        {
            if (boxPrefab == null || deliveryPoint == null)
            {
                Debug.LogError("DeliveryService: не назначены префаб коробки или точка доставки.");
                return;
            }

            StartCoroutine(DeliverRoutine(new List<DeliveryItem>(items)));
            GameEvents.Notify($"Заказ оформлен. Доставка через {DeliveryTime:0} сек.");
        }

        private IEnumerator DeliverRoutine(List<DeliveryItem> items)
        {
            // Грузовик выезжает так, чтобы приехать к сроку доставки.
            float drive = Mathf.Min(driveInTime, DeliveryTime);
            yield return new WaitForSeconds(Mathf.Max(0f, DeliveryTime - drive));

            // Один грузовик за раз: следующий заказ ждёт, пока предыдущий уедет.
            while (truckBusy) yield return null;
            truckBusy = true;

            Vector3 stop = new Vector3(deliveryPoint.position.x, roadY, roadLaneZ);
            Vector3 start = new Vector3(roadStartX, roadY, roadLaneZ);
            Vector3 exit = new Vector3(roadEndX, roadY, roadLaneZ);
            DeliveryTruck truck = DeliveryTruck.Create(start, (exit - start).normalized);

            yield return truck.DriveTo(stop, drive, false, true);
            truck.Honk();
            yield return new WaitForSeconds(0.4f);

            int slot = 0;
            int delivered = 0;
            int notPlaced = 0;

            foreach (DeliveryItem item in items)
            {
                for (int i = 0; i < item.boxes; i++)
                {
                    if (TryFindFreeSlot(ref slot, out Vector3 position))
                    {
                        ProductBox box = Instantiate(boxPrefab, position, deliveryPoint.rotation);
                        box.name = $"Box_{item.product.id}";
                        box.Fill(item.product, item.product.unitsPerBox);
                        box.gameObject.AddComponent<PopIn>();
                        Audio.SoundManager.Play(Audio.Sfx.Pickup, 0.35f);
                        delivered++;
                        slot++;
                        yield return new WaitForSeconds(unloadInterval);
                    }
                    else
                    {
                        notPlaced++;
                    }
                }
            }

            string message = $"Доставка прибыла: коробок — {delivered}";
            if (notPlaced > 0) message += $" (не хватило места для {notPlaced})";
            GameEvents.Notify(message);

            yield return new WaitForSeconds(0.6f);
            truckBusy = false; // следующий грузовик может выезжать, пока этот уезжает
            yield return truck.DriveTo(exit, 6f, true, false);
            Destroy(truck.gameObject);
        }

        /// <summary>Найти ближайшее свободное место, начиная с номера slot.</summary>
        private bool TryFindFreeSlot(ref int slot, out Vector3 position)
        {
            int total = columns * rows * layers;
            int mask = ~0;

            for (; slot < total; slot++)
            {
                position = SlotToWorld(slot);
                bool occupied = Physics.CheckBox(position, boxHalfSize * 0.95f, deliveryPoint.rotation,
                    mask, QueryTriggerInteraction.Ignore);
                if (!occupied) return true;
            }

            position = Vector3.zero;
            return false;
        }

        private Vector3 SlotToWorld(int slot)
        {
            int perLayer = columns * rows;
            int layer = slot / perLayer;
            int inLayer = slot % perLayer;
            int column = inLayer % columns;
            int row = inLayer / columns;

            Vector3 local = new Vector3(
                (column - (columns - 1) * 0.5f) * spacing.x,
                boxHalfSize.y + 0.01f + layer * spacing.y,
                row * spacing.z);

            return deliveryPoint.TransformPoint(local);
        }

        private void OnDrawGizmos()
        {
            if (deliveryPoint == null) return;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            for (int s = 0; s < columns * rows; s++)
                Gizmos.DrawWireCube(SlotToWorld(s), boxHalfSize * 2f);
        }
    }
}
